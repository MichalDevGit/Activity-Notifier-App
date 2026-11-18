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

## Communicating with the owner (mandatory)
The owner is refreshing C# and is new to .NET tooling and modern C# syntax. Every explanation must be understandable without prior knowledge.

**Proposals (before code)**
- Start with the big picture: what each new piece is **for** and how the pieces work together. Only then the implementation details.
- Explain every new term in one plain sentence the first time it appears (e.g. "NuGet audit = a check of package versions against known vulnerabilities"), and contrast it with what it could be confused with.
- List open decisions explicitly, each with a recommended option and the reason. Wait for the owner's answers.
- Use concrete examples with real times and numbers (e.g. "09:00 start → 09:50 MicroBreak → 10:30 LongBreak"). For confusing cases, a small table of all combinations works best.

**Explaining code and answering questions**
- Explain in plain words what the code means and does. This helps the most. For new syntax (`is not { } x`, `out`, `??`, `=>`, records), also show the longer equivalent form.
- **Tell the flow as a story in words** (who calls whom, on which thread, what happens next). Diagrams and examples support the story; they do not replace it.
- **Focus on how C# and .NET work, not on business logic.** For each component, give its role and general workflow briefly. Go into detail only where there is something to learn about C# or .NET (threads, async, DI, events, patterns, why this way vs alternatives). Business-logic lines that teach nothing about C# get a sentence at most.
- **Don't repeat what was already explained.** Refer back to it instead. When a portion mixes new and familiar material, mark clearly what is new and important, so the owner can focus on it.
- Answer numbered questions using the same numbering. Link to exact files and lines.
- When the owner summarizes their own understanding, reply **short and precise**: confirm what is right and add only the refinements that matter.

**Hand-over (after code)**
- Give a suggested reading order of the changed files.
- State every deviation from the approved proposal explicitly, with the reason.
- Tests: do not ask the owner to review all of them. Highlight the single most interesting test, walk through it step by step, and suggest a "break it on purpose" exercise.

**Commits**
- Commit only after the owner says "approved". Claude makes the commit.
- Before committing: check the diff for leftover experiments, run `dotnet build` and `dotnet test`.
- Windows PowerShell 5.1 breaks commit messages that contain double quotes: write the message to a file and use `git commit -F <file>`. Never hide git's error output.

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
