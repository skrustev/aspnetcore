// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
#pragma warning disable ASP0040 // FormDataAsync and RequestInit.ReferrerPolicy are experimental.

using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Xunit;

namespace Microsoft.AspNetCore.Components.Platform.Tests;

public class FetchTest
{
    [Fact]
    public async Task FetchAsync_SendsInputAndOptions_WithoutTokenOrAbortController()
    {
        var (runtime, _) = CreateFetch();
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        var init = new RequestInit { Method = "POST" };

        await using var first = await window.FetchAsync("/a");
        await using var second = await window.FetchAsync("/b", init);
        await using var third = await window.FetchAsync("/c", CancellationToken.None);

        // Any token, even CancellationToken.None, would turn off the default JS interop timeout.
        Assert.Collection(
            runtime.CallsTo("fetch"),
            call => AssertCall(call, ["/a"], canBeCanceled: false),
            call => AssertCall(call, ["/b", init], canBeCanceled: false),
            call => AssertCall(call, ["/c"], canBeCanceled: false));
        Assert.Empty(runtime.CallsTo("AbortController"));
    }

    [Fact]
    public async Task FetchAsync_SendsTheReferenceOfARequest()
    {
        var (runtime, _) = CreateFetch();
        var requestObject = NewRequestObject("request");
        runtime.Member("Request", _ => requestObject);
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        var init = new RequestInit { Method = "HEAD" };

        await using var request = await window.Request.CreateAsync("/health");
        await using var first = await window.FetchAsync(request);
        await using var second = await window.FetchAsync(request, init);

        Assert.Collection(
            runtime.CallsTo("fetch"),
            call => Assert.Equal(new object?[] { requestObject }, call.Arguments),
            call => Assert.Equal(new object?[] { requestObject, init }, call.Arguments));
    }

    [Fact]
    public async Task FetchAsync_RejectsNullArguments()
    {
        var (runtime, _) = CreateFetch();
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();

        await Assert.ThrowsAsync<ArgumentNullException>("input", () => window.FetchAsync(default(string)!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>("init", () => window.FetchAsync("/a", default(RequestInit)!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>("input", () => window.FetchAsync(default(Request)!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>("input", () => window.Request.CreateAsync(default(string)!).AsTask());
        Assert.Empty(runtime.Calls);
    }

    [Fact]
    public async Task Response_ReadsItsAttributes()
    {
        var (runtime, responseObject) = CreateFetch();
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        Assert.Equal(ResponseType.Cors, await response.GetTypeAsync());
        Assert.Equal("https://example.test/data", await response.GetUrlAsync());
        Assert.True(await response.GetRedirectedAsync());
        Assert.Equal((ushort)404, await response.GetStatusAsync());
        Assert.False(await response.GetOkAsync());
        Assert.Equal("Not Found", await response.GetStatusTextAsync());
        Assert.False(await response.GetBodyUsedAsync());
    }

    [Theory]
    [InlineData("basic", ResponseType.Basic)]
    [InlineData("cors", ResponseType.Cors)]
    [InlineData("default", ResponseType.Default)]
    [InlineData("error", ResponseType.Error)]
    [InlineData("opaque", ResponseType.Opaque)]
    [InlineData("opaqueredirect", ResponseType.OpaqueRedirect)]
    public async Task Response_GetTypeAsync_MapsEachWebIdlString(string value, ResponseType expected)
    {
        var (runtime, responseObject) = CreateFetch();
        responseObject.Value("type", value);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        Assert.Equal(expected, await response.GetTypeAsync());
    }

    [Fact]
    public async Task Request_ReadsItsAttributesAndMapsEachEnum()
    {
        var (runtime, _) = CreateFetch();
        var requestObject = NewRequestObject("request")
            .Value("method", "PUT")
            .Value("url", "https://example.test/r")
            .Value("referrer", "about:client")
            .Value("referrerPolicy", "strict-origin-when-cross-origin")
            .Value("mode", "same-origin")
            .Value("credentials", "include")
            .Value("cache", "only-if-cached")
            .Value("redirect", "manual")
            .Value("integrity", "sha256-x")
            .Value("keepalive", true);
        runtime.Member("Request", _ => requestObject);
        await using var provider = FetchTestServices.Create(runtime);
        await using var request = await provider.GetWindow().Request.CreateAsync("/r");

        Assert.Equal("PUT", await request.GetMethodAsync());
        Assert.Equal("https://example.test/r", await request.GetUrlAsync());
        Assert.Equal("about:client", await request.GetReferrerAsync());
        Assert.Equal(ReferrerPolicy.StrictOriginWhenCrossOrigin, await request.GetReferrerPolicyAsync());
        Assert.Equal(RequestMode.SameOrigin, await request.GetModeAsync());
        Assert.Equal(RequestCredentials.Include, await request.GetCredentialsAsync());
        Assert.Equal(RequestCache.OnlyIfCached, await request.GetCacheAsync());
        Assert.Equal(RequestRedirect.Manual, await request.GetRedirectAsync());
        Assert.Equal("sha256-x", await request.GetIntegrityAsync());
        Assert.True(await request.GetKeepaliveAsync());
    }

    [Fact]
    public async Task EnumGetters_ThrowNotSupportedExceptionForAStringTheEnumDoesNotHave()
    {
        var (runtime, responseObject) = CreateFetch();
        responseObject.Value("type", "partial-test");
        var requestObject = NewRequestObject("request")
            .Value("referrerPolicy", "x-policy")
            .Value("mode", "x-mode")
            .Value("credentials", "x-credentials")
            .Value("cache", "x-cache")
            .Value("redirect", "x-redirect");
        runtime.Member("Request", _ => requestObject);
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        await using var response = await window.FetchAsync("/data");
        await using var request = await window.Request.CreateAsync("/r");

        Assert.Contains("'partial-test'", (await Assert.ThrowsAsync<NotSupportedException>(() => response.GetTypeAsync().AsTask())).Message);
        Assert.Contains("'x-policy'", (await Assert.ThrowsAsync<NotSupportedException>(() => request.GetReferrerPolicyAsync().AsTask())).Message);
        Assert.Contains("'x-mode'", (await Assert.ThrowsAsync<NotSupportedException>(() => request.GetModeAsync().AsTask())).Message);
        Assert.Contains("'x-credentials'", (await Assert.ThrowsAsync<NotSupportedException>(() => request.GetCredentialsAsync().AsTask())).Message);
        Assert.Contains("'x-cache'", (await Assert.ThrowsAsync<NotSupportedException>(() => request.GetCacheAsync().AsTask())).Message);
        Assert.Contains("'x-redirect'", (await Assert.ThrowsAsync<NotSupportedException>(() => request.GetRedirectAsync().AsTask())).Message);
    }

    [Fact]
    public async Task RequestConstructor_PassesInputAndOptions()
    {
        var (runtime, _) = CreateFetch();
        var created = new List<FakeJSObject>();
        runtime.Member("Request", _ =>
        {
            var requestObject = NewRequestObject($"request {created.Count}");
            created.Add(requestObject);

            return requestObject;
        });
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        var init = new RequestInit { Method = "POST" };

        Assert.Same(window.Request, window.Request);
        await using var first = await window.Request.CreateAsync("/a");
        await using var second = await window.Request.CreateAsync("/b", init);
        await using var third = await window.Request.CreateAsync(first);
        await using var fourth = await window.Request.CreateAsync(first, init);

        Assert.All(runtime.CallsTo("Request"), call => Assert.Equal("new", call.Kind));
        Assert.Collection(
            runtime.CallsTo("Request"),
            call => Assert.Equal(new object?[] { "/a" }, call.Arguments),
            call => Assert.Equal(new object?[] { "/b", init }, call.Arguments),
            call => Assert.Equal(new object?[] { created[0] }, call.Arguments),
            call => Assert.Equal(new object?[] { created[0], init }, call.Arguments));
    }

    [Fact]
    public async Task Headers_AreStableAndAcquiredOnceOnFirstUse()
    {
        var (runtime, responseObject) = CreateFetch();
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        Assert.Same(response.Headers, response.Headers);
        Assert.Empty(responseObject.CallsTo("headers"));

        Assert.Equal("text/plain", await response.Headers.GetAsync("content-type"));
        Assert.True(await response.Headers.HasAsync("content-type"));
        await response.Headers.SetAsync("x-a", "1");
        await response.Headers.AppendAsync("x-a", "2");
        await response.Headers.DeleteAsync("x-a");

        Assert.Single(responseObject.CallsTo("headers"));
        var headersObject = HeadersOf(responseObject);
        Assert.Collection(
            headersObject.Calls,
            call => AssertInvocation(call, "get", "content-type"),
            call => AssertInvocation(call, "has", "content-type"),
            call => AssertInvocation(call, "set", "x-a", "1"),
            call => AssertInvocation(call, "append", "x-a", "2"),
            call => AssertInvocation(call, "delete", "x-a"));
    }

    [Fact]
    public async Task Headers_SharePendingAcquisition_AndAcquireAgainAfterAFailure()
    {
        var (runtime, responseObject) = CreateFetch();
        var headersObject = HeadersOf(responseObject);
        var pending = new TaskCompletionSource<object?>();
        var attempts = 0;
        responseObject.Member("headers", _ => ++attempts switch
        {
            1 => Task.FromCanceled<object?>(new CancellationToken(canceled: true)),
            2 => pending.Task,
            _ => throw new InvalidOperationException("Acquired more than twice."),
        });
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response.Headers.GetAsync("a").AsTask());
        var first = response.Headers.GetAsync("a").AsTask();
        var second = response.Headers.HasAsync("a").AsTask();
        pending.SetResult(headersObject);
        await Task.WhenAll(first, second);

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Headers_EntriesKeysAndValues_ReadASnapshotWithArrayFrom()
    {
        var (runtime, responseObject) = CreateFetch();
        runtime.Member("Array.from", _ => new[] { new[] { "accept", "a, b" }, new[] { "x-id", "1" } });
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        var entries = await response.Headers.EntriesAsync();
        var keys = await response.Headers.KeysAsync();
        var values = await response.Headers.ValuesAsync();

        Assert.Equal(new[] { KeyValuePair.Create("accept", "a, b"), KeyValuePair.Create("x-id", "1") }, entries);
        Assert.Equal(new[] { "accept", "x-id" }, keys);
        Assert.Equal(new[] { "a, b", "1" }, values);
        Assert.All(runtime.CallsTo("Array.from"), call => Assert.Equal(new object?[] { HeadersOf(responseObject) }, call.Arguments));
    }

    [Fact]
    public async Task ArrayBufferAsync_ReadsTheBodyAsAStreamWithTheDefaultLimit()
    {
        var (runtime, responseObject) = CreateFetch();
        var stream = new FakeJSStream([1, 2, 3]);
        responseObject.Member("arrayBuffer", _ => stream);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        Assert.Equal(new byte[] { 1, 2, 3 }, await response.ArrayBufferAsync());
        Assert.Equal(512_000, stream.MaxAllowedSize);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task BodyMethods_UseTheConfiguredLimit_AndThrowIOExceptionForALargerBody()
    {
        var (runtime, responseObject) = CreateFetch();
        responseObject.Member("arrayBuffer", _ => new FakeJSStream(new byte[5]));
        await using var provider = FetchTestServices.Create(runtime, options => options.MaxBodySize = 4);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        var exception = await Assert.ThrowsAsync<IOException>(() => response.ArrayBufferAsync().AsTask());
        await Assert.ThrowsAsync<IOException>(() => response.TextAsync().AsTask());
        await Assert.ThrowsAsync<IOException>(() => response.JsonAsync().AsTask());

        Assert.Contains("5", exception.Message);
        Assert.Contains("4", exception.Message);
    }

    [Fact]
    public void FetchOptions_HasADefaultLimitAndRejectsAValueBelowOne()
    {
        var options = new FetchOptions();

        Assert.Equal(512_000, options.MaxBodySize);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxBodySize = 0);
        options.MaxBodySize = long.MaxValue;
        Assert.Equal(long.MaxValue, options.MaxBodySize);
    }

    [Fact]
    public async Task TextAsync_DecodesUtf8AndRemovesAByteOrderMark()
    {
        var (runtime, responseObject) = CreateFetch();
        responseObject.Member("arrayBuffer", _ => new FakeJSStream([.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes("héllo")]));
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        Assert.Equal("héllo", await response.TextAsync());
        Assert.Empty(responseObject.CallsTo("text"));
    }

    [Theory]
    [InlineData("""{"id":1,"tags":["a"]}""", JsonValueKind.Object)]
    [InlineData("[1,2]", JsonValueKind.Array)]
    [InlineData("\"text\"", JsonValueKind.String)]
    [InlineData("12.5", JsonValueKind.Number)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("false", JsonValueKind.False)]
    [InlineData("null", JsonValueKind.Null)]
    public async Task JsonAsync_ParsesEachJsonValueInDotNet(string json, JsonValueKind expected)
    {
        var (runtime, responseObject) = CreateFetch(json);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        var value = await response.JsonAsync();

        Assert.Equal(expected, value.ValueKind);
        Assert.Equal(json, value.GetRawText());
        Assert.Empty(responseObject.CallsTo("json"));
    }

    [Fact]
    public async Task JsonAsync_ThrowsJsonExceptionForInvalidJson()
    {
        var (runtime, _) = CreateFetch("{not json");
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        await Assert.ThrowsAnyAsync<JsonException>(() => response.JsonAsync().AsTask());
    }

    [Fact]
    public async Task JsonAsync_KeepsLargeNumbers_RemovesAByteOrderMark_AndMatchesTheLastDuplicateKey()
    {
        var (runtime, responseObject) = CreateFetch();
        responseObject.Member("arrayBuffer", _ => new FakeJSStream([.. Encoding.UTF8.Preamble, .. """{"big":9007199254740993,"a":1,"a":2}"""u8.ToArray()]));
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        var value = await response.JsonAsync();

        // The browser would round 9007199254740993 to 9007199254740992.
        Assert.Equal("9007199254740993", value.GetProperty("big").GetRawText());
        Assert.Equal(2, value.GetProperty("a").GetInt32());
    }

    [Fact]
    public async Task BytesAsync_ChecksForBytesOncePerPrototypeAndScope()
    {
        var (runtime, responseObject) = CreateFetch("ab");
        var function = new FakeJSObject("bytes function");
        runtime.Member("Response.prototype.bytes", _ => function);
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        await using var response = await window.FetchAsync("/data");
        await using var clone = await response.CloneAsync();

        Assert.Equal("ab"u8.ToArray(), await response.BytesAsync());
        Assert.Equal("ab"u8.ToArray(), await clone.BytesAsync());

        Assert.Single(runtime.CallsTo("Response.prototype.bytes"));
        Assert.True(function.Disposed);
    }

    [Fact]
    public async Task BytesAsync_ThrowsNotSupportedExceptionWithoutCallingBytes_WhenTheBrowserLacksIt()
    {
        var (runtime, responseObject) = CreateFetch();
        var missing = new JSException("The property 'Response.prototype.bytes' is not defined or is not readable.");
        runtime.Member("Response.prototype.bytes", _ => throw missing);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => response.BytesAsync().AsTask());

        Assert.Same(missing, exception.InnerException);
        Assert.Empty(responseObject.CallsTo("bytes"));
    }

    [Fact]
    public async Task BytesAsync_TreatsANullFunctionAsMissing_AndChecksAgainAfterACanceledCheck()
    {
        var (runtime, responseObject) = CreateFetch();
        var attempts = 0;
        runtime.Member("Response.prototype.bytes", _ => ++attempts == 1 ? Task.FromCanceled<object?>(new CancellationToken(canceled: true)) : null);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response.BytesAsync().AsTask());
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => response.BytesAsync().AsTask());

        Assert.Null(exception.InnerException);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task BytesAsync_PassesABrowserErrorFromBytesThroughUnchanged()
    {
        var (runtime, responseObject) = CreateFetch();
        var bodyUsed = new JSException("TypeError: body stream already read");
        runtime.Member("Response.prototype.bytes", _ => new FakeJSObject("bytes function"));
        responseObject.Member("bytes", _ => throw bodyUsed);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");

        Assert.Same(bodyUsed, await Assert.ThrowsAsync<JSException>(() => response.BytesAsync().AsTask()));
    }

    [Fact]
    public async Task BytesAsync_ChecksThePrototypeOfEachType()
    {
        var (runtime, responseObject) = CreateFetch("ab");
        var requestObject = NewRequestObject("request").Member("bytes", _ => new FakeJSStream("ab"u8.ToArray()));
        var blobObject = NewBlobObject("blob", "ab"u8.ToArray());
        runtime.Member("Request", _ => requestObject);
        responseObject.Member("blob", _ => blobObject);
        foreach (var member in new[] { "Response.prototype.bytes", "Request.prototype.bytes", "Blob.prototype.bytes" })
        {
            runtime.Member(member, _ => new FakeJSObject(member));
        }

        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        await using var response = await window.FetchAsync("/data");
        await using var request = await window.Request.CreateAsync("/r");
        await using var blob = await response.BlobAsync();

        await request.BytesAsync();
        await blob.BytesAsync();

        Assert.Equal(
            new[] { "Request.prototype.bytes", "Blob.prototype.bytes" },
            runtime.Calls.Where(call => call.Identifier.EndsWith(".prototype.bytes", StringComparison.Ordinal)).Select(call => call.Identifier));
    }

    [Fact]
    public async Task Response_DisposeAsync_ReleasesTheResponseAndItsHeaders_AndRejectsLaterCalls()
    {
        var (runtime, responseObject) = CreateFetch();
        await using var provider = FetchTestServices.Create(runtime);
        var response = await provider.GetWindow().FetchAsync("/data");
        await response.Headers.GetAsync("a");

        await response.DisposeAsync();
        await response.DisposeAsync();

        Assert.Equal(1, responseObject.DisposeCount);
        Assert.Equal(1, HeadersOf(responseObject).DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => response.GetStatusAsync().AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => response.TextAsync().AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => response.Headers.GetAsync("a").AsTask());
    }

    [Fact]
    public async Task DisposeAsync_NeverThrows_WhenTheCircuitIsGone()
    {
        var (runtime, responseObject) = CreateFetch();
        responseObject.DisposeException = new JSDisconnectedException("The circuit is gone.");
        await using var provider = FetchTestServices.Create(runtime);
        var response = await provider.GetWindow().FetchAsync("/data");

        await response.DisposeAsync();

        Assert.Equal(1, responseObject.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotAbortABodyReadThatIsRunning()
    {
        var (runtime, responseObject) = CreateFetch();
        var pending = new TaskCompletionSource<object?>();
        responseObject.Member("arrayBuffer", _ => pending.Task);
        await using var provider = FetchTestServices.Create(runtime);
        var response = await provider.GetWindow().FetchAsync("/data");

        var read = response.ArrayBufferAsync().AsTask();
        await response.DisposeAsync();
        pending.SetResult(new FakeJSStream([7]));

        Assert.Equal(new byte[] { 7 }, await read);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => response.ArrayBufferAsync().AsTask());
    }

    [Fact]
    public async Task ScopeDisposal_ReleasesObjectsThatTheApplicationDidNotDispose()
    {
        var (runtime, responseObject) = CreateFetch();
        var blobObject = NewBlobObject("blob", []);
        responseObject.Member("blob", _ => blobObject);
        var otherObject = NewResponseObject("other", []);
        var responses = new Queue<FakeJSObject>([responseObject, otherObject]);
        runtime.Member("fetch", _ => responses.Dequeue());
        var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        var response = await window.FetchAsync("/data");
        var blob = await response.BlobAsync();
        var disposedEarly = await window.FetchAsync("/other");
        await disposedEarly.DisposeAsync();

        await provider.DisposeAsync();

        Assert.Equal(1, responseObject.DisposeCount);
        Assert.Equal(1, blobObject.DisposeCount);
        Assert.Equal(1, otherObject.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => blob.GetSizeAsync().AsTask());
    }

    [Fact]
    public async Task ScopeDisposal_DoesNotWaitForAPendingHeadersAcquisition()
    {
        var (runtime, responseObject) = CreateFetch();
        var pending = new TaskCompletionSource<object?>();
        responseObject.Member("headers", _ => pending.Task);
        var provider = FetchTestServices.Create(runtime);
        var response = await provider.GetWindow().FetchAsync("/data");
        var read = response.Headers.GetAsync("a").AsTask();

        // Disposal finishes although the acquisition never completes.
        await provider.DisposeAsync();

        Assert.True(responseObject.Disposed);
        Assert.False(read.IsCompleted);
    }

    [Fact]
    public async Task FetchAsync_WithAToken_SendsTheSignalOfANewAbortControllerOnACopyOfTheOptions()
    {
        var (runtime, _) = CreateFetch();
        var (controller, signal) = AddAbortController(runtime);
        await using var provider = FetchTestServices.Create(runtime);
        using var cts = new CancellationTokenSource();
        var init = new RequestInit { Method = "GET" };

        await using var response = await provider.GetWindow().FetchAsync("/a", init, cts.Token);

        var fetch = Assert.Single(runtime.CallsTo("fetch"));
        Assert.Equal(cts.Token, fetch.CancellationToken);
        Assert.Equal("/a", fetch.Arguments[0]);
        var sent = Assert.IsType<RequestInit>(fetch.Arguments[1]);
        Assert.NotSame(init, sent);
        Assert.Equal("GET", sent.Method);
        Assert.True(controller.Disposed);
        Assert.True(signal.Disposed);
    }

    [Fact]
    public async Task FetchAsync_CancelingTheToken_AbortsTheRequestAndThrowsOperationCanceledException()
    {
        var (runtime, responseObject) = CreateFetch();
        var (controller, _) = AddAbortController(runtime);
        var pending = new TaskCompletionSource<object?>();
        runtime.Member("fetch", _ => pending.Task);
        await using var provider = FetchTestServices.Create(runtime);
        using var cts = new CancellationTokenSource();

        var fetch = provider.GetWindow().FetchAsync("/a", cts.Token).AsTask();
        cts.Cancel();
        pending.SetException(new JSException("AbortError: The operation was aborted."));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch);
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.Single(controller.CallsTo("abort"));
    }

    [Fact]
    public async Task FetchAsync_CancelingTheTokenAfterTheCall_DoesNotAbortBodyReads()
    {
        var (runtime, _) = CreateFetch();
        var (controller, _) = AddAbortController(runtime);
        await using var provider = FetchTestServices.Create(runtime);
        using var cts = new CancellationTokenSource();

        await using var response = await provider.GetWindow().FetchAsync("/a", cts.Token);
        cts.Cancel();

        Assert.Empty(controller.CallsTo("abort"));
    }

    [Fact]
    public async Task Blob_ReadsItsAttributesAndSlices()
    {
        var (runtime, responseObject) = CreateFetch();
        var blobObject = NewBlobObject("blob", [1, 2, 3, 4]).Value("size", 4UL).Value("type", "image/png");
        var slices = new Queue<FakeJSObject>([NewBlobObject("s1", []), NewBlobObject("s2", []), NewBlobObject("s3", []), NewBlobObject("s4", [])]);
        blobObject.Member("slice", _ => slices.Dequeue());
        responseObject.Member("blob", _ => blobObject);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");
        await using var blob = await response.BlobAsync();

        Assert.Equal(4UL, await blob.GetSizeAsync());
        Assert.Equal("image/png", await blob.GetTypeAsync());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await blob.ArrayBufferAsync());
        await using var s1 = await blob.SliceAsync();
        await using var s2 = await blob.SliceAsync(1);
        await using var s3 = await blob.SliceAsync(1, -1);
        await using var s4 = await blob.SliceAsync(0, 2, "text/plain");

        Assert.Collection(
            blobObject.CallsTo("slice"),
            call => Assert.Empty(call.Arguments),
            call => Assert.Equal(new object?[] { 1L }, call.Arguments),
            call => Assert.Equal(new object?[] { 1L, -1L }, call.Arguments),
            call => Assert.Equal(new object?[] { 0L, 2L, "text/plain" }, call.Arguments));
    }

    [Fact]
    public async Task FormData_GetAsync_ReturnsAStringAFileOrNull()
    {
        var (runtime, responseObject) = CreateFetch();
        var fileObject = NewBlobObject("file", []).Value("name", "a.txt").Value("lastModified", 42L);
        var formObject = new FakeJSObject("form")
            .Member("get", args => args[0] switch
            {
                "title" => JsonSerializer.SerializeToElement("Hello"),
                "upload" when FormCallCount("get", "upload") == 1 => JsonSerializer.SerializeToElement(new { }),
                "upload" => fileObject,
                _ => JsonSerializer.SerializeToElement<object?>(null),
            });
        responseObject.Member("formData", _ => formObject);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");
        await using var form = await response.FormDataAsync();

        var title = await form.GetAsync("title");
        var upload = await form.GetAsync("upload");
        var missing = await form.GetAsync("missing");

        Assert.Equal("Hello", Assert.IsType<string>(title!.Value.Value));
        var file = Assert.IsType<File>(upload!.Value.Value);
        Assert.Equal("a.txt", await file.GetNameAsync());
        Assert.Equal(42L, await file.GetLastModifiedAsync());
        Assert.Null(missing);

        int FormCallCount(string identifier, string name) => formObject.CallsTo(identifier).Count(call => (string?)call.Arguments[0] == name);
    }

    [Fact]
    public async Task FormData_GetAllAndEntries_ReadFilesByTheirPathInTheArray()
    {
        var (runtime, responseObject) = CreateFetch();
        var file = NewBlobObject("file", []).Value("name", "a.txt");
        var allArray = new FakeJSObject("getAll array").Value("1", file);
        var entriesArray = new FakeJSObject("entries array").Value("1.1", file);
        var getAllCalls = 0;
        var formObject = new FakeJSObject("form")
            .Member("getAll", _ => ++getAllCalls == 1 ? JsonSerializer.SerializeToElement(new object[] { "x", new { } }) : allArray);
        var arrayFromCalls = 0;
        runtime.Member("Array.from", _ => ++arrayFromCalls % 2 == 1
            ? JsonSerializer.SerializeToElement(new object[] { new object[] { "a", "x" }, new object[] { "b", new { } } })
            : entriesArray);
        responseObject.Member("formData", _ => formObject);
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");
        await using var form = await response.FormDataAsync();

        var all = await form.GetAllAsync("a");
        var entries = await form.EntriesAsync();
        var keys = await form.KeysAsync();

        Assert.Collection(all, value => Assert.Equal("x", value.Value), value => Assert.IsType<File>(value.Value));
        Assert.Collection(
            entries,
            entry => Assert.Equal(("a", (object?)"x"), (entry.Key, entry.Value.Value)),
            entry => Assert.Equal("b", entry.Key));
        Assert.IsType<File>(entries[1].Value.Value);
        Assert.Equal(new[] { "a", "b" }, keys);
        Assert.True(allArray.Disposed);
        Assert.True(entriesArray.Disposed);
    }

    [Fact]
    public async Task GetBodyAsync_ReturnsTheSameStream_OrNullForNoBody()
    {
        var (runtime, responseObject) = CreateFetch();
        var bodyObject = new FakeJSObject("body").Value("locked", false);
        responseObject.Value("body", bodyObject);
        var noBodyObject = NewResponseObject("no body", []).Value("body", null);
        var responses = new Queue<FakeJSObject>([responseObject, noBodyObject]);
        runtime.Member("fetch", _ => responses.Dequeue());
        await using var provider = FetchTestServices.Create(runtime);
        var window = provider.GetWindow();
        var response = await window.FetchAsync("/data");
        await using var noBody = await window.FetchAsync("/empty");

        var body = await response.GetBodyAsync();

        Assert.Same(body, await response.GetBodyAsync());
        Assert.False(await body!.GetLockedAsync());
        Assert.Null(await noBody.GetBodyAsync());
        Assert.Single(responseObject.CallsTo("body"));

        await response.DisposeAsync();
        Assert.True(bodyObject.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => body.GetLockedAsync().AsTask());
    }

    [Fact]
    public async Task Reader_ReadsChunksAsStreams_AndReportsTheEnd()
    {
        var (runtime, responseObject) = CreateFetch();
        var chunks = new Queue<FakeJSObject>([
            new FakeJSObject("chunk 1").Value("done", false).Value("value", new FakeJSStream([1, 2])),
            new FakeJSObject("chunk 2").Value("done", true),
        ]);
        var readerObject = new FakeJSObject("reader").Member("read", _ => chunks.Dequeue());
        responseObject.Value("body", new FakeJSObject("body").Value("getReader", readerObject));
        await using var provider = FetchTestServices.Create(runtime);
        await using var response = await provider.GetWindow().FetchAsync("/data");
        var reader = await (await response.GetBodyAsync())!.GetReaderAsync();

        var first = await reader.ReadAsync();
        var second = await reader.ReadAsync();

        Assert.Equal(new byte[] { 1, 2 }, first.Value);
        Assert.False(first.Done);
        Assert.Null(second.Value);
        Assert.True(second.Done);
    }

    [Fact]
    public async Task Reader_ThrowsIOExceptionForAChunkLargerThanTheLimit()
    {
        var (runtime, responseObject) = CreateFetch();
        var readerObject = new FakeJSObject("reader")
            .Member("read", _ => new FakeJSObject("chunk").Value("done", false).Value("value", new FakeJSStream(new byte[3])));
        responseObject.Value("body", new FakeJSObject("body").Value("getReader", readerObject));
        await using var provider = FetchTestServices.Create(runtime, options => options.MaxBodySize = 2);
        await using var response = await provider.GetWindow().FetchAsync("/data");
        var reader = await (await response.GetBodyAsync())!.GetReaderAsync();

        await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync().AsTask());
    }

    private static (FakeJSRuntime Runtime, FakeJSObject Response) CreateFetch(string body = "")
    {
        var response = NewResponseObject("response", Encoding.UTF8.GetBytes(body));
        var runtime = new FakeJSRuntime().Member("fetch", _ => response);

        return (runtime, response);
    }

    private static FakeJSObject NewResponseObject(string name, byte[] body)
        => new FakeJSObject(name)
            .Value("type", "cors")
            .Value("url", "https://example.test/data")
            .Value("redirected", true)
            .Value("status", (ushort)404)
            .Value("ok", false)
            .Value("statusText", "Not Found")
            .Value("bodyUsed", false)
            .Value("headers", NewHeadersObject(name + " headers"))
            .Member("arrayBuffer", _ => new FakeJSStream(body))
            .Member("bytes", _ => new FakeJSStream(body))
            .Member("clone", _ => NewResponseObject(name + " clone", body));

    private static FakeJSObject NewRequestObject(string name)
        => new FakeJSObject(name).Value("headers", NewHeadersObject(name + " headers"));

    private static FakeJSObject NewBlobObject(string name, byte[] data)
        => new FakeJSObject(name)
            .Member("arrayBuffer", _ => new FakeJSStream(data))
            .Member("bytes", _ => new FakeJSStream(data));

    private static FakeJSObject NewHeadersObject(string name)
        => new FakeJSObject(name)
            .Member("get", args => args[0] is "content-type" ? "text/plain" : null)
            .Value("has", true)
            .Value("set", null)
            .Value("append", null)
            .Value("delete", null);

    private static FakeJSObject HeadersOf(FakeJSObject owner) => (FakeJSObject)owner.Members["headers"]([])!;

    private static (FakeJSObject Controller, FakeJSObject Signal) AddAbortController(FakeJSRuntime runtime)
    {
        var signal = new FakeJSObject("signal");
        var controller = new FakeJSObject("controller").Value("signal", signal).Value("abort", null);
        runtime.Member("AbortController", _ => controller);

        return (controller, signal);
    }

    private static void AssertInvocation(FakeCall call, string identifier, params object?[] arguments)
    {
        Assert.Equal(identifier, call.Identifier);
        Assert.Equal(arguments, call.Arguments);
    }

    private static void AssertCall(FakeCall call, object?[] arguments, bool canBeCanceled)
    {
        Assert.Equal(arguments, call.Arguments);
        Assert.Equal(canBeCanceled, call.CancellationToken.CanBeCanceled);
    }
}
