Be concise and token-efficient. Give direct answers, minimal examples, and no extra background.
No sycophantic openers or closing fluff. No emojis or em-dashes.

These rules apply to every task in this project unless explicitly overridden.
Bias: caution over speed on non-trivial work. Use judgment on trivial tasks.

## Rule 1 — Think Before Coding
State assumptions explicitly. If uncertain, ask rather than guess.
Present multiple interpretations when ambiguity exists.
Push back when a simpler approach exists.
Stop when confused. Name what's unclear.

## Rule 2 — Simplicity First
Minimum code that solves the problem. Nothing speculative.
No features beyond what was asked. No abstractions for single-use code.
Test: would a senior engineer say this is overcomplicated? If yes, simplify.

## Rule 3 — Surgical Changes
Touch only what you must. Clean up only your own mess.
Don't "improve" adjacent code, comments, or formatting.
Don't refactor what isn't broken. Match existing style.

## Rule 4 — Goal-Driven Execution
Define success criteria. Loop until verified.
Don't follow steps. Define success and iterate.
Strong success criteria let you loop independently.

## Rule 5 — Use the model only for judgment calls
Use me for: classification, drafting, summarization, extraction.
Do NOT use me for: routing, retries, deterministic transforms.
If code can answer, code answers.

## Rule 6 — Token budgets are not advisory
Per-task: 4,000 tokens. Per-session: 30,000 tokens.
If approaching budget, summarize and start fresh.
Surface the breach. Do not silently overrun.

## Rule 7 — Surface conflicts, don't average them
If two patterns contradict, pick one (more recent / more tested).
Explain why. Flag the other for cleanup.
Don't blend conflicting patterns.

## Rule 8 — Read before you write
Before adding code, read exports, immediate callers, shared utilities.
"Looks orthogonal" is dangerous. If unsure why code is structured a way, ask.

## Rule 9 — Tests verify intent, not just behavior
Tests must encode WHY behavior matters, not just WHAT it does.
A test that can't fail when business logic changes is wrong.

## Rule 10 — Checkpoint after every significant step
Summarize what was done, what's verified, what's left.
Don't continue from a state you can't describe back.
If you lose track, stop and restate.

## Rule 11 — Match the codebase's conventions, even if you disagree
Conformance > taste inside the codebase.
If you genuinely think a convention is harmful, surface it. Don't fork silently.

## Rule 12 — Fail loud
"Completed" is wrong if anything was skipped silently.
"Tests pass" is wrong if any were skipped.
Default to surfacing uncertainty, not hiding it.

# NEventStore.Persistence.Sql Project Guidelines

## Scope And Priorities

- Prefer edits under `src/` unless explicitly asked to change tooling/docs.
- Do not modify `dependencies/NEventStore` unless the task explicitly requires submodule changes.
- Follow GitFlow practices used by this repo (see [README.md](README.md)).

## Build And Test

- Restore/build solution:
  - `dotnet restore ./src/NEventStore.Persistence.Sql.Core.sln --verbosity m`
  - `dotnet build ./src/NEventStore.Persistence.Sql.Core.sln -c Release --no-restore /p:ContinuousIntegrationBuild=true`
- Run tests:
  - `dotnet test ./src/NEventStore.Persistence.Sql.Core.sln -c Release --no-build`
- Full packaging script (interactive):
  - `./build.ps1`

See [build.ps1](build.ps1), [appveyor.yml](appveyor.yml), and [README.md](README.md) for CI/local details.

## Test Environment Prerequisites

- Integration tests require running DB engines and connection-string environment variables (`NEventStore.MsSql`, `NEventStore.MySql`, `NEventStore.PostgreSql`, optional Oracle).
- Docker helper scripts are in `docker/`.

Use [docker/Readme.md](docker/Readme.md) and [README.md](README.md) as the source of truth for environment setup.

## Isolated Database Environments

- Work only inside the assigned Git worktree.
- Use the repository `start-environment` and `stop-environment` scripts; use a dynamic environment by default.
- Do not use the reserved `debug`, `test`, or `ci` environments unless the task explicitly requires one.
- Never commit `.env.dynamic`, `.env.debug`, `.env.test`, or `.env.ci`.
- Never use global Docker cleanup commands.
- Do not stop, remove, inspect for mutation, or otherwise modify another worktree's Compose environment.

## Repository Map

- Core persistence library: `src/NEventStore.Persistence.Sql/`
- DB-specific test projects:
  - `src/NEventStore.Persistence.MsSql.Tests/`
  - `src/NEventStore.Persistence.MySql.Tests/`
  - `src/NEventStore.Persistence.PostgreSql.Tests/`
  - `src/NEventStore.Persistence.Sqlite.Tests/`
  - `src/NEventStore.Persistence.Oracle.Tests/`
- Shared extra tests: `src/AdditionalTests/`

## Code And Style Conventions

- Language/version settings are centralized in [src/Directory.Build.props](src/Directory.Build.props): C# 13, nullable enabled, implicit usings enabled.
- Formatting/rules are in [src/.editorconfig](src/.editorconfig): tabs, CRLF, and selected analyzer/style constraints.
- Keep async code patterns consistent with existing `*.Async.cs` partial files.

## Known Pitfalls

- PostgreSQL + Npgsql 6+ timestamp behavior requires special handling; check [README.md](README.md) and [Changelog.md](Changelog.md) before touching PostgreSQL dialect/schema behavior.
- `build.ps1` and CI use GitVersion and pack via `.nuspec`; avoid replacing packaging flow unless requested.
- Local Docker examples and CI credentials differ; when tests fail, verify connection strings/environment variables first.

## Before Submitting Changes

- Build the affected projects.
- Run the nearest relevant DB-specific test project(s) first, then broader solution tests if needed.
- Update docs when behavior, setup, or workflow changes.
