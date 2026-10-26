# Fetch API proposal: what to take from the prototype

This compares the Fetch slice of the original package prototype with [fetch-api-proposal.md](fetch-api-proposal.md). For each difference, it says whether to adopt it in the proposal and where. Only difference 1 is applied to the proposal so far.

- **Prototype:** kotlarmilos/aspnetcore@341310c ("Add typed browser platform API prototype"). It added Web Storage, URL, and Fetch projections, unit tests, E2E tests, and the first version of the porting skill.
- **Compared against:** `main` at `9a57dad569` (Web Storage and the Cookie Store API) and the current `port-components-platform-api` skill.

## The prototype's Fetch slice

```csharp
public interface IBrowserPlatform
{
    Window Window { get; }
    UrlConstructor Url { get; }
    ValueTask<Response> FetchAsync(string resource, RequestInit? options = null);
}

public sealed class RequestInit
{
    public string? Method { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }

    internal Dictionary<string, object?> ToJavaScriptValue();   // leaves out null members
}

public sealed class Response : IAsyncDisposable
{
    public ValueTask<bool> GetOkAsync();
    public ValueTask<ushort> GetStatusAsync();
    public ValueTask<string> GetStatusTextAsync();
    public ValueTask<string> GetUrlAsync();
    public ValueTask<Headers> GetHeadersAsync();   // acquired once and cached
    public ValueTask<string> TextAsync();
    public ValueTask DisposeAsync();               // also disposes the cached Headers
}

public sealed class Headers : IAsyncDisposable
{
    public ValueTask AppendAsync(string name, string value);
    public ValueTask DeleteAsync(string name);
    public ValueTask<string?> GetAsync(string name);
    public ValueTask<bool> HasAsync(string name);
    public ValueTask SetAsync(string name, string value);
    public ValueTask DisposeAsync();
}

public sealed class UrlConstructor                 // the constructor pattern (URL family)
{
    public ValueTask<Url> CreateAsync(string url, string? baseUrl = null);   // InvokeConstructorAsync("URL", ...)
}
```

The implementation and tests:

- **Implementation.** `BrowserPlatform.FetchAsync` calls `jsRuntime.InvokeAsync<IJSObjectReference>("fetch", [resource, options.ToJavaScriptValue()])` and wraps the result in `Response`.
- **Unit tests (`FetchTest`).** They check:
  - the `fetch` identifier and the converted options dictionary
  - every `Response` getter
  - that `GetHeadersAsync()` returns the same object and reads `headers` once
  - that `DisposeAsync()` can be called twice, disposes both references, and makes `Response` and `Headers` throw `ObjectDisposedException`
- **Test app.** `POST /platform-api/echo` returns the request body with an `x-components-platform: browser` header. `FetchDemo.razor` shows `200:browser:request-body`, and `/platform-api/missing` shows `404:False`. The test runs in Interactive Server and WebAssembly.
- **Prototype skill.** It classifies browser values. A constructed object is a "sealed wrapper over an internal `IJSObjectReference`". A live child object is a "stable wrapper owned by its parent". An options dictionary gets an "explicit trimming-safe conversion". The skill also says to "recursively unwrap projected references in dictionaries, unions, and sequences" and to verify that a `CancellationToken` "aborts the browser operation".

## Differences to adopt

### 1. Constructor pattern for `Request` (changes the proposal's Q2) — applied

- **Proposal today:** `public static ValueTask<Request> CreateAsync(Window window, ...)` on `Request`.
- **Prototype:** a constructor object, `UrlConstructor.CreateAsync(...)`, over `InvokeConstructorAsync("URL", ...)`, reached from the platform as `Browser.Url`.
- **Suggestion:** adopt the pattern, and reach it from `Window`, as `window.Request` is in JavaScript:

  ```diff
   public static class WindowFetchExtensions
   {
       extension(Window window)
       {
           ...
  +        public RequestConstructor Request { get; }
       }
   }

  +public sealed class RequestConstructor
  +{
  +    public ValueTask<Request> CreateAsync(string input);
  +    public ValueTask<Request> CreateAsync(string input, RequestInit init);
  +    public ValueTask<Request> CreateAsync(Request input);
  +    public ValueTask<Request> CreateAsync(Request input, RequestInit init);
  +}
  ```

  Usage becomes `await Browser.Window.Request.CreateAsync("/health")`.
- **Why:**
  - The skill wants a developer to find the API from `Window.`, and a static method on `Request` is not reachable from there.
  - An extension property trims like `Window.CookieStore`.
  - `RequestConstructor` is a shared, non-disposable wrapper from `GetOrAddFeature`, so it fits the existing rules without a new one.
- **To change in the proposal:**
  - Scope row "`Request` constructor"
  - the Proposed API
  - the `HeadAsync` usage example
  - Q1 (the list of methods that create objects)
  - Q2: make this the recommendation, keep the static factory as an alternative, and note that the property `Request` sits next to the type `Request`
  - `PublicApiTest`: add `RequestConstructor` to `SharedInstances_CannotBeDisposedByApplicationCode`
- **Kept open:** whether the `<Name>Constructor` naming and the property named after the JavaScript global become the rule for all families.

### 2. Explicit conversion of `RequestInit` (changes the proposal's Q4)

- **Proposal today:** `RequestInit` carries `[JsonIgnore(WhenWritingNull)]` on every member, as `CookieInit` does. `BodyInit` needs an internal `[JsonConverter]`, and `HeadersInit` needs another. Q4 asks whether internal converters can appear on the public type.
- **Prototype:** an internal `RequestInit.ToJavaScriptValue()` that builds a `Dictionary<string, object?>` with only the members that are not `null`.
- **Suggestion:** adopt the conversion and extend it to every member:
  - enums as their Web IDL string, from a switch
  - `headers` as `string[][]` pairs
  - `body` as a `string`, a `byte[]`, or the `IJSObjectReference` of a `Blob` or `FormData`
  - `signal` with the abort signal reference
  - a `Request` argument unwrapped to its reference, following the prototype skill's rule on unwrapping projected references

  Remove the JSON attributes from `RequestInit` and the converter from `BodyInit`.
- **Why:** one place handles the unions and the reference unwrapping. The dictionary holds only strings, booleans, string arrays, `byte[]`, and object references, so it is trim-safe without `[DynamicDependency]`. No internal type appears in public metadata.
- **Cost:** the package then has two patterns for dictionaries that the application passes: attributes (`CookieInit`) and explicit conversion (`RequestInit`). Q4 should ask whether the type mapping states when to use each, for example "convert explicitly when a member is a union or holds a projected object".
- **To change in the proposal:**
  - the Proposed API for `RequestInit` and `BodyInit`
  - the design notes "RequestInit follows CookieInit" and "HeadersInit"
  - Implementation: replace "Wire shape of `RequestInit`" and rewrite "Trimming"
  - Risks "Trimming"
  - Q4

### 3. Ownership and disposal tests (supports the proposal's Q1)

The prototype already settled the shape that Q1 proposes. Objects that a call creates are publicly `IAsyncDisposable`. A child object (`Headers`) is cached and released with its owner, and after disposal both throw `ObjectDisposedException`. Add two things to Verification:

- `Assert.Same(response.Headers, response.Headers)`, and exactly one `headers` read, as in `Response_ForwardsPropertiesBodyAndHeaders`.
- `DisposeAsync()` called twice, then `ObjectDisposedException` from both `Response` and its `Headers`, as in `DisposeAsync_DisposesResponseAndHeadersAndRejectsFurtherCalls`.

Q1 can cite the prototype skill's classification ("constructed object → sealed wrapper", "live child object → wrapper owned by its parent") as prior agreement on the direction.

### 4. Test endpoints and demo (Verification)

Reuse the prototype's endpoints instead of inventing new ones:

- `POST /platform-api/echo`, extended to echo the method, headers, body, and cookies. Keep the `x-components-platform: browser` response header, so the test can tell that the browser made the request.
- `/platform-api/missing` for the 404 case (`404:False`).

Keep the #69845 `data-run` counter and the "<operation>: <result>" text, which the prototype's `FetchDemo` did not have.

### 5. JSON tests on top of the dictionary check (Verification)

The prototype's `FetchAsync_MapsRequestOptions` checked the converted `Dictionary<string, object?>` before serialization. Keep that check for the conversion itself, and add the `SerializingJSRuntime` tests from `CookieStoreTest`. They are the only ones that show the JSON the browser receives: the `[name, value]` pairs for headers, the enum strings, and the binary `byte[]` body.

### 6. Background and placement (the proposal's Background and Q3)

- **Background:** say that the prototype had a small Fetch slice that was left out when the package landed, and that this proposal extends it to the whole family.
- **Q3:** say where `Browser.FetchAsync(...)` in the summary comes from. The prototype declared `FetchAsync` and `Url` on `IBrowserPlatform`. `main` keeps `IBrowserPlatform` to a single `Window` member and adds families as `extension(Window)` blocks so they can be trimmed. A new interface member would also break anyone who implements `IBrowserPlatform`, for example in tests. That makes `Browser.Window.FetchAsync(...)` the consistent choice.

### 7. `DOMException` names (new open question)

The prototype skill said to "preserve browser failures, including `DOMException` name and data". The current skill keeps `JSException` out of the public contract. Fetch is the first family where the name matters:

- `AbortError` means canceled, which the proposal maps to `OperationCanceledException`.
- `TypeError` means a network or CORS failure, or a body that was already used.
- `SyntaxError` comes from `json()` for a body that is not valid JSON.

Add a question: is an undocumented `JSException` enough, or does the package need a public exception type that carries the name?

### 8. Compatibility with prototype call sites (design notes)

The proposal's types are a superset of the prototype's for the common calls. `RequestInit.Headers = new Dictionary<string, string> { ... }` still compiles against `IEnumerable<KeyValuePair<string, string>>`. `Body = "text"` still compiles through the implicit conversion to `BodyInit`. `GetOkAsync`, `GetStatusAsync` (`ushort`), `GetStatusTextAsync`, `GetUrlAsync`, and `TextAsync` keep their names and types. One line in the design notes is enough.

## Differences not to adopt

The current skill or review replaced these prototype practices. The proposal already follows the newer rule, so nothing changes. They are listed so that nobody reintroduces them from the prototype.

| Prototype | Current rule | Source |
| --- | --- | --- |
| `FetchAsync` on `IBrowserPlatform` | An extension on `Window` | `main` and the skill (see 6) |
| `RequestInit? options = null` | Overloads with and without `RequestInit` | The type mapping's rule for an optional dictionary with `= {}` |
| `RequestInit` with `{ get; init; }` | `{ get; set; }` | The type mapping: a dictionary the application passes uses settable properties. |
| `IReadOnlyDictionary<string, string>? Headers` | `IEnumerable<KeyValuePair<string, string>>?` | `HeadersInit` also allows a list of pairs with the same name twice. |
| `GetHeadersAsync()` | The synchronous `Headers` property | The skill's rule for `[SameObject]` interface attributes |
| `Headers` publicly `IAsyncDisposable` | Not disposable, released with its owner | The shared-instance rule: one caller must not break an object that others use. |
| `lock (_lock)` around state | No synchronization | The skill: Blazor runs on a single-threaded synchronization context. |
| `GetValueAsync<T>(name, CancellationToken.None)` | The overload without a token | The skill: any token, even `None`, turns off the default JS interop timeout, so with the prototype a stuck call never times out. |
| `DisposeAsync` awaits a pending `headers` acquisition | Disposal releases only a completed acquisition | The skill and #69845: waiting could hold up scope disposal until the interop timeout. |
| A failed acquisition cleared in a `catch` | Checking `IsFaulted` or `IsCanceled` on the next call | `CookieStore.GetReferenceAsync()` |
| `string? baseUrl = null` on `UrlConstructor.CreateAsync` | Overloads for optional arguments | The type mapping's overload rules |
| One-line `<summary>` docs, no `<param>` or `<returns>` | The full doc forms of `CookieStore` | The skill, and the review bot on #69305 |
| Result text checked directly after a click | The `data-run` counter, and "<operation>: <result>" text | #69845, after Copilot review found stale results passing |
| No `CancellationToken` on `FetchAsync` | A token mapped to `AbortController`, verified end to end | The proposal already does this, and the prototype skill asked for it. |
