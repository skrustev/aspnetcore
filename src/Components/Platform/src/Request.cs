// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a browser Fetch request.
/// </summary>
/// <remarks>
/// Create a request with <see cref="RequestConstructor.CreateAsync(string)"/>, reached from <c>Window.Request</c>.
/// The request holds a browser object until it is disposed.
/// </remarks>
public sealed class Request : IAsyncDisposable
{
    private readonly FetchFeature _feature;
    private readonly BodyMixin _body;

    internal Request(FetchFeature feature, IJSObjectReference reference)
    {
        _feature = feature;
        Handle = feature.Track(this, reference);
        _body = new BodyMixin(feature, Handle, FetchFeature.RequestBytes);
        Headers = new Headers(feature.JSRuntime, Handle);
    }

    internal JSObjectHandle Handle { get; }

    /// <summary>
    /// Gets the headers of the request.
    /// </summary>
    public Headers Headers { get; }

    /// <summary>
    /// Gets the HTTP method of the request.
    /// </summary>
    /// <returns>The HTTP method of the request.</returns>
    public ValueTask<string> GetMethodAsync() => Handle.Reference.GetValueAsync<string>("method");

    /// <summary>
    /// Gets the URL of the request, resolved against the document base URL.
    /// </summary>
    /// <returns>The URL of the request.</returns>
    public ValueTask<string> GetUrlAsync() => Handle.Reference.GetValueAsync<string>("url");

    /// <summary>
    /// Gets the referrer of the request.
    /// </summary>
    /// <returns>The referrer URL, <c>"about:client"</c>, or an empty string for no referrer.</returns>
    public ValueTask<string> GetReferrerAsync() => Handle.Reference.GetValueAsync<string>("referrer");

    /// <summary>
    /// Gets the referrer policy of the request.
    /// </summary>
    /// <returns>The referrer policy of the request.</returns>
    /// <exception cref="NotSupportedException">The browser returned a referrer policy that this version does not support.</exception>
    public async ValueTask<ReferrerPolicy> GetReferrerPolicyAsync()
        => FetchEnums.ParseReferrerPolicy(await Handle.Reference.GetValueAsync<string>("referrerPolicy").ConfigureAwait(false));

    /// <summary>
    /// Gets the mode of the request.
    /// </summary>
    /// <returns>The mode of the request.</returns>
    /// <exception cref="NotSupportedException">The browser returned a mode that this version does not support.</exception>
    public async ValueTask<RequestMode> GetModeAsync()
        => FetchEnums.ParseRequestMode(await Handle.Reference.GetValueAsync<string>("mode").ConfigureAwait(false));

    /// <summary>
    /// Gets when the browser sends credentials with the request.
    /// </summary>
    /// <returns>The credentials mode of the request.</returns>
    /// <exception cref="NotSupportedException">The browser returned a credentials mode that this version does not support.</exception>
    public async ValueTask<RequestCredentials> GetCredentialsAsync()
        => FetchEnums.ParseRequestCredentials(await Handle.Reference.GetValueAsync<string>("credentials").ConfigureAwait(false));

    /// <summary>
    /// Gets how the request uses the browser HTTP cache.
    /// </summary>
    /// <returns>The cache mode of the request.</returns>
    /// <exception cref="NotSupportedException">The browser returned a cache mode that this version does not support.</exception>
    public async ValueTask<RequestCache> GetCacheAsync()
        => FetchEnums.ParseRequestCache(await Handle.Reference.GetValueAsync<string>("cache").ConfigureAwait(false));

    /// <summary>
    /// Gets how the request handles a redirect.
    /// </summary>
    /// <returns>The redirect mode of the request.</returns>
    /// <exception cref="NotSupportedException">The browser returned a redirect mode that this version does not support.</exception>
    public async ValueTask<RequestRedirect> GetRedirectAsync()
        => FetchEnums.ParseRequestRedirect(await Handle.Reference.GetValueAsync<string>("redirect").ConfigureAwait(false));

    /// <summary>
    /// Gets the subresource integrity metadata of the request.
    /// </summary>
    /// <returns>The integrity metadata, or an empty string if the request has none.</returns>
    public ValueTask<string> GetIntegrityAsync() => Handle.Reference.GetValueAsync<string>("integrity");

    /// <summary>
    /// Gets a value that indicates whether the request can outlive the page.
    /// </summary>
    /// <returns><see langword="true"/> if the request can outlive the page; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> GetKeepaliveAsync() => Handle.Reference.GetValueAsync<bool>("keepalive");

    /// <summary>
    /// Creates a copy of the request, so that its body can be read twice. The body must not be used yet.
    /// </summary>
    /// <returns>The new request, which the caller disposes.</returns>
    public async ValueTask<Request> CloneAsync()
    {
        var reference = await Handle.Reference.InvokeAsync<IJSObjectReference>("clone").ConfigureAwait(false);

        return new Request(_feature, reference);
    }

    /// <summary>
    /// Gets a value that indicates whether the body was already read.
    /// </summary>
    /// <returns><see langword="true"/> if the body was read; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> GetBodyUsedAsync() => _body.GetBodyUsedAsync();

    /// <summary>
    /// Reads the body and copies it to .NET.
    /// </summary>
    /// <returns>The bytes of the body.</returns>
    /// <exception cref="IOException">The body is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public ValueTask<byte[]> ArrayBufferAsync() => _body.ArrayBufferAsync();

    /// <summary>
    /// Reads the body into a <see cref="Blob"/> that stays in the browser.
    /// </summary>
    /// <returns>The body as a <see cref="Blob"/>, which the caller disposes.</returns>
    public ValueTask<Blob> BlobAsync() => _body.BlobAsync();

    /// <summary>
    /// Reads the body and copies it to .NET.
    /// </summary>
    /// <returns>The bytes of the body.</returns>
    /// <exception cref="NotSupportedException">The browser does not provide <c>Request.bytes()</c> for the current page.</exception>
    /// <exception cref="IOException">The body is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public ValueTask<byte[]> BytesAsync() => _body.BytesAsync();

    /// <summary>
    /// Reads the body as form data that stays in the browser.
    /// </summary>
    /// <returns>The body as a <see cref="FormData"/>, which the caller disposes.</returns>
    [Experimental("ASP0040", UrlFormat = "https://aka.ms/aspnet/analyzer/{0}")]
    public ValueTask<FormData> FormDataAsync() => _body.FormDataAsync();

    /// <summary>
    /// Reads the body as UTF-8 and parses it as JSON.
    /// </summary>
    /// <returns>The parsed JSON value.</returns>
    /// <exception cref="JsonException">The body is not valid JSON.</exception>
    /// <exception cref="IOException">The body is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public ValueTask<JsonElement> JsonAsync() => _body.JsonAsync();

    /// <summary>
    /// Reads the body as UTF-8 text.
    /// </summary>
    /// <returns>The body as text.</returns>
    /// <exception cref="IOException">The body is larger than <see cref="FetchOptions.MaxBodySize"/>.</exception>
    public ValueTask<string> TextAsync() => _body.TextAsync();

    /// <summary>
    /// Releases the browser object of the request, together with its headers. A body read that is still running is not aborted.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask DisposeAsync() => Handle.DisposeAsync();
}
