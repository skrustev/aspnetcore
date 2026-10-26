// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents browser <c>FormData</c>: a list of named entries whose values are strings or files.
/// </summary>
/// <remarks>
/// The form data holds a browser object until it is disposed.
/// </remarks>
[JsonConverter(typeof(FormDataJsonConverter))]
public sealed class FormData : IAsyncDisposable
{
    private readonly FetchFeature _feature;

    internal FormData(FetchFeature feature, IJSObjectReference reference)
    {
        _feature = feature;
        Handle = feature.Track(this, reference);
    }

    internal JSObjectHandle Handle { get; }

    /// <summary>
    /// Appends an entry with a string value, keeping the existing entries with the same name.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <param name="value">The value of the entry.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public ValueTask AppendAsync(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        return Handle.Reference.InvokeVoidAsync("append", name, value);
    }

    /// <summary>
    /// Appends an entry with a blob value, keeping the existing entries with the same name.
    /// The browser stores the blob as a file named <c>"blob"</c>.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <param name="blobValue">The value of the entry.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="blobValue"/> is <see langword="null"/>.</exception>
    public ValueTask AppendAsync(string name, Blob blobValue)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(blobValue);

        return Handle.Reference.InvokeVoidAsync("append", name, blobValue.Handle.Reference);
    }

    /// <summary>
    /// Appends an entry with a blob value stored as a file with the specified name, keeping the existing entries with the same name.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <param name="blobValue">The value of the entry.</param>
    /// <param name="filename">The file name of the value.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="blobValue"/>, or <paramref name="filename"/> is <see langword="null"/>.</exception>
    public ValueTask AppendAsync(string name, Blob blobValue, string filename)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(blobValue);
        ArgumentNullException.ThrowIfNull(filename);

        return Handle.Reference.InvokeVoidAsync("append", name, blobValue.Handle.Reference, filename);
    }

    /// <summary>
    /// Deletes all entries with the specified name.
    /// </summary>
    /// <param name="name">The name of the entries to delete.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public ValueTask DeleteAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Handle.Reference.InvokeVoidAsync("delete", name);
    }

    /// <summary>
    /// Gets the value of the first entry with the specified name.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <returns>The value of the first entry, or <see langword="null"/> if no entry has that name. A <see cref="Platform.File"/> value is disposed by the caller.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public async ValueTask<FormDataEntryValue?> GetAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var formData = Handle.Reference;
        var value = await formData.InvokeAsync<JsonElement>("get", name).ConfigureAwait(false);

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => new FormDataEntryValue(value.GetString()!),

            // A File serializes as an empty object, so its reference needs a second call.
            _ => new FormDataEntryValue(new File(_feature, await formData.InvokeAsync<IJSObjectReference>("get", name).ConfigureAwait(false))),
        };
    }

    /// <summary>
    /// Gets the values of all entries with the specified name.
    /// </summary>
    /// <param name="name">The name of the entries.</param>
    /// <returns>The values of the entries, in order. Each <see cref="Platform.File"/> value is disposed by the caller.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public async ValueTask<IReadOnlyList<FormDataEntryValue>> GetAllAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var formData = Handle.Reference;
        var values = await formData.InvokeAsync<JsonElement>("getAll", name).ConfigureAwait(false);

        return await ToEntryValuesAsync(
            values,
            () => formData.InvokeAsync<IJSObjectReference>("getAll", name),
            static index => index.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether an entry with the specified name exists.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <returns><see langword="true"/> if an entry has that name; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public ValueTask<bool> HasAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Handle.Reference.InvokeAsync<bool>("has", name);
    }

    /// <summary>
    /// Sets an entry with a string value, replacing all existing entries with the same name.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <param name="value">The value of the entry.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public ValueTask SetAsync(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        return Handle.Reference.InvokeVoidAsync("set", name, value);
    }

    /// <summary>
    /// Sets an entry with a blob value, replacing all existing entries with the same name.
    /// The browser stores the blob as a file named <c>"blob"</c>.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <param name="blobValue">The value of the entry.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="blobValue"/> is <see langword="null"/>.</exception>
    public ValueTask SetAsync(string name, Blob blobValue)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(blobValue);

        return Handle.Reference.InvokeVoidAsync("set", name, blobValue.Handle.Reference);
    }

    /// <summary>
    /// Sets an entry with a blob value stored as a file with the specified name, replacing all existing entries with the same name.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <param name="blobValue">The value of the entry.</param>
    /// <param name="filename">The file name of the value.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="blobValue"/>, or <paramref name="filename"/> is <see langword="null"/>.</exception>
    public ValueTask SetAsync(string name, Blob blobValue, string filename)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(blobValue);
        ArgumentNullException.ThrowIfNull(filename);

        return Handle.Reference.InvokeVoidAsync("set", name, blobValue.Handle.Reference, filename);
    }

    /// <summary>
    /// Gets all entries as name and value pairs, in order.
    /// </summary>
    /// <returns>The entries at the time of the call. Each <see cref="Platform.File"/> value is disposed by the caller.</returns>
    public async ValueTask<IReadOnlyList<KeyValuePair<string, FormDataEntryValue>>> EntriesAsync()
    {
        var formData = Handle.Reference;
        var entries = await _feature.JSRuntime.InvokeAsync<JsonElement>("Array.from", formData).ConfigureAwait(false);

        var names = new List<string>(entries.GetArrayLength());
        var values = new List<JsonElement>(names.Capacity);
        foreach (var entry in entries.EnumerateArray())
        {
            names.Add(entry[0].GetString()!);
            values.Add(entry[1]);
        }

        var entryValues = await ToEntryValuesAsync(
            values,
            () => _feature.JSRuntime.InvokeAsync<IJSObjectReference>("Array.from", formData),
            static index => string.Create(CultureInfo.InvariantCulture, $"{index}.1")).ConfigureAwait(false);

        var result = new KeyValuePair<string, FormDataEntryValue>[names.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = KeyValuePair.Create(names[i], entryValues[i]);
        }

        return result;
    }

    /// <summary>
    /// Gets the names of all entries, in order.
    /// </summary>
    /// <returns>The entry names at the time of the call.</returns>
    public async ValueTask<IReadOnlyList<string>> KeysAsync()
    {
        var entries = await _feature.JSRuntime.InvokeAsync<JsonElement>("Array.from", Handle.Reference).ConfigureAwait(false);

        var names = new List<string>(entries.GetArrayLength());
        foreach (var entry in entries.EnumerateArray())
        {
            names.Add(entry[0].GetString()!);
        }

        return names;
    }

    /// <summary>
    /// Gets the values of all entries, in order.
    /// </summary>
    /// <returns>The entry values at the time of the call. Each <see cref="Platform.File"/> value is disposed by the caller.</returns>
    public async ValueTask<IReadOnlyList<FormDataEntryValue>> ValuesAsync()
    {
        var entries = await EntriesAsync().ConfigureAwait(false);
        var values = new FormDataEntryValue[entries.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = entries[i].Value;
        }

        return values;
    }

    /// <summary>
    /// Releases the browser object of the form data. The files that were read from it are released separately.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask DisposeAsync() => Handle.DisposeAsync();

    private ValueTask<FormDataEntryValue[]> ToEntryValuesAsync(JsonElement values, Func<ValueTask<IJSObjectReference>> getArray, Func<int, string> filePath)
        => ToEntryValuesAsync([.. values.EnumerateArray()], getArray, filePath);

    // A string arrives as JSON. A File serializes as an empty object, so it is read by its path in the array.
    private async ValueTask<FormDataEntryValue[]> ToEntryValuesAsync(List<JsonElement> values, Func<ValueTask<IJSObjectReference>> getArray, Func<int, string> filePath)
    {
        var result = new FormDataEntryValue[values.Count];
        IJSObjectReference? array = null;
        try
        {
            for (var i = 0; i < result.Length; i++)
            {
                if (values[i].ValueKind == JsonValueKind.String)
                {
                    result[i] = new FormDataEntryValue(values[i].GetString()!);
                    continue;
                }

                array ??= await getArray().ConfigureAwait(false);
                var file = await array.GetValueAsync<IJSObjectReference>(filePath(i)).ConfigureAwait(false);
                result[i] = new FormDataEntryValue(new File(_feature, file));
            }
        }
        finally
        {
            if (array is not null)
            {
                await JSObjectHandle.DisposeQuietlyAsync(array).ConfigureAwait(false);
            }
        }

        return result;
    }
}
