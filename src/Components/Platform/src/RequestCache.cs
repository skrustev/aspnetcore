// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Specifies how a request uses the browser HTTP cache.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RequestCache>))]
public enum RequestCache
{
    /// <summary>
    /// The browser uses the cache as HTTP caching rules describe.
    /// </summary>
    [JsonStringEnumMemberName("default")]
    Default,

    /// <summary>
    /// The browser neither reads from nor writes to the cache.
    /// </summary>
    [JsonStringEnumMemberName("no-store")]
    NoStore,

    /// <summary>
    /// The browser does not read from the cache, but writes the response to it.
    /// </summary>
    [JsonStringEnumMemberName("reload")]
    Reload,

    /// <summary>
    /// The browser revalidates a cached response with the server before it uses it.
    /// </summary>
    [JsonStringEnumMemberName("no-cache")]
    NoCache,

    /// <summary>
    /// The browser uses a cached response even if it is stale, and sends a request only if nothing is cached.
    /// </summary>
    [JsonStringEnumMemberName("force-cache")]
    ForceCache,

    /// <summary>
    /// The browser uses only a cached response and fails if nothing is cached. It requires <see cref="RequestMode.SameOrigin"/>.
    /// </summary>
    [JsonStringEnumMemberName("only-if-cached")]
    OnlyIfCached,
}
