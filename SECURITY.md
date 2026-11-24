# Security Policy

## Supported versions

Only the latest release gets security fixes.

## Reporting a vulnerability

Please report security problems privately, through [GitHub's private vulnerability reporting](https://github.com/mrnil/xkcd-viewer/security/advisories/new), not in a public issue. Include what you found, how to reproduce it, and what you think the impact is.

This is a one-person hobby project, so there's no guaranteed response time, but reports are acknowledged as soon as they're read. Fixes are released as a new patch version, and the advisory is published once the fix is out.

## What the plugin does that matters for security

- It makes HTTPS requests to `xkcd.com` and `imgs.xkcd.com` only. It doesn't follow redirects, every request has a timeout, and responses are size-capped.
- Everything it receives from xkcd is treated as untrusted and validated before use.
- The only URLs it opens in your browser are `https://xkcd.com/{number}/` pages.
- It stores three comic numbers in its local Logi settings, and nothing else. It sends no analytics and has no accounts.

See [`docs/ai/secure-coding.md`](docs/ai/secure-coding.md) for the rules the code follows, and the [EULA](EULA.md) for what is sent and stored.

## How releases are built

Releases are built, tested and packed by GitHub Actions from a tagged commit on `main` that has already passed CI (see [`docs/ai/release-process.md`](docs/ai/release-process.md)). Every action is pinned to a commit SHA, and the Logi Options+ installer and `PluginApi.dll` used for the build are checked for a valid Logitech signature. The repo's pre-commit hook scans commits for secrets with gitleaks.
