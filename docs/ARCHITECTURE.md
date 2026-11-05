# Architecture: Activity Notifier

> **Source of truth for the code structure.** Keep it exact. Update it in the same change as the code.
> Status legend: ⬜ planned · 🟨 in progress · ✅ implemented

## 1. Solution layout ✅ (M0)

```
ActivityNotifier.slnx               # Solution: lists all projects
├─ Directory.Build.props            # Nullable, TreatWarningsAsErrors, analyzers, ImplicitUsings, NuGet audit
├─ Directory.Packages.props         # Central package versions
├─ global.json                      # dotnet test uses Microsoft Testing Platform (required by xUnit v3 on .NET 10 SDK)
├─ .gitignore                       # Also ignores *.db and *.key (local data / secrets)
├─ src/
│  ├─ ActivityNotifier.Core/            net10.0           Domain, rules, tracker, interfaces (pure C#)
│  ├─ ActivityNotifier.Infrastructure/  net10.0           EF Core + SQLite, Gemini HTTP client
│  └─ ActivityNotifier.App/             net10.0-windows10.0.19041.0  WinForms tray, hooks, OS APIs, DPAPI, Host (composition root)
└─ tests/
   ├─ ActivityNotifier.Core.Tests/            xUnit: rules, tracker, engine, concurrency
   └─ ActivityNotifier.Infrastructure.Tests/  xUnit: repositories (SQLite in-memory), Gemini client (fake handler)
```

### Dependency rules
```
App ──► Infrastructure ──► Core
 └──────────────────────────► Core
Tests ──► the project they test
```
- **Core** references nothing (BCL only, including `TimeProvider`, `Channel`, `Lock`).
- **Infrastructure** implements Core interfaces (`IActivityRepository`, `IExerciseSuggester`).
- **App** is the only place with Windows APIs, WinForms, and DI wiring.

## 2. Runtime data flow

```
 [Keyboard/Mouse]
       │  WH_KEYBOARD_LL / WH_MOUSE_LL  (Hook thread)
       ▼
 InputHookService ──Interlocked++──► InputCounters
                                         │  every minute: Snapshot() (Interlocked.Exchange)
                                         ▼
                              MinuteAggregatorService ──► ActivityTracker.RecordMinute()
                                         │                      │ raises SessionStarted / SessionEnded
                                         │ Channel<ActivityMinute>
                                         ▼
                                  DbWriterService ──► IActivityRepository (EF Core / SQLite)

 SystemEventsMonitor (sleep, shutdown) ──► ActivityTracker.InterruptSession(reason)
   (screen lock does NOT end a session; a lock ≥ idle threshold ends it via the normal idle rule)

 RulesService (PeriodicTimer) ──► tracker.GetSnapshot() ──► RulesEngine.Evaluate()
                                                                │ raises NotificationRequested
                                                                ▼
                                                     NotificationCoordinator
                                                       │ IExerciseSuggester (Gemini → fallback Static)
                                                       │ INotifier (Toast, marshalled to UI thread)
                                                       └ Channel<NotificationRecord> → DbWriterService
 Toast buttons (Done / Snooze) ──► RulesEngine.Acknowledge / Snooze
```

## 3. Threading model

| Thread | Owner | Runs | Shared state touched | Sync technique |
|---|---|---|---|---|
| **UI thread** (STA) | `Program` / `TrayApplicationContext` | WinForms message loop, tray menu, showing toasts | none directly | others marshal in via `SynchronizationContext` / `Control.BeginInvoke` |
| **Hook thread** (dedicated, STA) | `InputHookService` | Own message loop + LL hook callbacks | `InputCounters` | Lock-free `Interlocked`. Last mouse point is only touched by this thread. |
| **Thread pool**: aggregator | `MinuteAggregatorService` (`BackgroundService`) | `PeriodicTimer` 1 min | `InputCounters` (read+reset), `ActivityTracker` | `Interlocked.Exchange` swap, tracker's `Lock` |
| **Thread pool**: rules | `RulesService` (`BackgroundService`) | `PeriodicTimer` 15 s | tracker snapshot, `RulesEngine` state | Immutable `ActivitySnapshot`, engine's `Lock` |
| **Thread pool**: DB writer | `DbWriterService` (`BackgroundService`) | Consumes channels, batches saves | DB | Single consumer. `IDbContextFactory` (new context per batch). |
| **SystemEvents thread** | `SystemEventsMonitor` | OS callbacks | `ActivityTracker` | tracker's `Lock` |
| **Toast activation** (OS callback thread) | `ToastNotifier` | Button clicks | `RulesEngine` | engine's `Lock` |

- **Single instance:** named `Mutex` (`Local\ActivityNotifier.SingleInstance`) in `Program.Main`.
- **AI calls:** `SemaphoreSlim(1,1)` in `NotificationCoordinator`. At most one in flight. Timeout via `CancellationTokenSource`.
- **Shutdown order:** cancel host token → unhook + stop hook thread → aggregator flushes last minute → channels `Complete()` → DB writer drains → exit.

## 4. Components

### Core (`src/ActivityNotifier.Core/`)
| Status | Component | Path | Responsibility |
|---|---|---|---|
| ✅ | `ActivityMinute` (record) | `Models/ActivityMinute.cs` | One minute bucket: `MinuteStartUtc`, `Keystrokes`, `Clicks`, `MouseDistancePx`; `MinuteEndUtc`, `IsActive` |
| ✅ | `ActivitySnapshot` (record) | `Models/ActivitySnapshot.cs` | Immutable "photo" for rules: `SessionStartUtc`, `LastActiveUtc` (kept after session end), `ContinuousDuration`, `RecentMinutes` (last 60 min), `IsInSession` |
| ✅ | `SessionEndReason` (enum) | `Models/SessionEndReason.cs` | `Idle`, `Sleep`, `Shutdown` (no `Lock`: see §6) |
| ✅ | `SessionStartedEventArgs`, `SessionEndedEventArgs` | `Models/*.cs` | Event data: start time / start, end, reason |
| ⬜ M2 | `RuleType` (enum) | `Models/RuleType.cs` | `Hydration`, `MicroBreak`, `LongBreak`, `Fingers` |
| ⬜ M2 | `NotificationRequest` (record) | `Models/NotificationRequest.cs` | Rule type + context numbers for the suggester |
| ✅ | `InputCounters` | `Tracking/InputCounters.cs` | Lock-free counters (`Interlocked`). `Snapshot(minuteStartUtc)` atomically reads and resets each counter, returns an `ActivityMinute`. |
| ✅ | `ActivityTracker` | `Tracking/ActivityTracker.cs` | Session detection (see §6). `RecordMinute`, `InterruptSession(reason)`, `GetSnapshot()`. Keeps last 60 min. Guarded by `System.Threading.Lock`; events `SessionStarted`, `SessionEnded` raised **outside** the lock. No clock: all times come from the minutes. |
| ⬜ | `IActivityRule` | `Rules/IActivityRule.cs` | `RuleType Type`, `int Priority`, `bool ShouldTrigger(ActivitySnapshot, DateTimeOffset nowUtc, RuleState state)` |
| ⬜ | `HydrationRule` | `Rules/HydrationRule.cs` | See §6 |
| ⬜ | `MicroBreakRule` | `Rules/MicroBreakRule.cs` | See §6 |
| ⬜ | `LongBreakRule` | `Rules/LongBreakRule.cs` | See §6 |
| ⬜ | `FingersRule` | `Rules/FingersRule.cs` | See §6 |
| ⬜ | `RulesEngine` | `Rules/RulesEngine.cs` | Evaluates rules by priority. Applies cooldowns, snooze, and "one notification at a time". Event `NotificationRequested`. `Acknowledge(RuleType)`, `Snooze(RuleType, TimeSpan)`. Guarded by `Lock`. |
| ✅ | `TrackingOptions` | `Options/TrackingOptions.cs` | `IdleThresholdMinutes` [1–60], default 5. (`ActiveNowMinutes` added in M2.) |
| ⬜ | `RulesOptions` | `Options/RulesOptions.cs` | Rule thresholds bound from `appsettings.json`, with DataAnnotations validation |
| ⬜ | `IActivityRepository` | `Abstractions/IActivityRepository.cs` | Save minutes, sessions, notifications. Query today's stats. |
| ⬜ | `IExerciseSuggester` | `Abstractions/IExerciseSuggester.cs` | `Task<string> SuggestAsync(NotificationRequest, CancellationToken)` |
| ⬜ | `INotifier` | `Abstractions/INotifier.cs` | Show a notification with Done/Snooze actions |
| ⬜ | `StaticExerciseSuggester` | `Suggestions/StaticExerciseSuggester.cs` | Built-in exercise texts per `RuleType` (fallback) |

### Infrastructure (`src/ActivityNotifier.Infrastructure/`)
| Status | Component | Path | Responsibility |
|---|---|---|---|
| ⬜ | `ActivityDbContext` | `Persistence/ActivityDbContext.cs` | EF Core context, 3 DbSets |
| ⬜ | Entities | `Persistence/Entities/*.cs` | `ActivityMinuteEntity`, `WorkSessionEntity`, `NotificationEntity` |
| ⬜ | `ActivityRepository` | `Persistence/ActivityRepository.cs` | Implements `IActivityRepository` via `IDbContextFactory` |
| ⬜ | Migrations | `Persistence/Migrations/` | Generated by `dotnet ef` |
| ⬜ | `GeminiExerciseSuggester` | `Ai/GeminiExerciseSuggester.cs` | Typed `HttpClient`, REST call. Validates response (length, plain text). Falls back to `StaticExerciseSuggester`. |
| ⬜ | `GeminiOptions` | `Ai/GeminiOptions.cs` | Model name, endpoint, timeout (no key here) |

### App (`src/ActivityNotifier.App/`)
| Status | Component | Path | Responsibility |
|---|---|---|---|
| 🟨 | `Program` | `Program.cs` | Single-instance mutex, builds Generic Host, runs WinForms loop. *M0: empty `[STAThread] Main` placeholder (starts and exits).* |
| ⬜ | `TrayApplicationContext` | `Ui/TrayApplicationContext.cs` | `NotifyIcon`, menu (Pause / Exit), tooltip |
| ⬜ | `NativeMethods` | `Interop/NativeMethods.cs` | P/Invoke: `SetWindowsHookEx`, `UnhookWindowsHookEx`, `CallNextHookEx`, `GetLastInputInfo`, message loop |
| ⬜ | `InputHookService` | `Input/InputHookService.cs` | Dedicated hook thread. Keeps hook delegates alive (GC). Increments `InputCounters` only. |
| ⬜ | `IdleDetector` | `Input/IdleDetector.cs` | `GetLastInputInfo` → time since last input |
| ⬜ | `SystemEventsMonitor` | `Os/SystemEventsMonitor.cs` | `SessionSwitch`, `PowerModeChanged` → tracker. Unsubscribes on dispose. |
| ⬜ | `ToastNotifier` | `Notifications/ToastNotifier.cs` | Implements `INotifier` (`Microsoft.Toolkit.Uwp.Notifications`). Handles button activation. |
| ⬜ | `DpapiSecretStore` | `Security/DpapiSecretStore.cs` | Encrypt/decrypt API key file with `ProtectedData` (CurrentUser) |
| ⬜ | `MinuteAggregatorService` | `Services/MinuteAggregatorService.cs` | `BackgroundService`: each minute counters → tracker + DB channel |
| ⬜ | `RulesService` | `Services/RulesService.cs` | `BackgroundService`: periodic rule evaluation |
| ⬜ | `NotificationCoordinator` | `Services/NotificationCoordinator.cs` | Handles `NotificationRequested`: suggest (SemaphoreSlim) → notify → record |
| ⬜ | `DbWriterService` | `Services/DbWriterService.cs` | `BackgroundService`: single consumer of channels, batched writes |
| ⬜ | `appsettings.json` | `appsettings.json` | Rules/Tracking/Gemini options (no secrets) |

## 5. Database (SQLite, `%LOCALAPPDATA%\ActivityNotifier\activity.db`)

| Table | Columns | Notes |
|---|---|---|
| `ActivityMinutes` | `MinuteStartUtc` (PK), `Keystrokes` int, `Clicks` int, `MouseDistancePx` int | Counts only. Inactive minutes are not stored. |
| `WorkSessions` | `Id` (PK), `StartUtc`, `EndUtc` (nullable), `EndReason` (nullable) | Index on `StartUtc` |
| `Notifications` | `Id` (PK), `SentUtc`, `Rule`, `Message`, `Source` (`Ai`/`Static`), `UserResponse` (nullable, P2) | Index on `SentUtc` |

All timestamps are UTC (`DateTimeOffset` stored as UTC ticks / ISO text).

## 6. Business rules (defaults, all configurable)

**Definitions**
- *Active minute*: a minute bucket with any input.
- *Active now*: last input ≤ 2 min ago.
- *Session*: one continuous stretch of work. Starts at the first active minute. Ends after idle ≥ **5 min**, sleep, or shutdown.
  - The recorded end time is **when it really ended** (end of the last active minute), not when it was detected.
  - **Screen lock does not end a session.** A short lock keeps it; a lock ≥ 5 min ends it through the idle rule.
  - **Gaps** (no minutes recorded, e.g. sleep): if an active minute arrives ≥ 5 min after the last activity, the old session is closed at its last activity and a new one starts.
  - Precision is **per minute** (accepted).

| Rule | Priority | Triggers when | Repeat / cooldown |
|---|---|---|---|
| 💧 Hydration | 3 | Active now **and** ≥ 2 active minutes in the current **local clock hour** **and** not yet reminded this clock hour | Once per clock hour. **No catch-up** for hours you were away. Returning mid-hour gives one reminder for the current hour. |
| 🚶 LongBreak | 1 (highest) | Continuous session ≥ 90 min | Every 90 min of continuous work |
| 🧍 MicroBreak | 2 | Continuous session ≥ 50 min (and LongBreak not due) | Every 50 min of continuous work |
| ✋ Fingers | 4 | ≥ 4000 keystrokes in the last 30 min | Cooldown 45 min |

**Engine policies**
- At most one notification per evaluation (highest priority wins).
- Minimum gap of 3 min between any two notifications.
- Snooze (default 10 min) suppresses that rule until it expires.
- A session ending (real break) resets break-rule counters.

## 7. Configuration (`appsettings.json`)
Sections: `Tracking` (IdleThresholdMinutes, ActiveNowMinutes), `Rules` (per-rule thresholds and cooldowns), `Gemini` (Model, Endpoint, TimeoutSeconds). Validated with `ValidateDataAnnotations().ValidateOnStart()`. **No secrets.** The API key is in the DPAPI-protected file `%LOCALAPPDATA%\ActivityNotifier\gemini.key`.

## 8. Packages
| Project | Packages |
|---|---|
| Infrastructure | `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options` |
| App | `Microsoft.Extensions.Hosting`, `Microsoft.Toolkit.Uwp.Notifications`, `System.Security.Cryptography.ProtectedData` |
| Tests | `xunit.v3`, `NSubstitute`, `Microsoft.Extensions.TimeProvider.Testing` (Core.Tests), `Microsoft.EntityFrameworkCore.Sqlite` (Infrastructure.Tests, M5). Test projects are `OutputType=Exe` and run on Microsoft Testing Platform, so `Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio` are not used. |

Packages are added to `Directory.Packages.props` only in the milestone that first uses them.
