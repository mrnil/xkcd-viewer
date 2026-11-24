# Code Commits

1. Commit after each completed change or implementation, not in large batches.
2. Commit messages use the [Conventional Commits](https://www.conventionalcommits.org/) format (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`, `build:`, `ci:`), with an optional lowercase scope (`feat(actions): ...`) and `!` for a breaking change (`feat(actions)!: ...`). `ci:` is for `.github/` changes.
3. The message says what changed **and why**. The diff already shows the what; the body is for the reason, the constraint or the alternative rejected.
4. Keep docs in step: a change to architecture, layout, dependencies or versions updates the matching `docs/ai/*.md` in the same commit.
5. Two git hooks in `.githooks/` check commits locally. Turn them on once per clone with `git config core.hooksPath .githooks`:
   - `commit-msg` rejects a subject line that isn't in the format above
   - `pre-commit` runs [gitleaks](https://github.com/gitleaks/gitleaks) over the staged changes and blocks a commit that adds a secret. It needs gitleaks installed (`winget install Gitleaks.Gitleaks`), and blocks every commit until it is
