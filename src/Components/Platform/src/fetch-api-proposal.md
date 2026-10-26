# [API Proposal] Add the Fetch API to the typed browser platform APIs for Blazor

> **Status: design draft.** This is the design step of the `port-components-platform-api` skill. Nothing is implemented. Answer the [Open Questions](#open-questions) before implementation starts, because several of them change the shape below. After agreement, file this with the `api-review` skill.
>
> **Revised 2026-10-22** against `main` after the Cookie Store API merged (#69845, commit `9a57dad569`). Changes from the first draft:
> - The answers in #69844 now apply as package rules. These are `ASP0040`, `NotSupportedException` for missing objects and missing members, and "capability checks ship as experimental".
> - The first draft's questions about `formData()` and missing members are settled by those rules.
> - New [Implementation](#implementation) and [Verification](#verification) sections.

## Background and Motivation

`Microsoft.AspNetCore.Components.Platform` (#69304, implemented in #69305) gives Blazor applications typed access to standard browser APIs. It has two families on `main`: Web Storage (#69305) and the Cookie Store API (#69844, implemented in #69845). This proposal adds a third family, the [Fetch Standard](https://fetch.spec.whatwg.org/). It covers `fetch()`, `Request`, `Response`, `Headers`, and the `Body` mixin methods `arrayBuffer()`, `blob()`, `bytes()`, `formData()`, `json()`, and `text()`. It also covers redirects, credentials, cache modes, aborting, and streaming response bodies.

An application calls the projected `fetch()` operation and then calls the same body methods that the browser `Response` object has:

```csharp
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await using Response response = await Browser.Window.FetchAsync(
    "/api/orders",
    new RequestInit { Method = "GET" },
    timeout.Token);

await using Blob body = await response.BlobAsync();
```

**Why this matters when .NET already has `HttpClient`.** In Interactive Server, `HttpClient` runs on the server. `Window.FetchAsync(...)` runs in the user's browser. So the request uses:

- the user's origin, with relative URLs resolved against the document base URL
- the cookies and credentials that the browser manages, including `HttpOnly` cookies that application code cannot read
- the browser HTTP cache and its `cache` modes
- CORS enforcement and the semantics of `mode` and `credentials`
- an active service worker
- the network path of the client, such as a corporate proxy, a VPN, or an intranet host that the server cannot reach
- the browser security context

In WebAssembly, `HttpClient` already goes through `fetch` (`BrowserHttpHandler`), but it maps the result to `HttpResponseMessage`. That mapping hides the response `type` (`opaque`, `opaqueredirect`), `redirect: "manual"`, `Blob` results that stay in the browser, and per-request `cache` and `priority`. A projection works the same way in both render modes.

The package rules apply:

- HTTP status is data. A 404 or 500 response completes normally, and `GetStatusAsync()` and `GetOkAsync()` report it.
- Network errors and other browser exceptions keep browser semantics. A rejected `fetch()` (`TypeError`) surfaces as an exception, and the public contract does not document `JSException`.
- A `CancellationToken` maps to an `AbortController` in the browser, so cancellation really stops the request.
- `JsonAsync()` returns the parsed JavaScript value without a .NET type. Typed deserialization is future work for an extension layer above the projection.
- As answered in #69844, a member that an older browser lacks throws `NotSupportedException`. A member that is not Baseline ships with `[Experimental("ASP0040")]`.

In the priority order of the package design document, `window` members come first (https://github.com/dotnet/aspnetcore/pull/69305#issuecomment-6084476924). `fetch` is a `window` member that every supported browser has, so it needs no capability check for the family as a whole.

## Scope

The entry point is `fetch()` on `WindowOrWorkerGlobalScope`, which the skill places on `Window`. The table lists everything the family reaches from there. The Web IDL comes from current webref (`fetch.idl`, `FileAPI.idl`, `xhr.idl`, `streams.idl`, `dom.idl`, `url.idl`, `referrer-policy.idl`). Baseline status comes from `web-features` and browser-compat-data as of 2026-10-21.

| Web IDL | Decision | Reason |
| --- | --- | --- |
| `fetch(RequestInfo, optional RequestInit = {})` | Implement | Entry point. Baseline widely available. |
| `Response`: `type`, `url`, `redirected`, `status`, `ok`, `statusText`, `headers`, `clone()` | Implement | Baseline widely available. |
| `Response` constructor, `Response.error()`, `Response.redirect()`, `Response.json()` | Defer | They create responses. Pages rarely need that. Service workers and the Cache API do. When they are added, they follow the same constructor pattern as `Request`. |
| `Body` on `Response`: `body`, `bodyUsed`, `arrayBuffer()`, `blob()`, `text()`, `json()` | Implement | Baseline widely available. |
| `Body.bytes()` | Implement | Baseline newly available (2025-01: Chrome 132, Firefox 128, Safari 18). In an older browser, it throws `NotSupportedException`, per #69844. |
| `Body.formData()` | Implement with `[Experimental("ASP0040")]` | **Not Baseline.** Chrome's support is partial: it throws `TypeError` for empty form data (crbug 40065051). The #69844 rule ships such members now with `ASP0040`. |
| `Body.textStream()` | Exclude | No browser ships it. |
| `Request`: `method`, `url`, `headers`, `referrer`, `referrerPolicy`, `mode`, `credentials`, `cache`, `redirect`, `integrity`, `clone()`, and the `Body` read methods | Implement | Baseline widely available. |
| `Request.keepalive` | Implement | Baseline newly available (2024-11). |
| `Request` constructor | Implement as `Window.Request.CreateAsync(...)` | Without it, an application cannot get a `Request` at all. |
| `Request.body`, `Request.duplex`, `RequestInit.duplex`, `ReadableStream` as a request `BodyInit` | Defer | **Not Baseline.** Upload streaming works only in Chromium. Feeding it from .NET also needs callbacks from JavaScript to .NET, which the skill defers. |
| `Request.signal`, `RequestInit.signal`, `AbortController`, `AbortSignal` | Covered by `CancellationToken` | The skill maps an `AbortSignal` parameter to a `CancellationToken`. The implementation creates the `AbortController` in the browser, so these types are not public. `AbortSignal` is an `EventTarget` with an `abort` event, which the skill defers anyway. |
| `Request.destination`, `isReloadNavigation`, `isHistoryNavigation` | Exclude | Only navigation requests in a service worker have meaningful values. A `Request` created on a page always reports `""` and `false`. `isHistoryNavigation` is not Baseline either. `RequestMode.Navigate` stays in the enum, because `GetModeAsync()` can return it. |
| `RequestInit`: `method`, `headers`, `body`, `referrer`, `referrerPolicy`, `mode`, `credentials`, `cache`, `redirect`, `integrity`, `keepalive` | Implement. `referrerPolicy` gets `[Experimental("ASP0040")]` | Baseline, except `referrerPolicy`. The #69844 rule ships such members now with `ASP0040`, and the XML doc of `RequestInit.ReferrerPolicy` notes the Safari on iOS gap (see [Documentation](#documentation)). `Request.GetReferrerPolicyAsync()` reads the attribute, which is Baseline (Safari on iOS 10.3), so it is not experimental. `web-features` lists the `referrerPolicy` option of `fetch()` as not Baseline, because browser-compat-data shows no support in Safari on iOS or in iOS WebViews. Chrome 52, Firefox 52, and desktop Safari 11.1 support it. Every browser on iOS uses WebKit, so the gap covers all of them. |
| `RequestInit.priority`, `RequestPriority` | Implement | Baseline newly available (2024-10). An older browser ignores the member without an error. |
| `RequestInit.window` | Exclude | Its only valid value is `null`, which a member left out when `null` cannot send. A page has no use for it. |
| `Headers`: `append`, `delete`, `get`, `has`, `set`, `iterable<ByteString, ByteString>` | Implement | Baseline widely available. |
| `Headers.getSetCookie()` | Defer, together with the `Headers` constructor | The browser filters `Set-Cookie` out of every response from `fetch`, so it would always return an empty list on `Response.Headers`. It is useful only on `Headers` that the application builds, so it is added with the constructor. Baseline widely available since 2023-09. |
| `Headers` constructor | Defer | `RequestInit.Headers` takes plain name and value pairs, so `fetch` does not need it. It can be added later as `Window.Headers.CreateAsync(...)`, together with `getSetCookie()`. |
| `fetchLater()`, `DeferredRequestInit`, `FetchLaterResult` | Exclude | Chrome only (135). Firefox and Safari have none. |
| `Blob` (File API): `size`, `type`, `slice()`, `stream()`, `text()`, `arrayBuffer()` | Implement, as a companion family | `blob()` returns it. Baseline widely available. |
| `Blob.bytes()` | Implement | Baseline newly available (2026-01). In an older browser, it throws `NotSupportedException`. |
| `Blob.textStream()` | Exclude | No browser ships it. |
| `Blob` constructor, `BlobPropertyBag`, `EndingType` | Defer | `BodyInit` takes `byte[]` and `string` directly, so `fetch` does not need it. It belongs to the File API slice, as `Window.Blob.CreateAsync(...)`. |
| `File` (File API): `name`, `lastModified` | Implement, as a companion family | A `FormDataEntryValue` can be a `File`. |
| `FormData` (XHR): `append`, `delete`, `get`, `getAll`, `has`, `set`, `iterable` | Implement, as a companion family | `formData()` returns it, and it is a `BodyInit`. |
| `FormData` constructor (`HTMLFormElement`) | Defer | It is DOM, which is out of scope for this package. A constructor without arguments can be added later as `Window.FormData.CreateAsync()`. |
| `URLSearchParams` as `BodyInit` | Defer | It is a separate family (URL Standard). `BodyInit` can gain a conversion when that family ships. A `string` body with an explicit `Content-Type` covers the need until then. |
| `ReadableStream` (Streams): `locked`, `cancel()`, `getReader()` (default reader), `ReadableStreamDefaultReader.read()`, `releaseLock()`, `cancel()` | Implement, as a companion family | Streaming response bodies. Baseline widely available. |
| `ReadableStream` constructor, `from()`, `pipeTo()`, `pipeThrough()`, `tee()`, async iteration, BYOB reader, `closed` | Defer | They need callbacks from JavaScript to .NET, `WritableStream`, or a design for promise-valued attributes. `from()` is not Baseline. A BYOB reader on `Response.body` became Baseline only in 2026-03. |
| `cancel(any reason)` and `abort(any reason)` reasons | Defer | The package has no mapping for `any` input yet. `JsonElement` covers only the result of `json()` (see the design notes). |

## Proposed API

All types are in the package namespace. Every live browser access returns `ValueTask` or `ValueTask<T>`. The XML doc forms are listed under [Documentation](#documentation).

### Fetch

```diff
 namespace Microsoft.AspNetCore.Components.Platform;

+public static class WindowFetchExtensions
+{
+    extension(Window window)
+    {
+        public ValueTask<Response> FetchAsync(string input, CancellationToken cancellationToken = default);
+        public ValueTask<Response> FetchAsync(string input, RequestInit init, CancellationToken cancellationToken = default);
+        public ValueTask<Response> FetchAsync(Request input, CancellationToken cancellationToken = default);
+        public ValueTask<Response> FetchAsync(Request input, RequestInit init, CancellationToken cancellationToken = default);
+
+        // The Request constructor, as window.Request is in JavaScript
+        public RequestConstructor Request { get; }
+    }
+}
+
+public sealed class RequestConstructor
+{
+    public ValueTask<Request> CreateAsync(string input);
+    public ValueTask<Request> CreateAsync(string input, RequestInit init);
+    public ValueTask<Request> CreateAsync(Request input);
+    public ValueTask<Request> CreateAsync(Request input, RequestInit init);
+}
+
+public sealed class FetchOptions
+{
+    // Largest body, in bytes, that a body method transfers to .NET. The default is 512,000.
+    public long MaxBodySize { get; set; }
+}
+
+public sealed class Response : IAsyncDisposable
+{
+    public Headers Headers { get; }
+
+    public ValueTask<ResponseType> GetTypeAsync();
+    public ValueTask<string> GetUrlAsync();
+    public ValueTask<bool> GetRedirectedAsync();
+    public ValueTask<ushort> GetStatusAsync();
+    public ValueTask<bool> GetOkAsync();
+    public ValueTask<string> GetStatusTextAsync();
+    public ValueTask<Response> CloneAsync();
+
+    // Body mixin
+    public ValueTask<ReadableStream?> GetBodyAsync();
+    public ValueTask<bool> GetBodyUsedAsync();
+    public ValueTask<byte[]> ArrayBufferAsync();
+    public ValueTask<Blob> BlobAsync();
+    public ValueTask<byte[]> BytesAsync();
+    [Experimental("ASP0040", UrlFormat = "https://aka.ms/aspnet/analyzer/{0}")]
+    public ValueTask<FormData> FormDataAsync();
+    public ValueTask<JsonElement> JsonAsync();
+    public ValueTask<string> TextAsync();
+
+    public ValueTask DisposeAsync();
+}
+
+public sealed class Request : IAsyncDisposable
+{
+    public Headers Headers { get; }
+
+    public ValueTask<string> GetMethodAsync();
+    public ValueTask<string> GetUrlAsync();
+    public ValueTask<string> GetReferrerAsync();
+    public ValueTask<ReferrerPolicy> GetReferrerPolicyAsync();
+    public ValueTask<RequestMode> GetModeAsync();
+    public ValueTask<RequestCredentials> GetCredentialsAsync();
+    public ValueTask<RequestCache> GetCacheAsync();
+    public ValueTask<RequestRedirect> GetRedirectAsync();
+    public ValueTask<string> GetIntegrityAsync();
+    public ValueTask<bool> GetKeepaliveAsync();
+    public ValueTask<Request> CloneAsync();
+
+    // Body mixin (Request.body is deferred because it is not Baseline)
+    public ValueTask<bool> GetBodyUsedAsync();
+    public ValueTask<byte[]> ArrayBufferAsync();
+    public ValueTask<Blob> BlobAsync();
+    public ValueTask<byte[]> BytesAsync();
+    [Experimental("ASP0040", UrlFormat = "https://aka.ms/aspnet/analyzer/{0}")]
+    public ValueTask<FormData> FormDataAsync();
+    public ValueTask<JsonElement> JsonAsync();
+    public ValueTask<string> TextAsync();
+
+    public ValueTask DisposeAsync();
+}
+
+public sealed class Headers
+{
+    public ValueTask AppendAsync(string name, string value);
+    public ValueTask DeleteAsync(string name);
+    public ValueTask<string?> GetAsync(string name);
+    public ValueTask<bool> HasAsync(string name);
+    public ValueTask SetAsync(string name, string value);
+
+    // iterable<ByteString, ByteString>
+    public ValueTask<IReadOnlyList<KeyValuePair<string, string>>> EntriesAsync();
+    public ValueTask<IReadOnlyList<string>> KeysAsync();
+    public ValueTask<IReadOnlyList<string>> ValuesAsync();
+}
+
+public sealed class RequestInit
+{
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public string? Method { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    [JsonConverter(typeof(HeadersInitJsonConverter))]   // internal: writes [name, value] pairs
+    public IEnumerable<KeyValuePair<string, string>>? Headers { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public BodyInit? Body { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public string? Referrer { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    [Experimental("ASP0040", UrlFormat = "https://aka.ms/aspnet/analyzer/{0}")]
+    public ReferrerPolicy? ReferrerPolicy { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public RequestMode? Mode { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public RequestCredentials? Credentials { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public RequestCache? Cache { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public RequestRedirect? Redirect { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public string? Integrity { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public bool? Keepalive { get; set; }
+    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
+    public RequestPriority? Priority { get; set; }
+}
+
+// C# union type. Each case converts to BodyInit implicitly, so Body = "text" compiles.
+// URLSearchParams and ReadableStream are deferred (see Scope).
+public union BodyInit(string, byte[], Blob, FormData);
+
+[JsonConverter(typeof(JsonStringEnumConverter<RequestMode>))]
+public enum RequestMode
+{
+    [JsonStringEnumMemberName("navigate")] Navigate,
+    [JsonStringEnumMemberName("same-origin")] SameOrigin,
+    [JsonStringEnumMemberName("no-cors")] NoCors,
+    [JsonStringEnumMemberName("cors")] Cors,
+}
+
+[JsonConverter(typeof(JsonStringEnumConverter<RequestCredentials>))]
+public enum RequestCredentials
+{
+    [JsonStringEnumMemberName("omit")] Omit,
+    [JsonStringEnumMemberName("same-origin")] SameOrigin,
+    [JsonStringEnumMemberName("include")] Include,
+}
+
+[JsonConverter(typeof(JsonStringEnumConverter<RequestCache>))]
+public enum RequestCache
+{
+    [JsonStringEnumMemberName("default")] Default,
+    [JsonStringEnumMemberName("no-store")] NoStore,
+    [JsonStringEnumMemberName("reload")] Reload,
+    [JsonStringEnumMemberName("no-cache")] NoCache,
+    [JsonStringEnumMemberName("force-cache")] ForceCache,
+    [JsonStringEnumMemberName("only-if-cached")] OnlyIfCached,
+}
+
+[JsonConverter(typeof(JsonStringEnumConverter<RequestRedirect>))]
+public enum RequestRedirect
+{
+    [JsonStringEnumMemberName("follow")] Follow,
+    [JsonStringEnumMemberName("error")] Error,
+    [JsonStringEnumMemberName("manual")] Manual,
+}
+
+[JsonConverter(typeof(JsonStringEnumConverter<RequestPriority>))]
+public enum RequestPriority
+{
+    [JsonStringEnumMemberName("high")] High,
+    [JsonStringEnumMemberName("low")] Low,
+    [JsonStringEnumMemberName("auto")] Auto,
+}
+
+[JsonConverter(typeof(JsonStringEnumConverter<ResponseType>))]
+public enum ResponseType
+{
+    [JsonStringEnumMemberName("basic")] Basic,
+    [JsonStringEnumMemberName("cors")] Cors,
+    [JsonStringEnumMemberName("default")] Default,
+    [JsonStringEnumMemberName("error")] Error,
+    [JsonStringEnumMemberName("opaque")] Opaque,
+    [JsonStringEnumMemberName("opaqueredirect")] OpaqueRedirect,
+}
+
+[JsonConverter(typeof(JsonStringEnumConverter<ReferrerPolicy>))]
+public enum ReferrerPolicy
+{
+    [JsonStringEnumMemberName("")] Empty,
+    [JsonStringEnumMemberName("no-referrer")] NoReferrer,
+    [JsonStringEnumMemberName("no-referrer-when-downgrade")] NoReferrerWhenDowngrade,
+    [JsonStringEnumMemberName("same-origin")] SameOrigin,
+    [JsonStringEnumMemberName("origin")] Origin,
+    [JsonStringEnumMemberName("strict-origin")] StrictOrigin,
+    [JsonStringEnumMemberName("origin-when-cross-origin")] OriginWhenCrossOrigin,
+    [JsonStringEnumMemberName("strict-origin-when-cross-origin")] StrictOriginWhenCrossOrigin,
+    [JsonStringEnumMemberName("unsafe-url")] UnsafeUrl,
+}
```

### Companion types (File API, FormData, Streams)

> **This section may extend the proposal too far, and it could be split out.** These types come from three other specifications: File API (`Blob`, `File`), XHR (`FormData`), and Streams (`ReadableStream`). They are kept here for now to see the whole shape that the Fetch body methods need. Each one could move to its own proposal and implementation slice, which this proposal would then depend on.

> **Names.** `Blob`, `File`, `FormData`, and `ReadableStream` keep their Web IDL names, like `Request`, `Response`, and `Headers`. `File` collides with `System.IO.File`. `System.IO` is in the implicit usings of every Blazor project, so in a component that imports `Microsoft.AspNetCore.Components.Platform`, a plain `File` is ambiguous and does not compile. Such code must qualify one of the two, or use an alias. Q4 in [Open Questions](#open-questions) asks whether to keep the names.

The body methods return these types, and `BodyInit` accepts some of them.

```diff
 namespace Microsoft.AspNetCore.Components.Platform;

+[JsonConverter(typeof(BlobJsonConverter))]   // internal: writes the browser object reference
+public class Blob : IAsyncDisposable   // not sealed, because File derives from it. No public constructor.
+{
+    public ValueTask<ulong> GetSizeAsync();
+    public ValueTask<string> GetTypeAsync();
+    public ValueTask<Blob> SliceAsync();
+    public ValueTask<Blob> SliceAsync(long start);
+    public ValueTask<Blob> SliceAsync(long start, long end);
+    public ValueTask<Blob> SliceAsync(long start, long end, string contentType);
+    public ValueTask<ReadableStream> StreamAsync();
+    public ValueTask<string> TextAsync();
+    public ValueTask<byte[]> ArrayBufferAsync();
+    public ValueTask<byte[]> BytesAsync();
+    public ValueTask DisposeAsync();
+}
+
+public sealed class File : Blob
+{
+    public ValueTask<string> GetNameAsync();
+    public ValueTask<long> GetLastModifiedAsync();
+}
+
+[JsonConverter(typeof(FormDataJsonConverter))]   // internal: writes the browser object reference
+public sealed class FormData : IAsyncDisposable
+{
+    public ValueTask AppendAsync(string name, string value);
+    public ValueTask AppendAsync(string name, Blob blobValue);
+    public ValueTask AppendAsync(string name, Blob blobValue, string filename);
+    public ValueTask DeleteAsync(string name);
+    public ValueTask<FormDataEntryValue?> GetAsync(string name);
+    public ValueTask<IReadOnlyList<FormDataEntryValue>> GetAllAsync(string name);
+    public ValueTask<bool> HasAsync(string name);
+    public ValueTask SetAsync(string name, string value);
+    public ValueTask SetAsync(string name, Blob blobValue);
+    public ValueTask SetAsync(string name, Blob blobValue, string filename);
+
+    // iterable<USVString, FormDataEntryValue>
+    public ValueTask<IReadOnlyList<KeyValuePair<string, FormDataEntryValue>>> EntriesAsync();
+    public ValueTask<IReadOnlyList<string>> KeysAsync();
+    public ValueTask<IReadOnlyList<FormDataEntryValue>> ValuesAsync();
+
+    public ValueTask DisposeAsync();
+}
+
+// typedef (File or USVString) FormDataEntryValue, as a C# union type
+public union FormDataEntryValue(File, string);
+
+public sealed class ReadableStream
+{
+    public ValueTask<bool> GetLockedAsync();
+    public ValueTask CancelAsync();
+    public ValueTask<ReadableStreamDefaultReader> GetReaderAsync();
+}
+
+public sealed class ReadableStreamDefaultReader
+{
+    public ValueTask<ReadableStreamReadResult> ReadAsync();
+    public ValueTask ReleaseLockAsync();
+    public ValueTask CancelAsync();
+}
+
+public sealed class ReadableStreamReadResult
+{
+    public byte[]? Value { get; init; }   // Web IDL "any"; every reachable stream yields Uint8Array
+    public bool? Done { get; init; }      // not "required" in Web IDL, so nullable
+}
```

### Design notes

- **Placement.** `fetch` is a member of `WindowOrWorkerGlobalScope`, so it goes on `Window`, as in `Browser.Window.FetchAsync(...)`. It is declared in an `extension(Window window)` block in `WindowFetchExtensions`, so the trimmer can drop the family. The family is reached as `Browser.Window.FetchAsync(...)`, not as a member of `IBrowserPlatform`, so `IBrowserPlatform` keeps `Window` as its only member.
- **Overloads.** `RequestInfo` is `Request or USVString`, so it becomes overloads on `string` and `Request`. `init` is optional with `= {}`, and the fetch method steps accept empty options. So each input has an overload without `init`. As the type mapping accepts, `FetchAsync(null)` does not compile.
- **`CancellationToken`.** Represents the optional last parameter, because `RequestInit.signal` is an `AbortSignal`. For now, the token cancels only the `FetchAsync` call itself, which completes when the response headers arrive. Once `FetchAsync` returns, the token no longer has any effect. It does not abort `ArrayBufferAsync()`, `BlobAsync()`, `BytesAsync()`, `FormDataAsync()`, `JsonAsync()`, `TextAsync()`, or stream reads that run later. In the opening example, the 10-second timeout covers the request up to the response headers, not the download in `BlobAsync()`. The browser behaves differently, because there the signal also aborts body reads. Q1 in [Open Questions](#open-questions) asks whether to match it. The body methods take no `CancellationToken` of their own, because the `Body` methods in Web IDL have no `AbortSignal`. Cancellation behavior is under [Implementation](#implementation).
- **Status is data.** Nothing throws for 4xx or 5xx responses. `redirect: Manual` gives `ResponseType.OpaqueRedirect` with status `0`. `mode: NoCors` to another origin gives `ResponseType.Opaque` with status `0` and an empty body. This is all browser behavior.
- **Attributes stay asynchronous.** `status`, `ok`, `url`, and the other attributes of `Response` and `Request` are not interfaces, so the skill projects them as `Get<X>Async()`. `headers` is `[SameObject] Headers`, so it is a synchronous property. `Response.Headers` and `Request.Headers` are instance properties, not `extension` blocks. The skill uses `extension` blocks so that the trimmer can drop a family the application never touches. `Headers` belongs to the same family as `Response` and `Request`, so an `extension` block would trim nothing. Reading the property makes no browser call. It returns the .NET `Headers` wrapper, which reads `headers` once, on its first method call.
- **Returned enums.** Six attributes return a Web IDL enum: `Response.type`, plus `referrerPolicy`, `mode`, `credentials`, `cache`, and `redirect` on `Request`. Every major browser supports all values of these six enums today. The enums have only the Web IDL members. A spec can still add a value later. If a newer browser returns a string that the enum does not have, the getter throws `NotSupportedException`, and the message names the string.
- **`Body` mixin.** Web IDL copies mixin members into each interface that includes it. So `Request` and `Response` each declare them, with no shared base class.
- **`ByteString`** (the method, header names and values, `statusText`) maps to `string`. The browser throws `TypeError` for characters above U+00FF.
- **Body size.** `ArrayBufferAsync()`, `BytesAsync()`, `TextAsync()`, and `JsonAsync()` on `Request`, `Response`, and `Blob` copy the whole body to .NET. `ArrayBufferAsync()` and `BytesAsync()` both return `byte[]`, because .NET has no separate type for an `ArrayBuffer`. `FetchOptions.MaxBodySize` limits all four. The default is 512,000 bytes, the same default as `IJSStreamReference.OpenReadStreamAsync`. An app raises or lowers it with `services.Configure<FetchOptions>(...)`, and `long.MaxValue` removes the limit. A value below 1 throws `ArgumentOutOfRangeException` from the setter. A body larger than the limit throws `IOException`, and the message gives both sizes. The browser has already read the body at that point, so `GetBodyUsedAsync()` returns `true`, and a `clone()` made before the call can still be read. The limit also applies to each chunk that `ReadableStreamDefaultReader.ReadAsync()` transfers. `BlobAsync()`, `FormDataAsync()`, and `GetBodyAsync()` keep the body in the browser, so the limit does not apply to them.
- **Stream chunks are `byte[]`.** `ReadableStreamReadResult.value` is `any` in Web IDL. Every stream this proposal reaches (`Response.body` and `Blob.stream()`) yields `Uint8Array` chunks, so `Value` is `byte[]?`, and it is `null` when the stream is done. A package-wide mapping for `any` is not needed for this. The browser decides the size of each chunk, and a chunk can exceed the 32 KB receive limit in Interactive Server. So each chunk reaches .NET through `IJSStreamReference`, within `FetchOptions.MaxBodySize` (see "Body size"). That costs a few interop calls per chunk. A BYOB reader, which would let .NET choose the chunk size, became Baseline only in 2026-03, so it is deferred. `Done` is `bool?`, because Web IDL does not mark it `required`, and the type mapping makes such members of a returned dictionary nullable.
- **`text()` and `json()` are decoded in .NET.** Bodies reach .NET through `IJSStreamReference`, because of the 32 KB receive limit in Interactive Server. `IJSStreamReference` accepts only `ArrayBuffer`, `ArrayBufferView`, and `Blob`, not a string. So `TextAsync()` and `JsonAsync()` call `arrayBuffer()` in the browser and decode the bytes as UTF-8 in .NET. `JsonAsync()` then parses the `JsonElement` in .NET. These are not one-to-one calls to `text()` and `json()`, but the result is the same, because the specification also decodes UTF-8 for both. `bodyUsed`, the body size limit, and the errors for a body that was already used behave as they do for the other body methods.
- **`json()` returns `JsonElement`.** Web IDL declares `json()` as `Promise<any>`. `JsonAsync()` returns `System.Text.Json.JsonElement`. The value comes from parsing JSON, so it is always plain JSON (an object, array, string, number, boolean, or `null`), and `JsonElement` represents every such value. The browser never runs `json()`, because the body is parsed in .NET, so there is no browser object to project. Reading the element makes no browser call. An app that wants a typed value calls `Deserialize<T>()` on it. A later `JsonAsync<T>()` in an extension layer can build on the same parsing. `JsonElement` differs from the browser in three ways, and the documentation and tests cover each:
  - A body that is not valid JSON throws `JsonException`. The browser would reject with `SyntaxError`.
  - A number keeps its exact text, while the browser rounds integers above 2^53.
  - System.Text.Json and `JSON.parse` may handle duplicate keys and a leading byte-order mark differently.

  This decides `any` only for `json()`. A package-wide type for `any`, for values that are not JSON and for `any` inputs such as the reason of `cancel()` and `abort()`, is left for the first family that needs it.
- **`unsigned short status`** maps to `ushort`, and **`unsigned long long size`** maps to `ulong`. The type mapping lists neither yet, so they follow its pattern (`unsigned long` maps to `uint`).
- **Unions.** Web IDL unions that appear as dictionary members or return values, which overloads cannot replace, map to C# union types. .NET 11 ships them, System.Text.Json supports them natively, and JS interop sends only the active case, without an envelope (`JSRuntimeUnionTests`, #67296).
  - `BodyInit` is `union BodyInit(string, byte[], Blob, FormData)`. Each case converts implicitly, so `Body = orderJson` compiles. `URLSearchParams` and `ReadableStream` are deferred (see [Scope](#scope)).
  - `FormDataEntryValue` is `union FormDataEntryValue(File, string)`, and a caller reads it with pattern matching on `Value`.

  A union is a value type, so `BodyInit?` and `FormDataEntryValue?` are `Nullable<T>`, and `null` means not set, or no entry. `default(T)` holds no case, so code that switches on `Value` needs a `_` branch. Telling a `File` from a string in `FormData.get()` costs one extra interop call, because there is no JavaScript module. Types that must travel as a browser object reference carry an internal converter (see [Implementation](#implementation)).
- **`RequestInit` members.** Every Web IDL member is optional with no default. By the type mapping, each one is nullable with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, and no default is copied into .NET. The XML doc of each member says what `null` means (the browser default). `body` is declared `BodyInit? body` in Web IDL: nullable, with no default. The type mapping has no row for that shape, so the fetch method steps decide. They use the body only if `init["body"]` exists and is not `null`, so `null` and a missing member both mean no body. `RequestInit.Body` is therefore `BodyInit?`, and it is left out when it is `null`.
- **`HeadersInit`** maps to `IEnumerable<KeyValuePair<string, string>>`. It keeps order and allows the same name twice, which the sequence form needs. It also accepts a `Dictionary<string, string>` for the record form. On the wire it must be an array of `[name, value]` pairs, not `{"key":…,"value":…}`, so the property needs a converter (see [Implementation](#implementation)).
- **Values are checked by the browser.** The browser rejects forbidden header names, `mode: NoCors` with a method other than GET, HEAD, or POST, a GET request with a body, and `only-if-cached` without `same-origin`. .NET checks only `null` arguments, and `ArgumentNullException` is the only documented argument exception. .NET checks the `RequestInit` argument itself, but not its members.
- **Disposal.** `FetchAsync`, `CloneAsync`, `BlobAsync`, `FormDataAsync`, `SliceAsync`, `RequestConstructor.CreateAsync`, and `FormData.GetAsync` (for a `File`) each create a new browser object that only the caller holds. So `Response`, `Request`, `Blob`, `File`, and `FormData` implement public `IAsyncDisposable`. `DisposeAsync()` releases the browser object. It can be called more than once and never throws, and every other member then throws `ObjectDisposedException`. `Headers`, `ReadableStream`, and `ReadableStreamDefaultReader` belong to the object they came from (the `[SameObject]` `headers`, or `body`). They are not disposable themselves, and they are released, and become unusable, when their owner is disposed. Disposing a `Response` or `Request` does not abort a body read that is still running, which matches the browser. The read completes or fails as it would have, and its result still reaches the caller. Only the members called after `DisposeAsync()` throw `ObjectDisposedException`.
- **Missing objects and members.** `fetch`, `Request`, `Response`, `Headers`, `Blob`, `FormData`, and `ReadableStream` exist in every browser that Blazor supports. So the family has no acquisition that can fail with `NotSupportedException`. Members newer than the supported browser range, `Response.BytesAsync()`, `Request.BytesAsync()`, and `Blob.BytesAsync()`, throw `NotSupportedException` in a browser that lacks them, per the rule answered in #69844.
- **Telling a missing member from a browser error.** A `BytesAsync()` call can fail for two reasons:
  1. The browser has no `bytes()`, for example Chrome before 132 or Safari before 18. This becomes `NotSupportedException`.
  2. The body was already used, because `text()`, `json()`, `blob()`, or another body method already read it, or a reader locked the stream. The browser rejects with `TypeError`, and this stays a `JSException`.

  So that the two are never confused, the implementation checks first whether `bytes()` exists, and it never reads error messages. On the first `BytesAsync()` call in a scope, it reads the function from the prototype with `jsRuntime.GetValueAsync<IJSObjectReference?>("Response.prototype.bytes")`, or `Request.prototype.bytes` or `Blob.prototype.bytes`. It then disposes the returned reference.
  - **A reference comes back:** `bytes()` exists. JS interop can return a reference to a function.
  - **JS interop throws `JSException` for a property that is not defined, or the result is `null`:** `bytes()` is missing.

  The internal `FetchFeature` caches the answer for each of the three prototypes for the rest of the scope. An answer is cached only when the check completes. A disconnect or a timeout reaches the caller unchanged, and the next call checks again. When `bytes()` is missing, `BytesAsync()` throws `NotSupportedException` with the check's `JSException` as the inner exception, without calling `bytes()`. When it exists, every `JSException` from the `bytes()` call itself reaches the caller unchanged.

  The check costs one extra interop call, plus the release of the reference, for each prototype and scope. It runs only on the first `BytesAsync()` call. Other members need no check, because every browser that Blazor supports has them.

## Usage Examples

```csharp
builder.Services.AddBrowserPlatform();

// Optional: allow bodies up to 10 MB in ArrayBufferAsync(), BytesAsync(), TextAsync(), and JsonAsync().
builder.Services.Configure<FetchOptions>(options => options.MaxBodySize = 10_000_000);
```

```razor
@using Microsoft.AspNetCore.Components.Platform
@inject IBrowserPlatform Browser

@code {
    // GET with the browser's cookies. The status is data.
    private async Task<string?> LoadOrdersAsync()
    {
        await using var response = await Browser.Window.FetchAsync("/api/orders");

        if (!await response.GetOkAsync())
        {
            Console.WriteLine($"Failed: {await response.GetStatusAsync()} {await response.GetStatusTextAsync()}");
            return null;
        }

        return await response.TextAsync();
    }

    // Cross-origin POST with credentials and a JSON body. The timeout aborts the request in the browser.
    private async Task<JsonElement> CreateOrderAsync(string orderJson)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var response = await Browser.Window.FetchAsync(
            "https://orders.contoso.com/api/orders",
            new RequestInit
            {
                Method = "POST",
                Mode = RequestMode.Cors,
                Credentials = RequestCredentials.Include,
                Headers = [new("Content-Type", "application/json")],
                Body = orderJson,
            },
            timeout.Token);

        return await response.JsonAsync();
    }

    // Read headers, and handle a redirect without following it.
    private async Task<string?> GetRedirectTargetAsync()
    {
        await using var response = await Browser.Window.FetchAsync(
            "/login",
            new RequestInit { Redirect = RequestRedirect.Manual, Cache = RequestCache.NoStore });

        return await response.GetTypeAsync() == ResponseType.OpaqueRedirect
            ? "redirected (the browser hides the location)"
            : await response.Headers.GetAsync("Location");
    }

    // Stream a large body chunk by chunk.
    private async Task<long> CountBytesAsync()
    {
        await using var response = await Browser.Window.FetchAsync("/downloads/report.csv");
        var body = await response.GetBodyAsync();
        if (body is null)
        {
            return 0;
        }

        var reader = await body.GetReaderAsync();
        long total = 0;
        while (true)
        {
            var chunk = await reader.ReadAsync();
            if (chunk.Done == true)
            {
                break;
            }

            total += chunk.Value!.Length;
        }

        return total;
    }

    // Keep the body in the browser as a Blob, and slice it there.
    private async Task<string> PeekAsync()
    {
        await using var response = await Browser.Window.FetchAsync("/files/large.bin");
        await using var blob = await response.BlobAsync();
        await using var head = await blob.SliceAsync(0, 16);

        return Convert.ToHexString(await head.BytesAsync());
    }

    // Build a Request once, and fetch it with other options.
    // Browser.Window.Request is the constructor, as window.Request is in JavaScript.
    private async Task<ushort> HeadAsync()
    {
        await using var request = await Browser.Window.Request.CreateAsync("/health");
        await using var response = await Browser.Window.FetchAsync(request, new RequestInit { Method = "HEAD" });

        return await response.GetStatusAsync();
    }
}
```

`FormDataAsync()` is experimental, so the code suppresses `ASP0040`:

```csharp
#pragma warning disable ASP0040
await using var form = await response.FormDataAsync();
#pragma warning restore ASP0040
FormDataEntryValue? entry = await form.GetAsync("title");
var title = entry?.Value switch
{
    string text => text,
    File file => await file.GetNameAsync(),
    _ => null,   // no entry named "title"
};
```

The API works in Interactive Server and WebAssembly. It is unavailable before interactivity.

## Implementation

It follows the skill rules. No JavaScript module is added.

- **Files.** These go in `src/Components/Platform/src`:
  - one file per type: `Response.cs`, `Request.cs`, `RequestConstructor.cs`, `Headers.cs`, `RequestInit.cs`, `BodyInit.cs`, one file per enum, and the companion types
  - `WindowFetchExtensions.cs` for the extension block

  The public surface is listed in `PublicAPI.Unshipped.txt`, and experimental members take no prefix there.
- **Entry from `Window`.** `fetch` is a function, not an object, so there is no shared `IJSObjectReference` to acquire. `WindowFetchExtensions` gets an internal feature with `window.GetOrAddFeature(static jsRuntime => new FetchFeature(jsRuntime))`. That gives it the `IJSRuntime` and a place to track live objects for the scope-end safety net (Q1). The feature implements `IInternalAsyncDisposal` and never throws from disposal. `FetchAsync` calls `jsRuntime.InvokeAsync<IJSObjectReference>("fetch", ...)`, and the returned reference becomes a `Response`.
- **Constructor objects.** `Window.Request` is a shared `RequestConstructor` from `window.GetOrAddFeature(...)`. It holds no browser reference. `CreateAsync` calls `jsRuntime.InvokeConstructorAsync("Request", ...)` and wraps the result in a new `Request`. Like every shared wrapper, `RequestConstructor` has no public `DisposeAsync`.
- **Object references.** `Response`, `Request`, `Blob`, `FormData`, and `File` hold the reference that the call returned, so they need no lazy acquisition. `Headers` (`response.headers`) and `ReadableStream` (`response.body`) are acquired lazily on first use with `GetValueAsync<IJSObjectReference>`. They share the pending task between callers and do not keep a failed or canceled acquisition. After disposal, every member throws `ObjectDisposedException`.
- **Arguments.** They are passed in `params` form, such as `InvokeAsync<string?>("get", name)`. They are written as `[...]` only where an argument could be taken for the argument list, the timeout, or the token: a `byte[]` body, a `TimeSpan`, or a `CancellationToken`.
- **Cancellation.**
  - With no token, or one that cannot be canceled (`!cancellationToken.CanBeCanceled`), the implementation calls the `IJSRuntime` overload without a token. Any token, even `None`, would turn off the default JS interop timeout.
  - With a token that can be canceled, it calls `InvokeConstructorAsync("AbortController")` and reads `signal`. It passes the signal reference inside the `init` it sends, because JS interop revives `__jsObjectId` in nested arguments. It then registers `abort()` on the token.
  - An `AbortError` rejection becomes `OperationCanceledException` for that token. Only that `JSException` is translated. A disconnect or a timeout reaches the caller unchanged.
  - The registration is removed when `FetchAsync` completes, so a later cancellation does not abort body reads. See [Open Questions](#open-questions) (Q1).
- **Wire shape of `RequestInit`.** `RequestInit` is serialized directly. Public types carry `[JsonConverter]` attributes that name internal converters:
  - `BodyInit` is a C# union type, which System.Text.Json writes as its active case without an envelope. A `string` stays a string, and a `byte[]` goes through the binary `__byte[]` transfer.
  - `Blob` and `FormData` carry `BlobJsonConverter` and `FormDataJsonConverter`, which write the browser object reference (`__jsObjectId`). Without them, System.Text.Json would write their public members, which gives an empty object.
  - `RequestInit.Headers` carries `HeadersInitJsonConverter`, which writes an array of `[name, value]` pairs.
  - A `Request` passed to `FetchAsync` or `CreateAsync` is an argument, not a dictionary member, so the implementation passes its object reference directly.
  - The abort signal is an internal `[JsonInclude]` property of `RequestInit`. A caller can reuse one `RequestInit` for several calls, so the implementation sets the signal on a copy of the caller's `RequestInit`, never on the original.
- **Trimming.** Every method that passes `RequestInit` gets `[DynamicDependency(LinkerFlags.JsonSerialized, typeof(RequestInit))]`, and every method that returns `ReadableStreamReadResult` inside another type gets one for that type. A returned collection uses an array as the `TValue`, such as `string[][]` for `EntriesAsync()`, and is exposed as `IReadOnlyList<T>`. Enums keep their converter attributes, because `RequestInit` sends them. Returned enum values are read as `string` and mapped with a `switch` on the Web IDL strings, so reading does not depend on reflection. Any other string throws `NotSupportedException`.
- **Body transfer.** Bodies come back through `IJSStreamReference` instead of as plain interop values, because of the receive limit in Interactive Server. `IJSStreamReference` accepts `ArrayBuffer`, `ArrayBufferView`, and `Blob` (`createJSStreamReference` in `Microsoft.JSInterop.ts`), so `arrayBuffer()` and `bytes()` results can be returned as a stream reference directly. `OpenReadStreamAsync` gets `FetchOptions.MaxBodySize` as its `maxAllowedSize`. The `ArgumentOutOfRangeException` that it throws for a larger body names a parameter that the caller never passed, so the implementation turns it into `IOException`. `FetchOptions` comes from `IOptions<FetchOptions>`, which `BrowserPlatform` passes to the internal `FetchFeature`. The package then references `Microsoft.Extensions.Options` in addition to `Microsoft.Extensions.DependencyInjection`.

## Documentation

The XML docs use these forms:

- Each `<summary>` names the browser object the call touches. For example, `Headers.SetAsync`: "Sets the value of the header with the specified name, replacing all existing values for that name." `Response.Headers`: "Gets the headers of the response."
- A `<param>` is a noun phrase only. Behavior goes in the `<summary>`.
- `ValueTask<T>` describes the value itself, such as "The HTTP status code of the response." A bare `ValueTask` uses "A <see cref="ValueTask"/> representing the completion of the operation."
- A nullable result uses "…, or <see langword="null"/> if …", such as `GetBodyAsync()`: "The body of the response, or <see langword="null"/> if the response has no body."
- A returned enum getter documents `NotSupportedException` for a value the enum does not have, for example `GetModeAsync()`: "The browser returned a mode that this version does not support."
- `JsonException` is documented on `JsonAsync()`: "The body is not valid JSON."
- `IOException` is documented on `ArrayBufferAsync()`, `BytesAsync()`, `TextAsync()`, `JsonAsync()`, and `ReadAsync()`: "The body is larger than <see cref="FetchOptions.MaxBodySize"/>."
- `ArgumentNullException` is documented for every non-nullable reference parameter. `NotSupportedException` is documented only on the `BytesAsync()` members: "The browser does not provide `bytes()` for the current page."
- `JSException` is not documented, for example for a network error, a body that was already used, or a header that `Headers` forbids.
- `<remarks>` covers behavior the browser has but .NET does not show. Examples: a status that is not successful does not throw; an opaque response has status `0`; and the token passed to `FetchAsync` does not cancel body reads after `FetchAsync` returns.
- `RequestInit.ReferrerPolicy` gets a `<remarks>` for Safari on iOS: "Safari on iOS and other browsers on iOS do not support this option for `fetch`. The request then uses the default referrer policy."
- Each `RequestInit` member says what `null` means. For example, `Mode`: "…, or <see langword="null"/> to use the browser default (`cors`)."

## Verification

- **Unit tests** go in `test/FetchTest.cs` and one file for each companion type. A recording `IJSRuntime` and `IJSObjectReference` cover:
  - the interop identifiers and arguments of every member
  - `JsonAsync()` parsing an object, an array, and each JSON primitive, throwing `JsonException` for invalid JSON, keeping a number above 2^53 exactly, and the behavior for duplicate keys and a leading byte-order mark
  - `FetchOptions.MaxBodySize` passed as `maxAllowedSize`, its default of 512,000, a configured value, `ArgumentOutOfRangeException` from the setter for a value below 1, and `IOException` for a larger body from each body method and from `ReadAsync()`
  - each returned enum getter mapping every Web IDL string to its member, and throwing `NotSupportedException` that names any other string
  - lazy acquisition of `Headers` and `ReadableStream`
  - one shared pending acquisition when two callers start before it completes
  - acquiring again after a canceled or failed acquisition, while other failures pass through unchanged
  - stable `Headers` instances and distinct `Response` instances
  - disposal of the held references
  - `ObjectDisposedException` after disposal
  - a body read that is still running when the `Response` is disposed, which is not aborted and still returns its result
  - disposal that does not wait for a pending acquisition and does not throw after failures
  - the existence check reading `Response.prototype.bytes`, `Request.prototype.bytes`, and `Blob.prototype.bytes`. It runs once for each prototype and scope, shares a check that is still pending, disposes the returned reference, and checks again after a disconnect or a timeout
  - a `JSException` from the check turning `BytesAsync()` into `NotSupportedException` without a `bytes` call, with the `JSException` as the inner exception
  - any `JSException` from the `bytes()` call itself, such as a browser `TypeError` for a body that was already used, reaching the caller unchanged
- **JSON tests** use a `SerializingJSRuntime` that derives from `JSRuntime` and records `ArgsJson`. They cover:
  - an empty `RequestInit` sending `{}`
  - each member left out when it is `null`
  - each enum value as its Web IDL string, as a theory
  - `Headers` as `[name, value]` pairs
  - each `BodyInit` kind
  - the abort signal sent only with a token that can be canceled
- **Cancellation tests** check that no token and `CancellationToken.None` call the overload without a token. They check that a canceled token calls `abort()` on the controller and throws `OperationCanceledException`, and that the registration is removed when `FetchAsync` completes, so canceling the token afterward does not call `abort()`.
- **`PublicApiTest`**:
  - `RequestConstructor`, `Headers`, `ReadableStream`, and `ReadableStreamDefaultReader` go in `SharedInstances_CannotBeDisposedByApplicationCode`.
  - Every new type goes in `LiveBrowserObjectMethods_AreAsynchronous`. `RequestConstructor.CreateAsync` returns `ValueTask<Request>`, and `Response.Headers` is a property, so the test needs no exceptions.
  - The interop type check needs no change, as long as `JsonElement`, `byte[]`, and the new types are the only exposed types.
- **E2E tests** go in `PlatformTestApp` and `PlatformTestApp.E2E.Tests`, with a `FetchDemo.razor` used by a server page and a WebAssembly page. The app adds small endpoints:
  - one that echoes the method, headers, body, and cookies of a request
  - one with a redirect
  - one that returns 404
  - one large body (over 32 KB) to check Interactive Server, and one over 512,000 bytes to check `IOException` with the default limit and success with a raised limit
  - one slow endpoint that records whether the client aborted

  The tests follow these practices:
  - Each result shows "<operation>: <result>" and has a `data-run` counter that goes up after the operation completes. The test waits for the next counter value before it checks the text.
  - The tests read browser state directly, not only through the same API. For example, they check what the echo endpoint received for each `RequestInit` member, and they read the cookies that the browser sent.
  - An init script removes `Response.prototype.bytes`, and the test checks that it is gone before the call. The page then shows `NotSupportedException` with `JSException` as the inner exception, in both render modes.
  - A second step reads the body with `TextAsync()` and then calls `BytesAsync()` in a browser that has `bytes()`. The page shows `JSException`, not `NotSupportedException`, so a used body is never reported as a missing member.
  - The tests also run from the output of `dotnet publish` of the E2E project, as on Helix.
- **Trimming.** A published, trimmed WebAssembly app shows four things:
  - Every `RequestInit` member reaches the browser, through the echo endpoint.
  - `ReadableStreamReadResult` deserializes.
  - The enum strings survive.
  - An app that uses only `Storage` drops the Fetch family.

## Alternative Designs

- **Read `status`, `ok`, `url`, and the other attributes once, as synchronous properties.** The attributes of a `Response` do not change, so a snapshot would be accurate. But without a JavaScript module, each attribute costs its own interop call. Reading all of them on every fetch would add six round trips, even for callers that never read them. `Get<X>Async()` costs a call only when it is used.
- **Make `AbortController` and `AbortSignal` public.** That matches Web IDL more closely, but it duplicates `CancellationToken`, and `AbortSignal` events are deferred.

## Risks

- **The receive limit in Interactive Server.** Values from JavaScript to .NET travel as SignalR messages, and `MaximumReceiveMessageSize` limits them to 32 KB by default. If `TextAsync()`, `JsonAsync()`, `BytesAsync()`, or `ArrayBufferAsync()` returned a larger result as a plain interop value, the circuit would break. The implementation must move bodies through `IJSStreamReference`, and an E2E test with a body over 32 KB checks it.
- **Body size limit.** With the default of 512,000 bytes, an app that reads larger bodies with `TextAsync()` or `JsonAsync()` gets `IOException` until it raises `FetchOptions.MaxBodySize`. A higher limit makes .NET hold the whole body in memory, on the server in Interactive Server. `BlobAsync()` and streaming avoid that.
- **The JS interop timeout in Interactive Server.** Without a `CancellationToken`, a fetch that takes longer than `CircuitOptions.JSInteropDefaultCallTimeout` (one minute by default) fails in .NET while the browser request continues. With a token, the timeout is off and the token controls the request.
- **Object lifetime.** Each `Response`, `Request`, `Blob`, and `FormData` holds a JS object reference until it is disposed. In Interactive Server, an application that does not dispose them keeps the browser objects and their bodies alive for the whole circuit, unless the scope-end safety net releases them (Q1). A reference that arrives after the scope ends is never released.
- **Round trips.** Every attribute read and body read is an interop call, and in Interactive Server each one is a network round trip. Streaming costs at least one call per chunk. A fetch with a token that can be canceled costs two extra calls to create the `AbortController` and read its signal. The first `BytesAsync()` call in a scope costs one extra call to check that `bytes()` exists.
- **Security.** Code in an Interactive Server circuit can make the user's browser send requests with the user's credentials, within CORS, and then read the responses on the server. Page JavaScript can already do this, so no new capability is added. Still, the documentation should say plainly that responses, including data protected by the user's cookies, reach server code.


## Open Questions

Questions answered in #69844 that apply here:

- **Members that are not Baseline** ship now with `[Experimental("ASP0040")]`, the single package-wide ID (https://github.com/dotnet/aspnetcore/issues/69844#issuecomment-6262595355). This proposal applies it to `Request.FormDataAsync()`, `Response.FormDataAsync()`, and `RequestInit.ReferrerPolicy`.
- **A missing object or member** throws `NotSupportedException`. This covers a newer member that an older browser lacks. This proposal applies it to `BytesAsync()`. The design notes describe how a missing member is detected.

Open:

Each question that has **For now** describes what this proposal currently does. The team should confirm it or choose another option.

1. **What happens to a `Response` after `FetchAsync` returns.** Two related choices decide how long the browser side of a response lives and what can still stop it.
   - **Objects that are never disposed.** The created objects implement public `IAsyncDisposable` (see the "Disposal" design note). An app that forgets to dispose one keeps the browser object, and any body that has not been read, alive for the whole circuit in Interactive Server. **Proposal:** as a safety net, the internal `FetchFeature` tracks the created objects and releases any that are still alive when the scope ends. Is the safety net wanted, given that it holds a reference to every object until it is disposed? Should it cover every created object, including a `Blob` sliced from another `Blob` and a `File` from `FormData`, or only `Response` and `Request`?
   - **Cancellation of later body reads.** In the browser, aborting the signal also fails a body read that is still running. So a token passed to `fetch()` also cancels `text()`, `blob()`, and stream reads that start later. **For now**, this proposal does not do that. The token cancels only `FetchAsync`, and its registration is removed when `FetchAsync` returns (see the `CancellationToken` design note). In the opening example, the 10-second timeout therefore does not cover the download in `BlobAsync()`.

     Two other options:
     - Match the browser. Keep the registration until the `Response` is disposed, so the token also aborts later body reads.
     - Give the body methods their own `CancellationToken`. Web IDL has no `AbortSignal` on them, so this would add a parameter that the browser API does not have. Real cancellation would also need its own `AbortController`, and the browser can abort a body read only through the signal of the original request. That overload can be added later without a breaking change.
2. **`iterable<K, V>`.** The type mapping has no rule for iterable declarations. This proposal maps `entries()`, `keys()`, and `values()` to `EntriesAsync()`, `KeysAsync()`, and `ValuesAsync()`. Each returns a snapshot as `IReadOnlyList<...>`. `forEach` is left out because it takes a callback. Should this be the rule for all families?
3. **String properties by name.** The main goal is to cover types and properties that are not implemented or implemented in the future. This proposal does not include them, but they were considered: `GetStringPropertyAsync(string name)` on `Request` and `Response`, and `GetStringProperty(string name)` and `SetStringProperty(string name, string? value)` on `RequestInit`.

    One example is `targetAddressSpace` from Local Network Access (`"public"`, `"local"`, or `"loopback"`), which only Chrome 142 and later support. They would also read a value that a later spec adds to one of the six returned enums, for which the typed getter throws `NotSupportedException`.

    ```csharp
    var init = new RequestInit { Method = "GET" };
    init.SetStringProperty("targetAddressSpace", "local");   // sent as targetAddressSpace: "local"
    await using var request = await Browser.Window.Request.CreateAsync("http://192.168.1.10/status", init);

    try
    {
        Console.WriteLine(await request.GetStringPropertyAsync("targetAddressSpace"));   // "local"
    }
    catch (NotSupportedException)
    {
        // Firefox and Safari have no targetAddressSpace.
    }
    ```
4. **Name collisions.** `Request`, `Response`, `Headers`, `Blob`, `File`, and `FormData` keep their Web IDL names, because the skill maps names one to one and says not to rename on our own. Some of them collide with names that Blazor apps already use:
   - `File` collides with `System.IO.File`, which is in the implicit usings of every Blazor project. In a component that imports the package namespace, as the test apps do in `_Imports.razor`, a plain `File` is ambiguous and does not compile until one of the two is qualified or aliased.
   - `Headers` has the same name as the namespaces `System.Net.Http.Headers` and `Microsoft.Net.Http.Headers`, which can make it ambiguous in files that import them.
   - `Request` and `Response` are common names in application code, although ASP.NET Core itself uses `HttpRequest` and `HttpResponse`.

   **For now**, this proposal keeps all the Web IDL names. One alternative is to rename only the types that collide, such as `WebFile` for `File`. `BrowserFile` would be easy to confuse with `IBrowserFile` from `InputFile`. Another is to put the Fetch, File API, and FormData types in a sub-namespace, such as `Microsoft.AspNetCore.Components.Platform.Fetch`, so that an app imports them only where it uses them. Should the names stay as they are?
