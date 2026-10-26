// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Internal;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Creates browser <see cref="Request"/> objects, as the <c>Request</c> constructor does in JavaScript.
/// </summary>
public sealed class RequestConstructor
{
    private readonly FetchFeature _feature;

    internal RequestConstructor(FetchFeature feature)
    {
        _feature = feature;
    }

    /// <summary>
    /// Creates a request for the specified URL.
    /// </summary>
    /// <param name="input">The URL of the request, which can be relative to the document base URL.</param>
    /// <returns>The new request, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public async ValueTask<Request> CreateAsync(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var reference = await _feature.JSRuntime.InvokeConstructorAsync("Request", input).ConfigureAwait(false);

        return new Request(_feature, reference);
    }

    /// <summary>
    /// Creates a request for the specified URL with the specified options.
    /// </summary>
    /// <param name="input">The URL of the request, which can be relative to the document base URL.</param>
    /// <param name="init">The options of the request.</param>
    /// <returns>The new request, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="init"/> is <see langword="null"/>.</exception>
    [DynamicDependency(LinkerFlags.JsonSerialized, typeof(RequestInit))]
    public async ValueTask<Request> CreateAsync(string input, RequestInit init)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(init);

        var reference = await _feature.JSRuntime.InvokeConstructorAsync("Request", input, init).ConfigureAwait(false);

        return new Request(_feature, reference);
    }

    /// <summary>
    /// Creates a copy of the specified request.
    /// </summary>
    /// <param name="input">The request to copy.</param>
    /// <returns>The new request, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public async ValueTask<Request> CreateAsync(Request input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var reference = await _feature.JSRuntime.InvokeConstructorAsync("Request", input.Handle.Reference).ConfigureAwait(false);

        return new Request(_feature, reference);
    }

    /// <summary>
    /// Creates a copy of the specified request, with the specified options replacing its own.
    /// </summary>
    /// <param name="input">The request to copy.</param>
    /// <param name="init">The options that replace those of <paramref name="input"/>.</param>
    /// <returns>The new request, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="init"/> is <see langword="null"/>.</exception>
    [DynamicDependency(LinkerFlags.JsonSerialized, typeof(RequestInit))]
    public async ValueTask<Request> CreateAsync(Request input, RequestInit init)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(init);

        var reference = await _feature.JSRuntime.InvokeConstructorAsync("Request", input.Handle.Reference, init).ConfigureAwait(false);

        return new Request(_feature, reference);
    }
}
