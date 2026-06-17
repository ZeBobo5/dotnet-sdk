// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Test.Internal;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Dapr.Actors.Internal;
using Xunit;

public sealed class ObjectMethodExecutorTests
{
    [Fact]
    public void Create_NullMethodInfo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObjectMethodExecutor.Create(null, typeof(object).GetTypeInfo()));
    }

    [Fact]
    public void Execute_VoidReturnMethod_ReturnsNull()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.VoidMethod));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        var result = executor.Execute(new TestHelper(), null);

        Assert.Null(result);
        Assert.False(executor.IsMethodAsync);
    }

    [Fact]
    public void Execute_IntReturnMethod_ReturnsValue()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.GetInt));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        var result = executor.Execute(new TestHelper(), null);

        Assert.Equal(42, result);
        Assert.False(executor.IsMethodAsync);
    }

    [Fact]
    public void Execute_StringReturnMethod_ReturnsValue()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.GetString));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        var result = executor.Execute(new TestHelper(), null);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Execute_WithParameters_PassesArgumentsCorrectly()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.Add));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        var result = executor.Execute(new TestHelper(), new object[] { 3, 7 });

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task ExecuteAsync_TaskMethod_AwaitsCorrectly()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.TaskMethod));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        Assert.True(executor.IsMethodAsync);
        Assert.Equal(typeof(void), executor.AsyncResultType);

        var awaitable = executor.ExecuteAsync(new TestHelper(), null);
        await awaitable;
    }

    [Fact]
    public void ExecuteAsync_TaskOfTMethod_IsAsyncAndReturnsResultType()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.TaskOfStringMethod));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        Assert.True(executor.IsMethodAsync);
        Assert.Equal(typeof(string), executor.AsyncResultType);
    }

    [Fact]
    public void ExecuteAsync_TaskOfIntMethod_IsAsyncAndReturnsResultType()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.TaskOfIntMethod));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        Assert.True(executor.IsMethodAsync);
        Assert.Equal(typeof(int), executor.AsyncResultType);
    }

    [Fact]
    public void ExecuteAsync_VoidTask_IsAsyncWithVoidResultType()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.TaskMethod));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        Assert.True(executor.IsMethodAsync);
        Assert.Equal(typeof(void), executor.AsyncResultType);
    }

    [Fact]
    public void MethodParameters_ReturnsCorrectParameterInfo()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.Add));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        Assert.Equal(2, executor.MethodParameters.Length);
        Assert.Equal(typeof(int), executor.MethodParameters[0].ParameterType);
        Assert.Equal(typeof(int), executor.MethodParameters[1].ParameterType);
    }

    [Fact]
    public void MethodInfo_ReturnsOriginalMethodInfo()
    {
        var method = typeof(TestHelper).GetMethod(nameof(TestHelper.GetInt));
        var executor = ObjectMethodExecutor.Create(method, typeof(TestHelper).GetTypeInfo());

        Assert.Same(method, executor.MethodInfo);
    }

    public class TestHelper
    {
        public void VoidMethod() { }
        public int GetInt() => 42;
        public string GetString() => "hello";
        public int Add(int a, int b) => a + b;
        public Task TaskMethod() => Task.CompletedTask;
        public Task<string> TaskOfStringMethod() => Task.FromResult("async result");
        public Task<int> TaskOfIntMethod() => Task.FromResult(99);
    }
}
