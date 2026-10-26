// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Specifies how a request handles a redirect.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RequestRedirect>))]
public enum RequestRedirect
{
    /// <summary>
    /// The browser follows redirects.
    /// </summary>
    [JsonStringEnumMemberName("follow")]
    Follow,

    /// <summary>
    /// The browser fails the request when the server redirects it.
    /// </summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>
    /// The browser does not follow a redirect and returns a response of type <see cref="ResponseType.OpaqueRedirect"/>.
    /// </summary>
    [JsonStringEnumMemberName("manual")]
    Manual,
}
