# Activity Notifier

A Windows system-tray application that helps people who work long hours at the computer stay healthy. It watches how actively you use the computer and reminds you, at the right moment, to drink water, take a break, or rest your hands.

> Independent project, designed and built end to end: architecture, business logic, Windows integration, persistence, and tests.

## The goal

Long, uninterrupted computer sessions lead to dehydration, eye strain, and repetitive strain injuries in the hands and wrists. Fixed-interval timers are easy to ignore because they fire even when you are away or already resting.

Activity Notifier reacts to **how you actually work**. It knows when you are active, when you stepped away, and how intensively you have been typing. It only interrupts you when a reminder is actually relevant.

## What it does

- **Hydration reminders:** a reminder every clock hour, but only if you were active during that hour.
- **Break suggestions:** a short micro-break after about 50 minutes of continuous work and a longer break after about 90 minutes. Stepping away resets the session automatically.
- **Finger and hand exercises:** suggested after intensive typing (for example, thousands of keystrokes within half an hour).
- **Smart scheduling:** one notification at a time, with cooldowns, a minimum gap between notifications, and *Done* / *Snooze* actions.
- **AI-personalized suggestions (planned):** exercise suggestions tailored to your activity pattern through the Google Gemini API, with a built-in fallback when offline.
- **Activity history (planned):** activity is stored locally per minute for daily statistics.

## Privacy by design

An app that listens to keyboard and mouse input has to earn trust, so privacy is built into the architecture:

- **No keylogging.** The app counts key presses but never records *which* keys were pressed. Mouse movement is reduced to a distance and coordinates are never stored.
- **Local data only.** All data stays on your machine in your user profile (`%LOCALAPPDATA%\ActivityNotifier\`).
- **Protected secrets.** The AI API key is encrypted with Windows DPAPI and never stored in plain text.
- **Minimal AI payload.** Only aggregated numbers are sent to the AI, over HTTPS. Responses are treated as untrusted input and validated.
- **No admin rights.** The app runs as a standard user.

## Technologies

| Area | Technology |
|---|---|
| Language & runtime | C#, .NET 10 |
| UI | Windows Forms (system tray, toast notifications) |
| Application structure | .NET Generic Host, dependency injection, Options pattern with startup validation, `ILogger` |
| Windows integration | Low-level keyboard/mouse hooks and Win32 APIs via P/Invoke (`LibraryImport`), `SystemEvents` (sleep, lock, shutdown) |
| Concurrency | Dedicated hook thread, `BackgroundService` + `PeriodicTimer`, `Interlocked`, `Lock`, `Channel<T>`, graceful shutdown |
| Data | Entity Framework Core + SQLite |
| AI | Google Gemini API via typed `HttpClient` |
| Testing | xUnit v3, NSubstitute, `FakeTimeProvider` for deterministic time-based tests |
| Code quality | Nullable reference types, warnings as errors, .NET analyzers, central package management, NuGet vulnerability audit |

## Current status

🚧 **In active development.**

- ✅ Activity tracking: sessions, idle detection, input counting
- ✅ Rules engine: hydration, micro/long breaks, finger exercises, cooldowns, snooze
- ✅ Background services, hosting, and single-instance handling
- ✅ Windows input hooks, mouse distance tracking, sleep/shutdown handling
- ⬜ Toast notifications with Done / Snooze (next)
- ⬜ Local database (EF Core + SQLite)
- ⬜ AI-personalized suggestions (Gemini) with secure key storage
- ⬜ Final security review

All business logic is covered by unit tests, including concurrency and time-based scenarios.

## Build and run

Requirements: Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet run --project src/ActivityNotifier.App
```
