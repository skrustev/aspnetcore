// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Specifies how much referrer information the browser sends with a request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReferrerPolicy>))]
public enum ReferrerPolicy
{
    /// <summary>
    /// No policy is set, so the browser uses the policy of the page.
    /// </summary>
    [JsonStringEnumMemberName("")]
    Empty,

    /// <summary>
    /// The browser sends no referrer.
    /// </summary>
    [JsonStringEnumMemberName("no-referrer")]
    NoReferrer,

    /// <summary>
    /// The browser sends the full URL, except from HTTPS to HTTP.
    /// </summary>
    [JsonStringEnumMemberName("no-referrer-when-downgrade")]
    NoReferrerWhenDowngrade,

    /// <summary>
    /// The browser sends the full URL to the same origin only.
    /// </summary>
    [JsonStringEnumMemberName("same-origin")]
    SameOrigin,

    /// <summary>
    /// The browser sends only the origin.
    /// </summary>
    [JsonStringEnumMemberName("origin")]
    Origin,

    /// <summary>
    /// The browser sends only the origin, except from HTTPS to HTTP.
    /// </summary>
    [JsonStringEnumMemberName("strict-origin")]
    StrictOrigin,

    /// <summary>
    /// The browser sends the full URL to the same origin, and only the origin to another origin.
    /// </summary>
    [JsonStringEnumMemberName("origin-when-cross-origin")]
    OriginWhenCrossOrigin,

    /// <summary>
    /// The browser sends the full URL to the same origin, only the origin to another origin, and nothing from HTTPS to HTTP.
    /// </summary>
    [JsonStringEnumMemberName("strict-origin-when-cross-origin")]
    StrictOriginWhenCrossOrigin,

    /// <summary>
    /// The browser always sends the full URL.
    /// </summary>
    [JsonStringEnumMemberName("unsafe-url")]
    UnsafeUrl,
}
