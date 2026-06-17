// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Dapr.Actors.Internal
{
    internal class ObjectMethodExecutor
    {
        private readonly object[] _parameterDefaultValues;
        private readonly MethodExecutorAsync _executorAsync;
        private readonly MethodExecutor _executor;

        private static readonly ConstructorInfo _objectMethodExecutorAwaitableConstructor =
            typeof(ObjectMethodExecutorAwaitable).GetConstructor(new[] {
                typeof(object),
                typeof(Func<object, object>),
                typeof(Func<object, bool>),
                typeof(Func<object, object>),
                typeof(Action<object, Action>),
                typeof(Action<object, Action>)
            });

        private ObjectMethodExecutor(MethodInfo methodInfo, TypeInfo targetTypeInfo, object[] parameterDefaultValues)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            MethodInfo = methodInfo;
            MethodParameters = methodInfo.GetParameters();
            TargetTypeInfo = targetTypeInfo;
            MethodReturnType = methodInfo.ReturnType;

            var isAwaitable = CoercedAwaitableInfo.IsTypeAwaitable(MethodReturnType, out var coercedAwaitableInfo);

            IsMethodAsync = isAwaitable;
            AsyncResultType = isAwaitable ? coercedAwaitableInfo.AwaitableInfo.ResultType : null;

            _executor = GetExecutor(methodInfo, targetTypeInfo);

            if (IsMethodAsync)
            {
                _executorAsync = GetExecutorAsync(methodInfo, targetTypeInfo, coercedAwaitableInfo);
            }

            _parameterDefaultValues = parameterDefaultValues;
        }

        private delegate ObjectMethodExecutorAwaitable MethodExecutorAsync(object target, object[] parameters);

        private delegate object MethodExecutor(object target, object[] parameters);

        private delegate void VoidMethodExecutor(object target, object[] parameters);

        public MethodInfo MethodInfo { get; }

        public ParameterInfo[] MethodParameters { get; }

        public TypeInfo TargetTypeInfo { get; }

        public Type AsyncResultType { get; }

        public Type MethodReturnType { get; internal set; }

        public bool IsMethodAsync { get; }

        public static ObjectMethodExecutor Create(MethodInfo methodInfo, TypeInfo targetTypeInfo)
        {
            return new ObjectMethodExecutor(methodInfo, targetTypeInfo, null);
        }

        public static ObjectMethodExecutor Create(MethodInfo methodInfo, TypeInfo targetTypeInfo, object[] parameterDefaultValues)
        {
            if (parameterDefaultValues == null)
            {
                throw new ArgumentNullException(nameof(parameterDefaultValues));
            }

            return new ObjectMethodExecutor(methodInfo, targetTypeInfo, parameterDefaultValues);
        }

        public object Execute(object target, object[] parameters)
        {
            return _executor(target, parameters);
        }

        public ObjectMethodExecutorAwaitable ExecuteAsync(object target, object[] parameters)
        {
            return _executorAsync(target, parameters);
        }

        public object GetDefaultValueForParameter(int index)
        {
            if (_parameterDefaultValues == null)
            {
                throw new InvalidOperationException($"Cannot call {nameof(GetDefaultValueForParameter)}, because no parameter default values were supplied.");
            }

            if (index < 0 || index > MethodParameters.Length - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _parameterDefaultValues[index];
        }

        private static MethodExecutor GetExecutor(MethodInfo methodInfo, TypeInfo targetTypeInfo)
        {
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

            var parameters = new List<Expression>();
            var paramInfos = methodInfo.GetParameters();
            for (int i = 0; i < paramInfos.Length; i++)
            {
                var paramInfo = paramInfos[i];
                var valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
                var valueCast = Expression.Convert(valueObj, paramInfo.ParameterType);
                parameters.Add(valueCast);
            }

            var instanceCast = Expression.Convert(targetParameter, targetTypeInfo.AsType());
            var methodCall = Expression.Call(instanceCast, methodInfo, parameters);

            if (methodCall.Type == typeof(void))
            {
                var lambda = Expression.Lambda<VoidMethodExecutor>(methodCall, targetParameter, parametersParameter);
                var voidExecutor = lambda.Compile();
                return WrapVoidMethod(voidExecutor);
            }
            else
            {
                var castMethodCall = Expression.Convert(methodCall, typeof(object));
                var lambda = Expression.Lambda<MethodExecutor>(castMethodCall, targetParameter, parametersParameter);
                return lambda.Compile();
            }
        }

        private static MethodExecutor WrapVoidMethod(VoidMethodExecutor executor)
        {
            return delegate (object target, object[] parameters)
            {
                executor(target, parameters);
                return null;
            };
        }

        private static MethodExecutorAsync GetExecutorAsync(
            MethodInfo methodInfo,
            TypeInfo targetTypeInfo,
            CoercedAwaitableInfo coercedAwaitableInfo)
        {
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

            var parameters = new List<Expression>();
            var paramInfos = methodInfo.GetParameters();
            for (int i = 0; i < paramInfos.Length; i++)
            {
                var paramInfo = paramInfos[i];
                var valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
                var valueCast = Expression.Convert(valueObj, paramInfo.ParameterType);
                parameters.Add(valueCast);
            }

            var instanceCast = Expression.Convert(targetParameter, targetTypeInfo.AsType());
            var methodCall = Expression.Call(instanceCast, methodInfo, parameters);

            var customAwaitableParam = Expression.Parameter(typeof(object), "awaitable");
            var awaitableInfo = coercedAwaitableInfo.AwaitableInfo;
            var postCoercionMethodReturnType = coercedAwaitableInfo.CoercerResultType ?? methodInfo.ReturnType;
            var getAwaiterFunc = Expression.Lambda<Func<object, object>>(
                Expression.Convert(
                    Expression.Call(
                        Expression.Convert(customAwaitableParam, postCoercionMethodReturnType),
                        awaitableInfo.GetAwaiterMethod),
                    typeof(object)),
                customAwaitableParam).Compile();

            var isCompletedParam = Expression.Parameter(typeof(object), "awaiter");
            var isCompletedFunc = Expression.Lambda<Func<object, bool>>(
                Expression.MakeMemberAccess(
                    Expression.Convert(isCompletedParam, awaitableInfo.AwaiterType),
                    awaitableInfo.AwaiterIsCompletedProperty),
                isCompletedParam).Compile();

            var getResultParam = Expression.Parameter(typeof(object), "awaiter");
            Func<object, object> getResultFunc;
            if (awaitableInfo.ResultType == typeof(void))
            {
                getResultFunc = Expression.Lambda<Func<object, object>>(
                    Expression.Block(
                        Expression.Call(
                            Expression.Convert(getResultParam, awaitableInfo.AwaiterType),
                            awaitableInfo.AwaiterGetResultMethod),
                        Expression.Constant(null)
                    ),
                    getResultParam).Compile();
            }
            else
            {
                getResultFunc = Expression.Lambda<Func<object, object>>(
                    Expression.Convert(
                        Expression.Call(
                            Expression.Convert(getResultParam, awaitableInfo.AwaiterType),
                            awaitableInfo.AwaiterGetResultMethod),
                        typeof(object)),
                    getResultParam).Compile();
            }

            var onCompletedParam1 = Expression.Parameter(typeof(object), "awaiter");
            var onCompletedParam2 = Expression.Parameter(typeof(Action), "continuation");
            var onCompletedFunc = Expression.Lambda<Action<object, Action>>(
                Expression.Call(
                    Expression.Convert(onCompletedParam1, awaitableInfo.AwaiterType),
                    awaitableInfo.AwaiterOnCompletedMethod,
                    onCompletedParam2),
                onCompletedParam1,
                onCompletedParam2).Compile();

            Action<object, Action> unsafeOnCompletedFunc = null;
            if (awaitableInfo.AwaiterUnsafeOnCompletedMethod != null)
            {
                var unsafeOnCompletedParam1 = Expression.Parameter(typeof(object), "awaiter");
                var unsafeOnCompletedParam2 = Expression.Parameter(typeof(Action), "continuation");
                unsafeOnCompletedFunc = Expression.Lambda<Action<object, Action>>(
                    Expression.Call(
                        Expression.Convert(unsafeOnCompletedParam1, awaitableInfo.AwaiterType),
                        awaitableInfo.AwaiterUnsafeOnCompletedMethod,
                        unsafeOnCompletedParam2),
                    unsafeOnCompletedParam1,
                    unsafeOnCompletedParam2).Compile();
            }

            var coercedMethodCall = coercedAwaitableInfo.RequiresCoercion
                ? Expression.Invoke(coercedAwaitableInfo.CoercerExpression, methodCall)
                : (Expression)methodCall;

            var returnValueExpression = Expression.New(
                _objectMethodExecutorAwaitableConstructor,
                Expression.Convert(coercedMethodCall, typeof(object)),
                Expression.Constant(getAwaiterFunc),
                Expression.Constant(isCompletedFunc),
                Expression.Constant(getResultFunc),
                Expression.Constant(onCompletedFunc),
                Expression.Constant(unsafeOnCompletedFunc, typeof(Action<object, Action>)));

            var lambda = Expression.Lambda<MethodExecutorAsync>(returnValueExpression, targetParameter, parametersParameter);
            return lambda.Compile();
        }
    }
}
