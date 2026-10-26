// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

// The members of the Web IDL Body mixin, which Request and Response both include.
internal sealed class BodyMixin
{
    private readonly FetchFeature _feature;
    private readonly JSObjectHandle _handle;
    private readonly string _bytesMember;

    public BodyMixin(FetchFeature feature, JSObjectHandle handle, string bytesMember)
    {
        _feature = feature;
        _handle = handle;
        _bytesMember = bytesMember;
    }

    public ValueTask<bool> GetBodyUsedAsync() => _handle.Reference.GetValueAsync<bool>("bodyUsed");

    public ValueTask<byte[]> ArrayBufferAsync() => _feature.ReadBytesAsync(_handle.Reference, "arrayBuffer");

    public async ValueTask<Blob> BlobAsync()
    {
        var reference = await _handle.Reference.InvokeAsync<IJSObjectReference>("blob").ConfigureAwait(false);

        return new Blob(_feature, reference);
    }

    public ValueTask<byte[]> BytesAsync() => _feature.ReadBytesWithMemberCheckAsync(_handle.Reference, _bytesMember);

    public async ValueTask<FormData> FormDataAsync()
    {
        var reference = await _handle.Reference.InvokeAsync<IJSObjectReference>("formData").ConfigureAwait(false);

        return new FormData(_feature, reference);
    }

    public ValueTask<JsonElement> JsonAsync() => _feature.ReadJsonAsync(_handle.Reference);

    public ValueTask<string> TextAsync() => _feature.ReadTextAsync(_handle.Reference);
}
