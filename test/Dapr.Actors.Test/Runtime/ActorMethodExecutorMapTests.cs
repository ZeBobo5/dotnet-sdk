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

namespace Dapr.Actors.Test.Runtime;

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;
using Xunit;

public sealed class ActorMethodExecutorMapTests
{
    [Fact]
    public void Lookup_ExistingMethod_ReturnsExecutor()
    {
        var map = new ActorMethodExecutorMap(new[] { typeof(ITestActorInterface) });

        var executor = map.LookupActorMethodExecutor(nameof(ITestActorInterface.DoWorkAsync));

        Assert.NotNull(executor);
        Assert.True(executor.IsMethodAsync);
    }

    [Fact]
    public void Lookup_NonExistingMethod_ThrowsMissingMethodException()
    {
        var map = new ActorMethodExecutorMap(new[] { typeof(ITestActorInterface) });

        Assert.Throws<MissingMethodException>(() =>
            map.LookupActorMethodExecutor("NonExistentMethod"));
    }

    [Fact]
    public void Lookup_ReturnsSameInstanceOnRepeatedCalls()
    {
        var map = new ActorMethodExecutorMap(new[] { typeof(ITestActorInterface) });

        var first = map.LookupActorMethodExecutor(nameof(ITestActorInterface.DoWorkAsync));
        var second = map.LookupActorMethodExecutor(nameof(ITestActorInterface.DoWorkAsync));

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Executor_CanInvokeMethod()
    {
        var map = new ActorMethodExecutorMap(new[] { typeof(ITestActorInterface) });
        var executor = map.LookupActorMethodExecutor(nameof(ITestActorInterface.DoWorkAsync));

        var helper = new TestActorInterfaceHelper();
        var awaitable = executor.ExecuteAsync(helper, null);

        // The method is synchronous (returns Task.CompletedTask), so we can just await it.
        await awaitable;
    }

    [Fact]
    public void Lookup_DifferentMethods_ReturnsDifferentExecutors()
    {
        var map = new ActorMethodExecutorMap(new[] { typeof(ITestActorInterface) });

        var executor1 = map.LookupActorMethodExecutor(nameof(ITestActorInterface.DoWorkAsync));
        var executor2 = map.LookupActorMethodExecutor(nameof(ITestActorInterface.GetValueAsync));

        Assert.NotSame(executor1, executor2);
    }

    [Fact]
    public void Constructor_MultipleInterfaces_IndexesAll()
    {
        var map = new ActorMethodExecutorMap(new[] { typeof(ITestActorInterface), typeof(IOtherActorInterface) });

        var executor1 = map.LookupActorMethodExecutor(nameof(ITestActorInterface.DoWorkAsync));
        var executor2 = map.LookupActorMethodExecutor(nameof(IOtherActorInterface.DoOtherWork));

        Assert.NotNull(executor1);
        Assert.NotNull(executor2);
    }

    public interface ITestActorInterface : IActor
    {
        Task DoWorkAsync();
        Task<int> GetValueAsync();
    }

    public interface IOtherActorInterface : IActor
    {
        Task DoOtherWork();
    }

    private class TestActorInterfaceHelper : ITestActorInterface
    {
        public Task DoWorkAsync() => Task.CompletedTask;
        public Task<int> GetValueAsync() => Task.FromResult(42);
    }
}
