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

namespace Dapr.Actors.Runtime;

using System;
using System.Collections.Generic;
using System.Reflection;
using Dapr.Actors.Internal;

/// <summary>
/// Actor method executor map for non-remoting calls. method_name -> ObjectMethodExecutor for methods defined in IActor interfaces.
/// </summary>
internal class ActorMethodExecutorMap
{
    private readonly Dictionary<string, ObjectMethodExecutor> executors;

    public ActorMethodExecutorMap(IEnumerable<Type> interfaceTypes)
    {
        this.executors = new Dictionary<string, ObjectMethodExecutor>();

        foreach (var actorInterface in interfaceTypes)
        {
            var targetTypeInfo = actorInterface.GetTypeInfo();
            foreach (var methodInfo in actorInterface.GetMethods())
            {
                var executor = ObjectMethodExecutor.Create(methodInfo, targetTypeInfo);
                this.executors.Add(methodInfo.Name, executor);
            }
        }
    }

    public ObjectMethodExecutor LookupActorMethodExecutor(string methodName)
    {
        if (!this.executors.TryGetValue(methodName, out var executor))
        {
            throw new MissingMethodException($"Actor type doesn't contain method {methodName}");
        }

        return executor;
    }
}
