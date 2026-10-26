// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

// Maps the enum strings that the browser returns. A switch keeps reading free of reflection, and an unknown string,
// such as a value that a later specification adds, throws instead of mapping to a wrong member.
internal static class FetchEnums
{
    public static ResponseType ParseResponseType(string value) => value switch
    {
        "basic" => ResponseType.Basic,
        "cors" => ResponseType.Cors,
        "default" => ResponseType.Default,
        "error" => ResponseType.Error,
        "opaque" => ResponseType.Opaque,
        "opaqueredirect" => ResponseType.OpaqueRedirect,
        _ => throw Unsupported("type", value),
    };

    public static RequestMode ParseRequestMode(string value) => value switch
    {
        "navigate" => RequestMode.Navigate,
        "same-origin" => RequestMode.SameOrigin,
        "no-cors" => RequestMode.NoCors,
        "cors" => RequestMode.Cors,
        _ => throw Unsupported("mode", value),
    };

    public static RequestCredentials ParseRequestCredentials(string value) => value switch
    {
        "omit" => RequestCredentials.Omit,
        "same-origin" => RequestCredentials.SameOrigin,
        "include" => RequestCredentials.Include,
        _ => throw Unsupported("credentials", value),
    };

    public static RequestCache ParseRequestCache(string value) => value switch
    {
        "default" => RequestCache.Default,
        "no-store" => RequestCache.NoStore,
        "reload" => RequestCache.Reload,
        "no-cache" => RequestCache.NoCache,
        "force-cache" => RequestCache.ForceCache,
        "only-if-cached" => RequestCache.OnlyIfCached,
        _ => throw Unsupported("cache", value),
    };

    public static RequestRedirect ParseRequestRedirect(string value) => value switch
    {
        "follow" => RequestRedirect.Follow,
        "error" => RequestRedirect.Error,
        "manual" => RequestRedirect.Manual,
        _ => throw Unsupported("redirect", value),
    };

    public static ReferrerPolicy ParseReferrerPolicy(string value) => value switch
    {
        "" => ReferrerPolicy.Empty,
        "no-referrer" => ReferrerPolicy.NoReferrer,
        "no-referrer-when-downgrade" => ReferrerPolicy.NoReferrerWhenDowngrade,
        "same-origin" => ReferrerPolicy.SameOrigin,
        "origin" => ReferrerPolicy.Origin,
        "strict-origin" => ReferrerPolicy.StrictOrigin,
        "origin-when-cross-origin" => ReferrerPolicy.OriginWhenCrossOrigin,
        "strict-origin-when-cross-origin" => ReferrerPolicy.StrictOriginWhenCrossOrigin,
        "unsafe-url" => ReferrerPolicy.UnsafeUrl,
        _ => throw Unsupported("referrerPolicy", value),
    };

    private static NotSupportedException Unsupported(string attribute, string value)
        => new($"The browser returned the {attribute} '{value}', which this version does not support.");
}
