// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

// Writes HeadersInit as an array of [name, value] pairs, the sequence form of the Web IDL union.
internal sealed class HeadersInitJsonConverter : JsonConverter<IEnumerable<KeyValuePair<string, string>>>
{
    public override IEnumerable<KeyValuePair<string, string>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, string>> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var (name, headerValue) in value)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(name);
            writer.WriteStringValue(headerValue);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }
}

// Writes a projected object as its browser object reference, which JS interop revives in the arguments.
internal abstract class JSObjectWrapperJsonConverter<T> : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // The JS interop options convert IJSObjectReference to {"__jsObjectId":id}.
        var typeInfo = (JsonTypeInfo<IJSObjectReference>)options.GetTypeInfo(typeof(IJSObjectReference));
        JsonSerializer.Serialize(writer, GetReference(value), typeInfo);
    }

    protected abstract IJSObjectReference GetReference(T value);
}

internal sealed class BlobJsonConverter : JSObjectWrapperJsonConverter<Blob>
{
    protected override IJSObjectReference GetReference(Blob value) => value.Handle.Reference;
}

internal sealed class FormDataJsonConverter : JSObjectWrapperJsonConverter<FormData>
{
    protected override IJSObjectReference GetReference(FormData value) => value.Handle.Reference;
}
