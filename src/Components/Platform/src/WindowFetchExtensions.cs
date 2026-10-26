// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Provides access to the Fetch API from a <see cref="Window"/>.
/// </summary>
public static class WindowFetchExtensions
{
    extension(Window window)
    {
        /// <summary>
        /// Sends a request for the specified URL from the browser. The request uses the cookies, cache, and CORS rules of the browser.
        /// </summary>
        /// <param name="input">The URL of the request, which can be relative to the document base URL.</param>
        /// <param name="cancellationToken">A token that aborts the request in the browser until the response headers arrive.</param>
        /// <returns>The response, which the caller disposes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A status that is not successful, such as 404, does not throw. The token does not abort body reads after the call returns.
        /// </remarks>
        public ValueTask<Response> FetchAsync(string input, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);

            return GetFetchFeature(window).FetchAsync(input, init: null, cancellationToken);
        }

        /// <summary>
        /// Sends a request for the specified URL with the specified options from the browser. The request uses the cookies,
        /// cache, and CORS rules of the browser.
        /// </summary>
        /// <param name="input">The URL of the request, which can be relative to the document base URL.</param>
        /// <param name="init">The options of the request.</param>
        /// <param name="cancellationToken">A token that aborts the request in the browser until the response headers arrive.</param>
        /// <returns>The response, which the caller disposes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="init"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A status that is not successful, such as 404, does not throw. The token does not abort body reads after the call returns.
        /// </remarks>
        public ValueTask<Response> FetchAsync(string input, RequestInit init, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(init);

            return GetFetchFeature(window).FetchAsync(input, init, cancellationToken);
        }

        /// <summary>
        /// Sends the specified request from the browser. The request uses the cookies, cache, and CORS rules of the browser.
        /// </summary>
        /// <param name="input">The request to send.</param>
        /// <param name="cancellationToken">A token that aborts the request in the browser until the response headers arrive.</param>
        /// <returns>The response, which the caller disposes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A status that is not successful, such as 404, does not throw. The token does not abort body reads after the call returns.
        /// </remarks>
        public ValueTask<Response> FetchAsync(Request input, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);

            return GetFetchFeature(window).FetchAsync(input.Handle.Reference, init: null, cancellationToken);
        }

        /// <summary>
        /// Sends the specified request from the browser, with the specified options replacing its own. The request uses the
        /// cookies, cache, and CORS rules of the browser.
        /// </summary>
        /// <param name="input">The request to send.</param>
        /// <param name="init">The options that replace those of <paramref name="input"/>.</param>
        /// <param name="cancellationToken">A token that aborts the request in the browser until the response headers arrive.</param>
        /// <returns>The response, which the caller disposes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="init"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A status that is not successful, such as 404, does not throw. The token does not abort body reads after the call returns.
        /// </remarks>
        public ValueTask<Response> FetchAsync(Request input, RequestInit init, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(init);

            return GetFetchFeature(window).FetchAsync(input.Handle.Reference, init, cancellationToken);
        }

        /// <summary>
        /// Gets the constructor of browser <see cref="Platform.Request"/> objects.
        /// </summary>
        public RequestConstructor Request
            => window.GetOrAddFeature(_ => new RequestConstructor(GetFetchFeature(window)));
    }

    private static FetchFeature GetFetchFeature(Window window)
        => window.GetOrAddFeature(jsRuntime => new FetchFeature(
            jsRuntime,
            window.Services?.GetService<IOptions<FetchOptions>>()?.Value ?? new FetchOptions()));
}
