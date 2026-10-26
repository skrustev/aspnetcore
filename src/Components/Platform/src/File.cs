// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a browser <c>File</c>: a <see cref="Blob"/> with a name and a modification time.
/// </summary>
public sealed class File : Blob
{
    internal File(FetchFeature feature, IJSObjectReference reference)
        : base(feature, reference)
    {
    }

    /// <summary>
    /// Gets the name of the file, without a path.
    /// </summary>
    /// <returns>The name of the file.</returns>
    public ValueTask<string> GetNameAsync() => Handle.Reference.GetValueAsync<string>("name");

    /// <summary>
    /// Gets the time the file was last modified.
    /// </summary>
    /// <returns>The last modification time, in milliseconds since the Unix epoch.</returns>
    public ValueTask<long> GetLastModifiedAsync() => Handle.Reference.GetValueAsync<long>("lastModified");
}
