// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents the result of <see cref="ReadableStreamDefaultReader.ReadAsync"/>.
/// </summary>
public sealed class ReadableStreamReadResult
{
    /// <summary>
    /// Gets the bytes of the chunk, or <see langword="null"/> at the end of the stream.
    /// </summary>
    public byte[]? Value { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the stream has ended.
    /// </summary>
    public bool? Done { get; init; }
}
