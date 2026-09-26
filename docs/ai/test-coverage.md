# Test Coverage & Architecture Policy

Applies to `tests/xkcdPlugin.Tests/` (xUnit + Moq). That project doesn't exist yet. Create it with the first piece of business logic (see `tech.md` for the `PluginApi` reference gotcha).

## Overall Approach

TDD with xUnit + Moq. Write tests before or alongside the implementation.

- **Framework**: xUnit
- **Mocking**: Moq. Mock the interfaces around external dependencies (e.g. `IxkcdClient`) and the logger (`IPluginLog`)
- **Pattern**: Arrange-Act-Assert
- **Naming**: `MethodName_Condition_ExpectedBehaviour`

## Coverage Targets by Layer

| Layer | Target | Why |
|-------|--------|-----|
| Business logic / core functionality | 90%+ | Pure logic, no SDK dependency, cheap to test |
| Services (classes wrapping HTTP or other I/O) | 80%+ | Testable through injected interfaces or a fake `HttpMessageHandler` |
| `src/Actions/` (SDK commands/adjustments/folders) | Exempt from the numeric target | See below |
| Helpers | Full coverage | Usually pure functions (formatting, clamping, wrapping) |

## Why the Actions Layer Is Exempt

Classes in `src/Actions/` inherit from SDK base classes (`PluginDynamicCommand`, `PluginDynamicAdjustment`, `PluginDynamicFolder`, `ActionEditorCommand`). They:

- Need the Logi Plugin Service runtime to behave properly
- Reach the plugin through `xkcdPlugin.Instance`, which ties them to the plugin lifecycle
- Draw with `BitmapBuilder`, embedded resources and `PluginImageSize`
- Can't be unit tested meaningfully without mocking the whole framework

This is an accepted tradeoff, not a gap to close. Test only constructor wiring (name, display name, group) for these classes, not rendering. **The exemption covers SDK glue, not logic.** Pull pure logic out into tested helpers (`structure.md` pattern 5). The more code sits in `Actions/`, the more of this exemption is being abused.

## Test Patterns

### Arrange-Act-Assert with Moq

```csharp
[Fact]
public async Task GetComicAsync_WhenClientReturnsComic_ReturnsIt()
{
    // Arrange
    this._mockClient.Setup(x => x.GetComicAsync(353)).ReturnsAsync(new xkcdComic { Num = 353, Title = "Python" });

    // Act
    var result = await this._service.GetComicAsync(353);

    // Assert
    Assert.Equal("Python", result.Title);
}
```

### Always Test Three Paths

For every service method, test:

1. Happy path (succeeds)
2. Unavailable path (offline, 404, bad input): returns the safe default, and no downstream call where one shouldn't happen
3. Exception path: logs an error with context (entity + exception message) and returns the safe default

### Testing an HTTP Client Wrapper

The class that calls `https://xkcd.com` directly needs a fake `HttpMessageHandler`, not a mock of itself. Mocking `IxkcdClient` tests the *caller*, and never runs the client's own URL building, status handling or JSON mapping.

1. Give the client a second, `internal` constructor that takes an `HttpClient`. Production code uses the public constructor with a single shared static `HttpClient`. Expose the internal constructor to tests with `[assembly: InternalsVisibleTo("xkcdPlugin.Tests")]` (e.g. in `src/Helpers/AssemblyInfo.cs`) instead of making it public.
2. Write a small `StubHttpMessageHandler` that records every `HttpRequestMessage` and returns a response supplied by the test.
3. Assert on the recorded request (URL) and the parsed result. Cover what a mock can't: the exact URL for "latest" vs a numbered comic, 404 handling, malformed or missing JSON fields, and that the wire JSON maps onto the model.

```csharp
var handler = new StubHttpMessageHandler(request =>
    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
var client = new xkcdClient(new HttpClient(handler));

var comic = await client.GetComicAsync(353);

Assert.Equal("https://xkcd.com/353/info.0.json", handler.Requests[0].RequestUri.ToString());
```

Keep realistic JSON fixtures, captured from the live API, in the test project instead of hand-written approximations.

### Async and Timing

- **Wait for outcomes, don't sleep for them.** When the code under test exposes a completion signal (a returned `Task`, an event, a callback), await it or poll it with a bounded helper, e.g. `TestWait.UntilAsync(condition, because)`, which fails with what it was waiting for. A fixed `Thread.Sleep` is either too short (flaky) or too long (slow).
- **If fire-and-forget `Task.Run` is unavoidable**, keep any wait a named constant (`PluginTimings.TestAsyncDelay`), never a magic number.
- **Make time injectable.** Anything with a delay, timeout, cache expiry or "check every N minutes" should take its durations (or a clock) as constructor parameters, so tests run in milliseconds.
- **Repeat before trusting.** Run a new concurrency test in a loop (`for i in $(seq 1 10); do dotnet test --no-build --filter ...; done`). A race that fails 1 run in 5 won't show up in one green run.

## Intentionally Not Unit-Tested

1. **`Actions/` classes beyond a constructor/wiring smoke test**: SDK-dependent, exempt per the policy above.
2. **SDK-bound rendering helpers** (`BitmapBuilder` code): keep them thin, with every decision (colours, text, font size) made in tested pure code.
3. **The main plugin class**: orchestration that needs the plugin runtime. Keep it thin enough that this is an acceptable gap.

## Running Tests

```bash
dotnet test
dotnet test --collect:"XPlat Code Coverage"
dotnet test --filter "FullyQualifiedName~xkcdClientTests"
```

## CI Note

Timing-based tests are flaky under CI's variable scheduling. Don't add a flaky suite to CI and normalize failures; fix the timing first (inject the clock or scheduler).
