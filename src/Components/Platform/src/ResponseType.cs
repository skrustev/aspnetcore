// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Specifies the type of a response.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResponseType>))]
public enum ResponseType
{
    /// <summary>
    /// A response from the same origin.
    /// </summary>
    [JsonStringEnumMemberName("basic")]
    Basic,

    /// <summary>
    /// A response from another origin, received with CORS.
    /// </summary>
    [JsonStringEnumMemberName("cors")]
    Cors,

    /// <summary>
    /// A response that was not created by a request, such as one from a constructor.
    /// </summary>
    [JsonStringEnumMemberName("default")]
    Default,

    /// <summary>
    /// A network error.
    /// </summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>
    /// A response from another origin, received without CORS. Its status is 0, and its headers and body are hidden.
    /// </summary>
    [JsonStringEnumMemberName("opaque")]
    Opaque,

    /// <summary>
    /// A redirect that the browser did not follow, because the request used <see cref="RequestRedirect.Manual"/>. Its status is 0.
    /// </summary>
    [JsonStringEnumMemberName("opaqueredirect")]
    OpaqueRedirect,
}
