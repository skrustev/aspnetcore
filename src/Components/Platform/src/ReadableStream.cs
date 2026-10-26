// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a browser <c>ReadableStream</c> of bytes, such as the body of a <see cref="Response"/>.
/// </summary>
/// <remarks>
/// The stream belongs to the object it came from, and it becomes unusable when that object is disposed.
/// </remarks>
public sealed class ReadableStream
{
    private readonly FetchFeature _feature;
    private readonly JSObjectHandle _handle;

    private ReadableStream(FetchFeature feature, IJSObjectReference reference)
    {
        _feature = feature;
        _handle = new JSObjectHandle(this, reference);
    }

    internal static ReadableStream Create(FetchFeature feature, JSObjectHandle owner, IJSObjectReference reference)
    {
        var stream = new ReadableStream(feature, reference);
        AddToOwner(owner, stream._handle);

        return stream;
    }

    // An owner disposed during the call that returned the reference would never release it, so release it right away.
    internal static void AddToOwner(JSObjectHandle owner, JSObjectHandle child)
    {
        if (owner.IsDisposed)
        {
            _ = child.DisposeAsync().AsTask();
            owner.ThrowIfDisposed();
        }

        owner.AddChild(child);
    }

    /// <summary>
    /// Gets a value that indicates whether a reader has locked the stream.
    /// </summary>
    /// <returns><see langword="true"/> if the stream is locked; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> GetLockedAsync() => _handle.Reference.GetValueAsync<bool>("locked");

    /// <summary>
    /// Cancels the stream, so that the rest of its data is discarded.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask CancelAsync() => _handle.Reference.InvokeVoidAsync("cancel");

    /// <summary>
    /// Creates a reader and locks the stream to it.
    /// </summary>
    /// <returns>The reader, which belongs to the stream.</returns>
    public async ValueTask<ReadableStreamDefaultReader> GetReaderAsync()
    {
        var reference = await _handle.Reference.InvokeAsync<IJSObjectReference>("getReader").ConfigureAwait(false);
        var reader = new ReadableStreamDefaultReader(_feature, reference);
        AddToOwner(_handle, reader.Handle);

        return reader;
    }
}

