// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
#pragma warning disable ASP0040 // FormDataAsync and RequestInit.ReferrerPolicy are experimental.

using System.Text.Json;
using Xunit;

namespace Microsoft.AspNetCore.Components.Platform.Tests;

// A mock IJSRuntime records arguments before serialization, so these tests check the JSON that the browser receives.
public class FetchJsonTest
{
    [Fact]
    public async Task FetchAsync_SendsEmptyOptionsAsAnEmptyObject()
    {
        var runtime = new SerializingJSRuntime();
        await using var provider = FetchTestServices.Create(runtime);

        await using var response = await provider.GetWindow().FetchAsync("/a", new RequestInit());

        Assert.Equal("""["/a",{}]""", runtime.ArgumentsSentTo("fetch"));
    }

    [Fact]
    public async Task FetchAsync_SendsEveryMemberUnderItsWebIdlName()
    {
        var runtime = new SerializingJSRuntime();
        await using var provider = FetchTestServices.Create(runtime);
        var init = new RequestInit
        {
            Method = "POST",
            Headers = [new("x-a", "1"), new("x-a", "2")],
            Body = "text",
            Referrer = "",
            ReferrerPolicy = ReferrerPolicy.NoReferrer,
            Mode = RequestMode.Cors,
            Credentials = RequestCredentials.Include,
            Cache = RequestCache.NoStore,
            Redirect = RequestRedirect.Manual,
            Integrity = "sha256-x",
            Keepalive = true,
            Priority = RequestPriority.High,
        };

        await using var response = await provider.GetWindow().FetchAsync("/a", init);

        Assert.Equal(
            """["/a",{"method":"POST","headers":[["x-a","1"],["x-a","2"]],"body":"text","referrer":"","referrerPolicy":"no-referrer","mode":"cors","credentials":"include","cache":"no-store","redirect":"manual","integrity":"sha256-x","keepalive":true,"priority":"high"}]""",
            runtime.ArgumentsSentTo("fetch"));
    }

    [Fact]
    public async Task FetchAsync_SendsADictionaryOfHeadersAsPairs()
    {
        var runtime = new SerializingJSRuntime();
        await using var provider = FetchTestServices.Create(runtime);

        await using var response = await provider.GetWindow().FetchAsync("/a", new RequestInit
        {
            Headers = new Dictionary<string, string> { ["accept"] = "application/json" },
        });

        Assert.Equal("""["/a",{"headers":[["accept","application/json"]]}]""", runtime.ArgumentsSentTo("fetch"));
    }

    [Fact]
    public void EnumValues_AreSentAsTheirWebIdlStrings()
    {
        var runtime = new SerializingJSRuntime();
        var expected = new (object Value, string Json)[]
        {
            (RequestMode.Navigate, "navigate"), (RequestMode.SameOrigin, "same-origin"), (RequestMode.NoCors, "no-cors"), (RequestMode.Cors, "cors"),
            (RequestCredentials.Omit, "omit"), (RequestCredentials.SameOrigin, "same-origin"), (RequestCredentials.Include, "include"),
            (RequestCache.Default, "default"), (RequestCache.NoStore, "no-store"), (RequestCache.Reload, "reload"),
            (RequestCache.NoCache, "no-cache"), (RequestCache.ForceCache, "force-cache"), (RequestCache.OnlyIfCached, "only-if-cached"),
            (RequestRedirect.Follow, "follow"), (RequestRedirect.Error, "error"), (RequestRedirect.Manual, "manual"),
            (RequestPriority.High, "high"), (RequestPriority.Low, "low"), (RequestPriority.Auto, "auto"),
            (ReferrerPolicy.Empty, ""), (ReferrerPolicy.NoReferrer, "no-referrer"), (ReferrerPolicy.NoReferrerWhenDowngrade, "no-referrer-when-downgrade"),
            (ReferrerPolicy.SameOrigin, "same-origin"), (ReferrerPolicy.Origin, "origin"), (ReferrerPolicy.StrictOrigin, "strict-origin"),
            (ReferrerPolicy.OriginWhenCrossOrigin, "origin-when-cross-origin"),
            (ReferrerPolicy.StrictOriginWhenCrossOrigin, "strict-origin-when-cross-origin"), (ReferrerPolicy.UnsafeUrl, "unsafe-url"),
        };

        Assert.All(expected, item => Assert.Equal($"\"{item.Json}\"", runtime.Serialize(item.Value)));
        Assert.Equal(
            Enum.GetValues<RequestMode>().Length + Enum.GetValues<RequestCredentials>().Length + Enum.GetValues<RequestCache>().Length
                + Enum.GetValues<RequestRedirect>().Length + Enum.GetValues<RequestPriority>().Length + Enum.GetValues<ReferrerPolicy>().Length,
            expected.Length);
    }

    [Fact]
    public async Task FetchAsync_SendsABytesBodyAsABinaryTransfer()
    {
        var runtime = new SerializingJSRuntime();
        await using var provider = FetchTestServices.Create(runtime);

        await using var response = await provider.GetWindow().FetchAsync("/a", new RequestInit { Body = new byte[] { 1, 2 } });

        Assert.Equal("""["/a",{"body":{"__byte[]":1}}]""", runtime.ArgumentsSentTo("fetch"));
        Assert.Equal(new byte[] { 1, 2 }, Assert.Single(runtime.SentByteArrays));
    }

    [Fact]
    public async Task FetchAsync_SendsABlobOrFormDataBodyAsItsBrowserObjectReference()
    {
        var runtime = new SerializingJSRuntime();
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        await using var source = await window.FetchAsync("/source");
        await using var blob = await source.BlobAsync();
        await using var form = await source.FormDataAsync();

        await using var withBlob = await window.FetchAsync("/blob", new RequestInit { Body = blob });
        var blobJson = runtime.ArgumentsSentTo("fetch");
        await using var withForm = await window.FetchAsync("/form", new RequestInit { Body = form });
        var formJson = runtime.ArgumentsSentTo("fetch");

        // The source response is object 1, its blob is object 2, and its form data is object 3.
        Assert.Equal("""["/blob",{"body":{"__jsObjectId":2}}]""", blobJson);
        Assert.Equal("""["/form",{"body":{"__jsObjectId":3}}]""", formJson);
    }

    [Fact]
    public async Task FetchAsync_SendsTheAbortSignalOnlyWithATokenThatCanBeCanceled()
    {
        var runtime = new SerializingJSRuntime();
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        using var cts = new CancellationTokenSource();
        var init = new RequestInit { Method = "GET" };

        await using var withToken = await window.FetchAsync("/a", init, cts.Token);
        var withTokenJson = runtime.ArgumentsSentTo("fetch");
        await using var withoutToken = await window.FetchAsync("/a", init);
        var withoutTokenJson = runtime.ArgumentsSentTo("fetch");

        // The AbortController is object 1 and its signal is object 2. The caller's RequestInit keeps no signal.
        Assert.Equal("""["/a",{"method":"GET","signal":{"__jsObjectId":2}}]""", withTokenJson);
        Assert.Equal("""["/a",{"method":"GET"}]""", withoutTokenJson);
    }
}
