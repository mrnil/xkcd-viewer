# Secure Coding Principles for Logi Actions SDK Plugins

Security principles for a Logi Actions SDK plugin that talks to an external service. See "This Plugin" at the end for what applies to xkcd specifically.

## Input Validation

* Validate all data received from the external integration before processing
* Use `String.IsNullOrEmpty()` for all string inputs from external sources
* Validate numeric inputs (ports, IDs, indices) are in the expected range
* Validate file paths before reading/writing (prevent path traversal)
* Handle JSON/response parsing exceptions gracefully with proper error messages

## Credentials and Tokens

* Never hardcode API keys, client secrets, OAuth tokens, or passwords in plugin code
* Store credentials/tokens using the platform's secure storage where available; if that isn't available, store them outside source control (plugin config directory, not the repo) and never commit them
* Never log passwords, tokens, or authorization headers — not even at Debug/Trace level
* Treat refresh tokens with the same care as passwords — a leaked refresh token is a standing compromise
* Read credentials from config/secure storage only when needed; don't cache them longer than necessary

## Error Handling and Logging

* Use try-catch blocks for all external I/O operations (file, network)
* Log errors with sufficient context but without sensitive data
* Sanitize file paths in logs to avoid exposing usernames
* Use centralized logging (`PluginLog` / injected `IPluginLog`) for all log operations
* Fail securely - disable functionality when errors occur rather than retrying blindly or falling back to an insecure path

## Memory Management

* Properly dispose of all `IDisposable` resources
* Unsubscribe from events in `Unload`/`Dispose` methods
* Use `using` statements for file operations
* Avoid memory leaks in long-running async operations

## Communication Security

* Prefer TLS (`https://`, `wss://`) for any connection that leaves the local machine
* If connecting to a local application over an unencrypted transport (e.g. a local WebSocket), restrict connections to localhost (`127.0.0.1` / `::1`) and validate that before connecting
* Validate server certificates — don't disable certificate validation to work around a connectivity issue
* Handle connection failures gracefully with retry logic (exponential backoff, not a tight loop)
* Respect the external API's rate limits; back off on `429`/throttling responses instead of hammering it

## File Operations

* Only read from known, documented configuration directories
* Only write user-generated files (screenshots, exports) to user-approved directories (Pictures/Documents/Desktop)
* Validate file paths before operations
* Use `Path.Combine()` for cross-platform path construction
* Handle file I/O exceptions gracefully

## General Practices

* Use managed code (.NET) for automatic memory management
* Avoid dynamic code execution
* Never execute OS commands from user input
* Use async/await properly to avoid blocking
* Validate connection/auth state before all external operations
* Use guard clauses for early validation

## This Plugin

The plugin reads a public, unauthenticated API (`https://xkcd.com/.../info.0.json`) and the images it links to. There are no credentials. The risks are untrusted input and resource use:

* Treat every JSON field as untrusted: numbers may be missing or out of range, and `title` / `alt` / `transcript` can be long and contain any Unicode, newlines or HTML entities. Validate, truncate and normalize before rendering or logging
* Only fetch images over `https://` from xkcd's own hosts (`imgs.xkcd.com`), never from an arbitrary URL taken from the payload without checking it
* Put a timeout on every request, and a maximum size on downloaded images, before decoding
* Cache responses (a published comic never changes) and poll for new comics rarely, at most every few minutes, rather than on every render or press
* Open links in the browser only for `https://xkcd.com/...` URLs the plugin built itself
