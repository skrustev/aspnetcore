// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents the headers of a browser <see cref="Request"/> or <see cref="Response"/>.
/// </summary>
/// <remarks>
/// The headers belong to the request or response, and they become unusable when it is disposed.
/// The headers of a response from <c>fetch</c> cannot be changed, so the browser rejects a change to them.
/// </remarks>
public sealed class Headers
{
    private readonly IJSRuntime _jsRuntime;
    private readonly JSObjectHandle _owner;
    private readonly LazyJSObjectReference _reference;

    internal Headers(IJSRuntime jsRuntime, JSObjectHandle owner)
    {
        _jsRuntime = jsRuntime;
        _owner = owner;
        _reference = new LazyJSObjectReference(() => owner.Reference.GetValueAsync<IJSObjectReference>("headers"));
        owner.AddChild(_reference);
    }

    /// <summary>
    /// Appends a value to the header with the specified name, keeping its existing values.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The value to append.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public async ValueTask AppendAsync(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        var headers = await GetReferenceAsync().ConfigureAwait(false);
        await headers.InvokeVoidAsync("append", name, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the header with the specified name, if it exists.
    /// </summary>
    /// <param name="name">The name of the header to delete.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public async ValueTask DeleteAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var headers = await GetReferenceAsync().ConfigureAwait(false);
        await headers.InvokeVoidAsync("delete", name).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the values of the header with the specified name, combined into one string separated by commas.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <returns>The combined values of the header, or <see langword="null"/> if the header does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public async ValueTask<string?> GetAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var headers = await GetReferenceAsync().ConfigureAwait(false);

        return await headers.InvokeAsync<string?>("get", name).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether a header with the specified name exists.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <returns><see langword="true"/> if the header exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> HasAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var headers = await GetReferenceAsync().ConfigureAwait(false);

        return await headers.InvokeAsync<bool>("has", name).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the value of the header with the specified name, replacing all existing values for that name.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public async ValueTask SetAsync(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        var headers = await GetReferenceAsync().ConfigureAwait(false);
        await headers.InvokeVoidAsync("set", name, value).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all headers as name and value pairs, sorted by lowercase name, with the values of a name combined.
    /// </summary>
    /// <returns>The headers at the time of the call.</returns>
    public async ValueTask<IReadOnlyList<KeyValuePair<string, string>>> EntriesAsync()
    {
        var headers = await GetReferenceAsync().ConfigureAwait(false);
        var entries = await _jsRuntime.InvokeAsync<string[][]>("Array.from", headers).ConfigureAwait(false);

        return [.. entries.Select(static entry => KeyValuePair.Create(entry[0], entry[1]))];
    }

    /// <summary>
    /// Gets the names of all headers, sorted and in lowercase.
    /// </summary>
    /// <returns>The header names at the time of the call.</returns>
    public async ValueTask<IReadOnlyList<string>> KeysAsync()
    {
        var entries = await EntriesAsync().ConfigureAwait(false);

        return [.. entries.Select(static entry => entry.Key)];
    }

    /// <summary>
    /// Gets the values of all headers, in the order of <see cref="KeysAsync"/>.
    /// </summary>
    /// <returns>The header values at the time of the call.</returns>
    public async ValueTask<IReadOnlyList<string>> ValuesAsync()
    {
        var entries = await EntriesAsync().ConfigureAwait(false);

        return [.. entries.Select(static entry => entry.Value)];
    }

    private Task<IJSObjectReference> GetReferenceAsync()
    {
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, this);

        return _reference.GetAsync();
    }
}
