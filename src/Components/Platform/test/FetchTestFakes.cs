// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace Microsoft.AspNetCore.Components.Platform.Tests;

internal sealed record FakeCall(string Kind, string Identifier, object?[] Arguments, CancellationToken CancellationToken);

// Answers each identifier with a scripted member. A member returns a value, throws, or returns a Task<object?> for a pending call.
internal static class FakeJS
{
    public static ValueTask<TValue> Run<TValue>(
        string owner,
        List<FakeCall> calls,
        Dictionary<string, Func<object?[], object?>> members,
        string kind,
        string identifier,
        object?[]? args,
        CancellationToken cancellationToken)
    {
        calls.Add(new FakeCall(kind, identifier, args ?? [], cancellationToken));

        if (!members.TryGetValue(identifier, out var member))
        {
            throw new InvalidOperationException($"{owner} has no member '{identifier}'.");
        }

        object? result;
        try
        {
            result = member(args ?? []);
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<TValue>(ex);
        }

        return result is Task<object?> pending ? AwaitAsync<TValue>(pending) : ValueTask.FromResult((TValue)result!);
    }

    private static async ValueTask<TValue> AwaitAsync<TValue>(Task<object?> pending) => (TValue)(await pending)!;
}

internal sealed class FakeJSRuntime : IJSRuntime
{
    public Dictionary<string, Func<object?[], object?>> Members { get; } = [];

    public List<FakeCall> Calls { get; } = [];

    public FakeJSRuntime Member(string identifier, Func<object?[], object?> member)
    {
        Members[identifier] = member;

        return this;
    }

    public IEnumerable<FakeCall> CallsTo(string identifier) => Calls.Where(call => call.Identifier == identifier);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => FakeJS.Run<TValue>("window", Calls, Members, "invoke", identifier, args, CancellationToken.None);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => FakeJS.Run<TValue>("window", Calls, Members, "invoke", identifier, args, cancellationToken);

    public ValueTask<IJSObjectReference> InvokeConstructorAsync(string identifier, object?[]? args)
        => FakeJS.Run<IJSObjectReference>("window", Calls, Members, "new", identifier, args, CancellationToken.None);

    public ValueTask<IJSObjectReference> InvokeConstructorAsync(string identifier, CancellationToken cancellationToken, object?[]? args)
        => FakeJS.Run<IJSObjectReference>("window", Calls, Members, "new", identifier, args, cancellationToken);

    public ValueTask<TValue> GetValueAsync<TValue>(string identifier)
        => FakeJS.Run<TValue>("window", Calls, Members, "get", identifier, null, CancellationToken.None);

    public ValueTask<TValue> GetValueAsync<TValue>(string identifier, CancellationToken cancellationToken)
        => FakeJS.Run<TValue>("window", Calls, Members, "get", identifier, null, cancellationToken);
}

internal sealed class FakeJSObject(string name) : IJSObjectReference
{
    public string Name { get; } = name;

    public Dictionary<string, Func<object?[], object?>> Members { get; } = [];

    public List<FakeCall> Calls { get; } = [];

    public int DisposeCount { get; private set; }

    public bool Disposed => DisposeCount > 0;

    public Exception? DisposeException { get; set; }

    public FakeJSObject Member(string identifier, Func<object?[], object?> member)
    {
        Members[identifier] = member;

        return this;
    }

    public FakeJSObject Value(string identifier, object? value) => Member(identifier, _ => value);

    public IEnumerable<FakeCall> CallsTo(string identifier) => Calls.Where(call => call.Identifier == identifier);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => FakeJS.Run<TValue>(Name, Calls, Members, "invoke", identifier, args, CancellationToken.None);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => FakeJS.Run<TValue>(Name, Calls, Members, "invoke", identifier, args, cancellationToken);

    public ValueTask<TValue> GetValueAsync<TValue>(string identifier)
        => FakeJS.Run<TValue>(Name, Calls, Members, "get", identifier, null, CancellationToken.None);

    public ValueTask<TValue> GetValueAsync<TValue>(string identifier, CancellationToken cancellationToken)
        => FakeJS.Run<TValue>(Name, Calls, Members, "get", identifier, null, cancellationToken);

    public ValueTask DisposeAsync()
    {
        DisposeCount++;

        return DisposeException is null ? ValueTask.CompletedTask : ValueTask.FromException(DisposeException);
    }

    public override string ToString() => Name;
}

internal sealed class FakeJSStream(byte[] data) : IJSStreamReference
{
    public long Length => data.Length;

    public long? MaxAllowedSize { get; private set; }

    public bool Disposed { get; private set; }

    public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        MaxAllowedSize = maxAllowedSize;
        if (Length > maxAllowedSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAllowedSize));
        }

        return ValueTask.FromResult<Stream>(new MemoryStream(data));
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;

        return ValueTask.CompletedTask;
    }
}

// Runs the real JSRuntime serializer, records the JSON that the browser would receive, and answers each call
// that expects an object reference with a new reference.
internal sealed class SerializingJSRuntime : JSRuntime
{
    private long _nextObjectId = 1;

    public List<(string Identifier, string? ArgsJson)> Calls { get; } = [];

    public List<byte[]> SentByteArrays { get; } = [];

    public string ArgumentsSentTo(string identifier) => Calls.Last(call => call.Identifier == identifier).ArgsJson ?? string.Empty;

    public string Serialize(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), JsonSerializerOptions);

    protected override void BeginInvokeJS(in JSInvocationInfo invocationInfo)
    {
        Calls.Add((invocationInfo.Identifier, invocationInfo.ArgsJson));

        var result = invocationInfo.ResultType == JSCallResultType.JSObjectReference
            ? $$"""{"__jsObjectId":{{_nextObjectId++}}}"""
            : "null";

        if (invocationInfo.AsyncHandle != 0)
        {
            DotNetDispatcher.EndInvokeJS(this, $"[{invocationInfo.AsyncHandle},true,{result}]");
        }
    }

    protected override void BeginInvokeJS(long taskId, string identifier, string? argsJson, JSCallResultType resultType, long targetInstanceId)
        => throw new NotSupportedException();

    protected override void EndInvokeDotNet(DotNetInvocationInfo invocationInfo, in DotNetInvocationResult invocationResult)
        => throw new NotSupportedException();

    protected override void SendByteArray(int id, byte[] data) => SentByteArrays.Add(data);
}

internal static class FetchTestServices
{
    public static ServiceProvider Create(IJSRuntime jsRuntime, Action<FetchOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(jsRuntime);
        services.AddBrowserPlatform();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.BuildServiceProvider();
    }

    public static Window GetWindow(this ServiceProvider provider) => provider.GetRequiredService<IBrowserPlatform>().Window;
}
