// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Internal;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

// The scope feature behind the Fetch family. It calls fetch, moves bodies to .NET, checks for bytes(), and releases the
// objects that the application did not dispose when the scope ends.
internal sealed class FetchFeature : IInternalAsyncDisposal
{
    internal const string ResponseBytes = "Response.prototype.bytes";
    internal const string RequestBytes = "Request.prototype.bytes";
    internal const string BlobBytes = "Blob.prototype.bytes";

    private readonly FetchOptions _options;
    private readonly HashSet<JSObjectHandle> _liveObjects = [];
    private readonly Dictionary<string, Task<MemberCheck>> _memberChecks = [];
    private bool _disposed;

    public FetchFeature(IJSRuntime jsRuntime, FetchOptions options)
    {
        JSRuntime = jsRuntime;
        _options = options;
    }

    public IJSRuntime JSRuntime { get; }

    [DynamicDependency(LinkerFlags.JsonSerialized, typeof(RequestInit))]
    public async ValueTask<Response> FetchAsync(object input, RequestInit? init, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IJSObjectReference reference;
        if (!cancellationToken.CanBeCanceled)
        {
            // Any token, even CancellationToken.None, turns off the default JS interop timeout.
            reference = init is null
                ? await JSRuntime.InvokeAsync<IJSObjectReference>("fetch", input).ConfigureAwait(false)
                : await JSRuntime.InvokeAsync<IJSObjectReference>("fetch", input, init).ConfigureAwait(false);
        }
        else
        {
            reference = await FetchWithAbortAsync(input, init, cancellationToken).ConfigureAwait(false);
        }

        return new Response(this, reference);
    }

    private async ValueTask<IJSObjectReference> FetchWithAbortAsync(object input, RequestInit? init, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var controller = await JSRuntime.InvokeConstructorAsync("AbortController", cancellationToken, []).ConfigureAwait(false);
        try
        {
            var signal = await controller.GetValueAsync<IJSObjectReference>("signal", cancellationToken).ConfigureAwait(false);
            try
            {
                // The token cancels only this call. The registration ends with it, so a later cancellation does not abort body reads.
                using var registration = cancellationToken.Register(static state => _ = AbortAsync((IJSObjectReference)state!), controller);
                try
                {
                    return await JSRuntime.InvokeAsync<IJSObjectReference>(
                        "fetch",
                        cancellationToken,
                        [input, (init ?? new RequestInit()).WithSignal(signal)]).ConfigureAwait(false);
                }
                catch (JSException) when (cancellationToken.IsCancellationRequested)
                {
                    // The browser rejects an aborted fetch with an AbortError.
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            finally
            {
                await JSObjectHandle.DisposeQuietlyAsync(signal).ConfigureAwait(false);
            }
        }
        finally
        {
            await JSObjectHandle.DisposeQuietlyAsync(controller).ConfigureAwait(false);
        }
    }

    private static async Task AbortAsync(IJSObjectReference controller)
    {
        try
        {
            await controller.InvokeVoidAsync("abort").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    // Registers an object that a call created, so that the scope can release it if the application does not.
    public JSObjectHandle Track(object owner, IJSObjectReference reference)
    {
        var handle = new JSObjectHandle(owner, reference);

        if (_disposed)
        {
            // The scope ended during the call. Nothing would release the reference later.
            _ = handle.DisposeAsync().AsTask();

            return handle;
        }

        _liveObjects.Add(handle);
        handle.OnDisposed(h => _liveObjects.Remove(h));

        return handle;
    }

    public async ValueTask<byte[]> ReadBytesAsync(IJSObjectReference target, string method)
    {
        // A body larger than the SignalR receive limit cannot travel as a plain interop value, so it travels as a stream.
        var streamReference = await target.InvokeAsync<IJSStreamReference>(method).ConfigureAwait(false);

        return await ReadStreamAsync(streamReference).ConfigureAwait(false);
    }

    public async ValueTask<byte[]> ReadValueBytesAsync(IJSObjectReference target, string property)
    {
        var streamReference = await target.GetValueAsync<IJSStreamReference>(property).ConfigureAwait(false);

        return await ReadStreamAsync(streamReference).ConfigureAwait(false);
    }

    private async ValueTask<byte[]> ReadStreamAsync(IJSStreamReference streamReference)
    {
        await using (streamReference.ConfigureAwait(false))
        {
            var maxBodySize = _options.MaxBodySize;
            if (streamReference.Length > maxBodySize || streamReference.Length > Array.MaxLength)
            {
                throw BodyTooLarge(streamReference.Length, maxBodySize, inner: null);
            }

            Stream stream;
            try
            {
                stream = await streamReference.OpenReadStreamAsync(maxBodySize).ConfigureAwait(false);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // The message names maxAllowedSize, a parameter that the caller never passed.
                throw BodyTooLarge(streamReference.Length, maxBodySize, ex);
            }

            await using (stream.ConfigureAwait(false))
            {
                var bytes = new byte[streamReference.Length];
                await stream.ReadExactlyAsync(bytes).ConfigureAwait(false);

                return bytes;
            }
        }
    }

    private static IOException BodyTooLarge(long length, long maxBodySize, Exception? inner)
        => new($"The body is {length} bytes, which is larger than FetchOptions.MaxBodySize ({maxBodySize} bytes).", inner);

    public async ValueTask<string> ReadTextAsync(IJSObjectReference target)
    {
        var bytes = await ReadBytesAsync(target, "arrayBuffer").ConfigureAwait(false);

        // text() decodes UTF-8 and removes a byte-order mark, and so does this.
        return Encoding.UTF8.GetString(WithoutByteOrderMark(bytes));
    }

    public async ValueTask<JsonElement> ReadJsonAsync(IJSObjectReference target)
    {
        var bytes = await ReadBytesAsync(target, "arrayBuffer").ConfigureAwait(false);

        // json() decodes UTF-8 like text(), so a byte-order mark is removed before parsing.
        using var document = JsonDocument.Parse(bytes.AsMemory(ByteOrderMarkLength(bytes)));

        return document.RootElement.Clone();
    }

    private static ReadOnlySpan<byte> WithoutByteOrderMark(byte[] bytes) => bytes.AsSpan(ByteOrderMarkLength(bytes));

    private static int ByteOrderMarkLength(byte[] bytes)
        => bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;

    public async ValueTask<byte[]> ReadBytesWithMemberCheckAsync(IJSObjectReference target, string prototypeMember)
    {
        var check = await CheckMemberAsync(prototypeMember).ConfigureAwait(false);
        if (!check.Exists)
        {
            throw new NotSupportedException($"The browser does not provide {prototypeMember.Replace(".prototype.", ".", StringComparison.Ordinal)}() for the current page.", check.Error);
        }

        return await ReadBytesAsync(target, "bytes").ConfigureAwait(false);
    }

    // Checks once for each scope whether the browser has a member that is newer than the supported browsers. A JSException
    // from the call itself, such as a body that was already used, is then never mistaken for a missing member.
    private Task<MemberCheck> CheckMemberAsync(string prototypeMember)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_memberChecks.TryGetValue(prototypeMember, out var check) || check.IsFaulted || check.IsCanceled)
        {
            check = RunMemberCheckAsync(prototypeMember);
            _memberChecks[prototypeMember] = check;
        }

        return check;
    }

    private async Task<MemberCheck> RunMemberCheckAsync(string prototypeMember)
    {
        IJSObjectReference? function;
        try
        {
            function = await JSRuntime.GetValueAsync<IJSObjectReference?>(prototypeMember).ConfigureAwait(false);
        }
        catch (JSException ex)
        {
            // Interop throws for a property that is not defined.
            return new MemberCheck(Exists: false, ex);
        }

        if (function is null)
        {
            return new MemberCheck(Exists: false, Error: null);
        }

        await JSObjectHandle.DisposeQuietlyAsync(function).ConfigureAwait(false);

        return new MemberCheck(Exists: true, Error: null);
    }

    async ValueTask IInternalAsyncDisposal.InternalDisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Releases the objects that the application did not dispose. Disposal of a handle never throws.
        foreach (var handle in _liveObjects.ToArray())
        {
            await handle.DisposeAsync().ConfigureAwait(false);
        }

        _liveObjects.Clear();
    }

    private readonly record struct MemberCheck(bool Exists, JSException? Error);
}
