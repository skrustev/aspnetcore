// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a browser Fetch response.
/// </summary>
/// <remarks>
/// A status that is not successful, such as 404, does not throw. <see cref="GetStatusAsync"/> and <see cref="GetOkAsync"/> report it.
/// The response holds a browser object until it is disposed.
/// </remarks>
public sealed class Response : IAsyncDisposable
{
    private readonly FetchFeature _feature;
    private readonly JSObjectHandle _handle;
    private readonly BodyMixin _body;
    private Task<ReadableStream?>? _bodyStreamTask;

    internal Response(FetchFeature feature, IJSObjectReference reference)
    {
        _feature = feature;
        _handle = feature.Track(this, reference);
        _body = new BodyMixin(feature, _handle, FetchFeature.ResponseBytes);
        Headers = new Headers(feature.JSRuntime, _handle);
    }

    /// <summary>
    /// Gets the headers of the response.
    /// </summary>
    public Headers Headers { get; }

    /// <summary>
    /// Gets the type of the response.
    /// </summary>
    /// <returns>The type of the response.</returns>
    /// <exception cref="NotSupportedException">The browser returned a type that this version does not support.</exception>
    public async ValueTask<ResponseType> GetTypeAsync()
        => FetchEnums.ParseResponseType(await _handle.Reference.GetValueAsync<string>("type").ConfigureAwait(false));

    /// <summary>
    /// Gets the URL of the response, after any redirects.
    /// </summary>
    /// <returns>The URL of the response, or an empty string for a response without a URL.</returns>
    public ValueTask<string> GetUrlAsync() => _handle.Reference.GetValueAsync<string>("url");

    /// <summary>
    /// Gets a value that indicates whether the response is the result of a redirect.
    /// </summary>
    /// <returns><see langword="true"/> if the browser followed a redirect; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> GetRedirectedAsync() => _handle.Reference.GetValueAsync<bool>("redirected");

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    /// <returns>The HTTP status code of the response, or 0 for an opaque response.</returns>
    public ValueTask<ushort> GetStatusAsync() => _handle.Reference.GetValueAsync<ushort>("status");

    /// <summary>
    /// Gets a value that indicates whether the status of the response is in the range 200 to 299.
    /// </summary>
    /// <returns><see langword="true"/> if the status is in the range 200 to 299; otherwise, <see langword="false"/>.</returns>
    public ValueTask<bool> GetOkAsync() => _handle.Reference.GetValueAsync<bool>("ok");

    /// <summary>
    /// Gets the status message of the response.
    /// </summary>
    /// <returns>The status message of the response, which is empty for HTTP/2 and later.</returns>
    public ValueTask<string> GetStatusTextAsync() => _handle.Reference.GetValueAsync<string>("statusText");

    /// <summary>
    /// Creates a copy of the response, so that its body can be read twice. The body must not be used yet.
    /// </summary>
    /// <returns>The new response, which the caller disposes.</returns>
    public async ValueTask<Response> CloneAsync()
    {
        var reference = await _handle.Reference.InvokeAsync<IJSObjectReference>("clone").ConfigureAwait(false);

        return new Response(_feature, reference);
    }

    /// <summary>
    /// Gets the body of the response as a stream that stays in the browser. Every call returns the same stream.
    /// </summary>
    /// <returns>The body of the response, or <see langword="null"/> if the response has no body.</returns>
    public ValueTask<ReadableStream?> GetBodyAsync()
    {
        _handle.ThrowIfDisposed();

        if (_bodyStreamTask is null || _bodyStreamTask.IsFaulted || _bodyStreamTask.IsCanceled)
        {
            _bodyStreamTask = AcquireBodyAsync();
        }

        return new(_bodyStreamTask);
    }

    private async Task<ReadableStream?> AcquireBodyAsync()
    {
        var reference = await _handle.Reference.GetValueAsync<IJSObjectReference?>("body").ConfigureAwait(false);

        return reference is null ? null : ReadableStream.Create(_feature, _handle, reference);
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
    /// <exception cref="NotSupportedException">The browser does not provide <c>Response.bytes()</c> for the current page.</exception>
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
    /// Releases the browser object of the response, together with its headers and body stream.
    /// A body read that is still running is not aborted.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask DisposeAsync() => _handle.DisposeAsync();
}
