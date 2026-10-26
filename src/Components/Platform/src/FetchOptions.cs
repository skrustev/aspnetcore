// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Provides options for the Fetch API projection.
/// </summary>
public sealed class FetchOptions
{
    private long _maxBodySize = 512_000;

    /// <summary>
    /// Gets or sets the largest body, in bytes, that <see cref="Response.ArrayBufferAsync"/>, <see cref="Response.BytesAsync"/>,
    /// <see cref="Response.TextAsync"/>, <see cref="Response.JsonAsync"/>, and the same methods of <see cref="Request"/> and
    /// <see cref="Blob"/> transfer to .NET. The limit also applies to each chunk that
    /// <see cref="ReadableStreamDefaultReader.ReadAsync"/> transfers. The default is 512,000 bytes.
    /// </summary>
    /// <remarks>
    /// Set <see cref="long.MaxValue"/> to remove the limit. <see cref="Response.BlobAsync"/> and <see cref="Response.GetBodyAsync"/>
    /// keep the body in the browser, so the limit does not apply to them.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxBodySize
    {
        get => _maxBodySize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxBodySize = value;
        }
    }
}
