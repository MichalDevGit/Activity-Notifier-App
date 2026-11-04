# CLAUDE.md — Project Guidelines (read first)

## Read order for every agent / session
1. **This file** (guidelines and rules).
2. **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**: the exact map of the code. **Trust it.** Do NOT explore the whole repo.
   Open only the files the current task touches (paths are listed in ARCHITECTURE.md).
3. **[docs/PLAN.md](docs/PLAN.md)**: priorities and milestone status. Work only on the current milestone.

## What this project is
A Windows tray app (C#, .NET 10, WinForms) that tracks computer activity (input **counts** only) and notifies:
hourly water reminders (only while active), break suggestions, and finger exercises. Suggestions can be personalized with the Gemini free API.

**Main purpose: learning.** The owner is refreshing C# and must be able to **follow and fully understand every line**.
Learning goals: unit tests, events, input hooks, OS APIs, business logic, threads & synchronization, DB (EF Core + SQLite), security, AI API.

## Workflow rules (mandatory)
1. **Propose → approve → implement → review.** Before writing code for a milestone (or any change), describe what you will do and **wait for the owner's approval**. After implementing, hand over for review and stop.
2. Work in **small milestones** (see PLAN.md). Each milestone must build and pass its tests on its own.
3. **Update `docs/ARCHITECTURE.md` in the same change** as any code change that adds, removes, or moves components, interfaces, threads, tables, or files. Update the milestone status in `docs/PLAN.md`.
4. Stay in scope: P1 first. Do not add P2/P3 features or extra abstractions without approval.
5. Prefer simple, readable code over clever code. This is a learning project.

## Code conventions
- .NET 10, C# latest. `Nullable` enabled, `TreatWarningsAsErrors`, analyzers at `latest-recommended` (set in `Directory.Build.props`).
- Package versions are managed centrally in `Directory.Packages.props`.
- File-scoped namespaces. One public type per file. File name = type name.
- Classes are `sealed` unless designed for inheritance. Immutable data uses `record`.
- **Time:** never use `DateTime.Now`/`UtcNow`. Always inject `TimeProvider`. Store UTC, convert to local only for "clock hour" logic and display.
- Async methods end with `Async` and accept a `CancellationToken`. No `async void` except UI event handlers.
- No static mutable state. Dependencies come through constructor injection.
- **Comments for learning:** XML doc (`///`) on public types and members. Short *why* comments on non-obvious code (threading, interop, security). Do not comment obvious code.
- Core project has **no** Windows, UI, EF Core, or HTTP dependencies.

## Testing rules
- xUnit v3 + NSubstitute + `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`). Assertions use xUnit `Assert`.
- Test names: `Method_Scenario_ExpectedResult`.
- All business logic in Core must be covered by unit tests. Time-based tests advance `FakeTimeProvider`. Never `Thread.Sleep`.
- DB tests use SQLite **in-memory**. HTTP tests use a fake `HttpMessageHandler`. No real network in tests.

## Security rules (non-negotiable)
- **Never store, log, or transmit key identity** (virtual key codes, characters) or mouse coordinates. The hook callback only increments counters. Mouse movement is reduced to a distance number.
- Secrets (Gemini API key) are stored only with **DPAPI** (`ProtectedData`, `CurrentUser` scope). Never in source, `appsettings.json`, logs, or git.
- Data lives only in `%LOCALAPPDATA%\ActivityNotifier\`. No raw SQL string concatenation (use EF Core / parameters).
- AI requests over HTTPS only and contain **aggregated numbers only**. AI responses are untrusted: length-limited, plain text, fallback on anything invalid.
- The app runs as a standard user. Never require admin.
- Configuration is validated at startup (`ValidateOnStart`).

## Commands
All commands run from the repo root.
- Build: `dotnet build` (whole solution)
- Test: `dotnet test` (whole solution; uses Microsoft Testing Platform via `global.json`)
- Run: `dotnet run --project src/ActivityNotifier.App` (plain `dotnet run` fails at the root: the root has a solution, not a project)
- Vulnerable packages: `dotnet list package --vulnerable --include-transitive`
- Add migration: `dotnet ef migrations add <Name> --project src/ActivityNotifier.Infrastructure --startup-project src/ActivityNotifier.App`
