// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Specifies the mode of a request, which decides how the browser handles a cross-origin request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RequestMode>))]
public enum RequestMode
{
    /// <summary>
    /// The request is a navigation. The browser returns this mode for navigation requests and rejects it in <see cref="RequestInit"/>.
    /// </summary>
    [JsonStringEnumMemberName("navigate")]
    Navigate,

    /// <summary>
    /// The browser rejects a request to another origin.
    /// </summary>
    [JsonStringEnumMemberName("same-origin")]
    SameOrigin,

    /// <summary>
    /// The browser sends a request to another origin without CORS and returns an opaque response.
    /// </summary>
    [JsonStringEnumMemberName("no-cors")]
    NoCors,

    /// <summary>
    /// The browser sends a request to another origin with CORS.
    /// </summary>
    [JsonStringEnumMemberName("cors")]
    Cors,
}
