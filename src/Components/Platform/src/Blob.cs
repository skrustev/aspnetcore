// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a browser <c>Blob</c>: immutable binary data that stays in the browser.
/// </summary>
/// <remarks>
/// The blob holds a browser object until it is disposed.
/// </remarks>
[JsonConverter(typeof(BlobJsonConverter))]
public class Blob : IAsyncDisposable
{
    internal Blob(FetchFeature feature, IJSObjectReference reference)
    {
        Feature = feature;
        Handle = feature.Track(this, reference);
    }

    internal FetchFeature Feature { get; }

    internal JSObjectHandle Handle { get; }

    /// <summary>
    /// Gets the size of the blob.
    /// </summary>
    /// <returns>The size of the blob, in bytes.</returns>
    public ValueTask<ulong> GetSizeAsync() => Handle.Reference.GetValueAsync<ulong>("size");

    /// <summary>
    /// Gets the media type of the blob.
    /// </summary>
    /// <returns>The media type of the blob, or an empty string if it is unknown.</returns>
    public ValueTask<string> GetTypeAsync() => Handle.Reference.GetValueAsync<string>("type");

    /// <summary>
    /// Creates a blob with the same data and no media type.
    /// </summary>
    /// <returns>The new blob, which the caller disposes.</returns>
    public async ValueTask<Blob> SliceAsync()
        => new Blob(Feature, await Handle.Reference.InvokeAsync<IJSObjectReference>("slice").ConfigureAwait(false));

    /// <summary>
    /// Creates a blob from the data starting at the specified offset.
    /// </summary>
    /// <param name="start">The offset of the first byte. A negative offset counts from the end.</param>
    /// <returns>The new blob, which the caller disposes.</returns>
    public async ValueTask<Blob> SliceAsync(long start)
        => new Blob(Feature, await Handle.Reference.InvokeAsync<IJSObjectReference>("slice", start).ConfigureAwait(false));

    /// <summary>
    /// Creates a blob from the data in the specified range.
    /// </summary>
    /// <param name="start">The offset of the first byte. A negative offset counts from the end.</param>
    /// <param name="end">The offset after the last byte. A negative offset counts from the end.</param>
    /// <returns>The new blob, which the caller disposes.</returns>
    public async ValueTask<Blob> SliceAsync(long start, long end)
        => new Blob(Feature, await Handle.Reference.InvokeAsync<IJSObjectReference>("slice", start, end).ConfigureAwait(false));

    /// <summary>
    /// Creates a blob with the specified media type from the data in the specified range.
    /// </summary>
    /// <param name="start">The offset of the first byte. A negative offset counts from the end.</param>
    /// <param name="end">The offset after the last byte. A negative offset counts from the end.</param>
    /// <param name="contentType">The media type of the new blob.</param>
    /// <returns>The new blob, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contentType"/> is <see langword="null"/>.</exception>
    public async ValueTask<Blob> SliceAsync(long start, long end, string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        return new Blob(Feature, await Handle.Reference.InvokeAsync<IJSObjectReference>("slice", start, end, contentType).ConfigureAwait(false));
    }

    /// <summary>
    /// Creates a stream that reads the data of the blob in the browser.
    /// </summary>
    /// <returns>A new stream, which belongs to the blob and is released with it.</returns>
    public async ValueTask<ReadableStream> StreamAsync()
    {
        var reference = await Handle.Reference.InvokeAsync<IJSObjectReference>("stream").ConfigureAwait(false);

        return ReadableStream.Create(Feature, Handle, reference);
    }

    /// <summary>
    /// Reads the data of the blob as UTF-8 text.
    /// </summary>
    /// <returns>The data as text.</returns>
    /// <exception cref="IOException">The blob is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public ValueTask<string> TextAsync() => Feature.ReadTextAsync(Handle.Reference);

    /// <summary>
    /// Reads the data of the blob and copies it to .NET.
    /// </summary>
    /// <returns>The bytes of the blob.</returns>
    /// <exception cref="IOException">The blob is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public ValueTask<byte[]> ArrayBufferAsync() => Feature.ReadBytesAsync(Handle.Reference, "arrayBuffer");

    /// <summary>
    /// Reads the data of the blob and copies it to .NET.
    /// </summary>
    /// <returns>The bytes of the blob.</returns>
    /// <exception cref="NotSupportedException">The browser does not provide <c>Blob.bytes()</c> for the current page.</exception>
    /// <exception cref="IOException">The blob is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public ValueTask<byte[]> BytesAsync() => Feature.ReadBytesWithMemberCheckAsync(Handle.Reference, FetchFeature.BlobBytes);

    /// <summary>
    /// Releases the browser object of the blob, together with its streams.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask DisposeAsync() => Handle.DisposeAsync();
}

