// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents the options of a request. Every member is optional, and a member that is <see langword="null"/> is not sent,
/// so the browser uses its default.
/// </summary>
public sealed class RequestInit
{
    /// <summary>
    /// Gets or sets the HTTP method, or <see langword="null"/> to use the browser default (<c>GET</c>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Method { get; set; }

    /// <summary>
    /// Gets or sets the request headers as name and value pairs, or <see langword="null"/> to send no headers of the application.
    /// </summary>
    /// <remarks>
    /// The same name can appear more than once, and the browser combines the values. A <see cref="Dictionary{TKey, TValue}"/>
    /// can be assigned as well.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(HeadersInitJsonConverter))]
    public IEnumerable<KeyValuePair<string, string>>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the request body, or <see langword="null"/> for a request without a body.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BodyInit? Body { get; set; }

    /// <summary>
    /// Gets or sets the referrer: a URL of the same origin, <c>"about:client"</c>, or an empty string for no referrer.
    /// <see langword="null"/> uses the browser default (<c>"about:client"</c>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Referrer { get; set; }

    /// <summary>
    /// Gets or sets the referrer policy, or <see langword="null"/> to use the policy of the page.
    /// </summary>
    /// <remarks>
    /// Safari on iOS and other browsers on iOS do not support this option for <c>fetch</c>. The request then uses the default referrer policy.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Experimental("ASP0040", UrlFormat = "https://aka.ms/aspnet/analyzer/{0}")]
    public ReferrerPolicy? ReferrerPolicy { get; set; }

    /// <summary>
    /// Gets or sets the request mode, or <see langword="null"/> to use the browser default (<see cref="RequestMode.Cors"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestMode? Mode { get; set; }

    /// <summary>
    /// Gets or sets when the browser sends credentials, or <see langword="null"/> to use the browser default
    /// (<see cref="RequestCredentials.SameOrigin"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestCredentials? Credentials { get; set; }

    /// <summary>
    /// Gets or sets how the request uses the browser HTTP cache, or <see langword="null"/> to use the browser default
    /// (<see cref="RequestCache.Default"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestCache? Cache { get; set; }

    /// <summary>
    /// Gets or sets how the request handles a redirect, or <see langword="null"/> to use the browser default
    /// (<see cref="RequestRedirect.Follow"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestRedirect? Redirect { get; set; }

    /// <summary>
    /// Gets or sets the subresource integrity metadata that the response must match, or <see langword="null"/> for no check.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Integrity { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the request can outlive the page, or <see langword="null"/> to use the
    /// browser default (<see langword="false"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Keepalive { get; set; }

    /// <summary>
    /// Gets or sets the priority of the request, or <see langword="null"/> to use the browser default (<see cref="RequestPriority.Auto"/>).
    /// </summary>
    /// <remarks>
    /// A browser that does not support priorities ignores this setting.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestPriority? Priority { get; set; }

    // The AbortSignal of a CancellationToken. It is set only on a copy, so a RequestInit can be reused across calls.
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal IJSObjectReference? Signal { get; set; }

    internal RequestInit WithSignal(IJSObjectReference signal)
    {
        var copy = (RequestInit)MemberwiseClone();
        copy.Signal = signal;

        return copy;
    }
}
