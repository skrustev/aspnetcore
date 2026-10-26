// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Specifies when the browser sends credentials, such as cookies, with a request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RequestCredentials>))]
public enum RequestCredentials
{
    /// <summary>
    /// The browser never sends credentials.
    /// </summary>
    [JsonStringEnumMemberName("omit")]
    Omit,

    /// <summary>
    /// The browser sends credentials only to the same origin.
    /// </summary>
    [JsonStringEnumMemberName("same-origin")]
    SameOrigin,

    /// <summary>
    /// The browser always sends credentials, also to another origin.
    /// </summary>
    [JsonStringEnumMemberName("include")]
    Include,
}
