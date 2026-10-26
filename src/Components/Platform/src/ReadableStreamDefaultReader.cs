// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a browser <c>ReadableStreamDefaultReader</c>, which reads the chunks of a <see cref="ReadableStream"/>.
/// </summary>
/// <remarks>
/// The reader belongs to its stream, and it becomes unusable when the object that the stream came from is disposed.
/// </remarks>
public sealed class ReadableStreamDefaultReader
{
    private readonly FetchFeature _feature;

    internal ReadableStreamDefaultReader(FetchFeature feature, IJSObjectReference reference)
    {
        _feature = feature;
        Handle = new JSObjectHandle(this, reference);
    }

    internal JSObjectHandle Handle { get; }

    /// <summary>
    /// Reads the next chunk of the stream and copies it to .NET.
    /// </summary>
    /// <returns>The chunk, or a result whose <see cref="ReadableStreamReadResult.Done"/> is <see langword="true"/> at the end of the stream.</returns>
    /// <exception cref="IOException">The chunk is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public async ValueTask<ReadableStreamReadResult> ReadAsync()
    {
        var result = await Handle.Reference.InvokeAsync<IJSObjectReference>("read").ConfigureAwait(false);
        try
        {
            if (await result.GetValueAsync<bool>("done").ConfigureAwait(false))
            {
                return new ReadableStreamReadResult { Done = true };
            }

            // A chunk can be larger than the SignalR receive limit, so it travels as a stream.
            var value = await _feature.ReadValueBytesAsync(result, "value").ConfigureAwait(false);

            return new ReadableStreamReadResult { Value = value, Done = false };
        }
        finally
        {
            await JSObjectHandle.DisposeQuietlyAsync(result).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases the lock of the reader, so that another reader can read the stream.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask ReleaseLockAsync() => Handle.Reference.InvokeVoidAsync("releaseLock");

    /// <summary>
    /// Cancels the stream, so that the rest of its data is discarded.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask CancelAsync() => Handle.Reference.InvokeVoidAsync("cancel");
}
