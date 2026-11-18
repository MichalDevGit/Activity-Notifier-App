# Plan: Activity Notifier

**Goal:** refresh C# by building a focused Windows tray app. P1 must fit in ~3–4 days.
**Workflow:** for each milestone: propose → owner approves → implement → owner reviews → next.

## Priorities

### P1: Core (must have)
| # | Area | Scope | Learning goal |
|---|---|---|---|
| 1 | Business logic | Session detection; rules: Hydration, MicroBreak, LongBreak, Fingers; cooldowns, snooze, one-at-a-time | Business logic |
| 2 | Unit tests | xUnit v3 + NSubstitute + FakeTimeProvider for all Core logic | Unit tests |
| 3 | Events | Custom `EventHandler<T>` events (tracker, engine); correct subscribe/unsubscribe | Events |
| 4 | Input listening | Low-level keyboard/mouse hooks on a dedicated thread, counts only | Device listening |
| 5 | OS integration | `GetLastInputInfo`, `SystemEvents` (lock/sleep), tray icon, toasts with Done/Snooze | OS APIs |
| 6 | Threads & sync | `Interlocked`, `Channel`, `BackgroundService` + `PeriodicTimer`, `Lock` + snapshots, `Mutex`, graceful shutdown; concurrency tests | Threads & sync |
| 7 | Database | SQLite + EF Core, 3 tables, migration, batched writer, `IDbContextFactory`, in-memory tests | DB |
| 8 | Security baseline | No key identity, DPAPI secret, per-user data folder, strict build, NuGet audit, config validation | Security |
| 9 | AI | Gemini via typed `HttpClient`, aggregated data only, timeout + `SemaphoreSlim`, static fallback, fake-handler tests | AI API |
| 10 | Infrastructure | Generic Host, DI, Options, `ILogger` (minimal) | Professional structure |

### P2: Should have (only if P1 finishes early)
- Restore the current session from the DB after a restart
- Today's stats in the tray tooltip ("5h12m active, 6 cups")
- AI resilience (`Microsoft.Extensions.Http.Resilience`: retry, circuit breaker)
- "Start with Windows" option
- 🖱️ Mouse-heavy rule (forearm/wrist stretches)
- Save the user's response per notification (Done/Snoozed/Ignored)

### P3: Nice to have (later)
- Stats window with charts
- Settings window
- Do-not-disturb during fullscreen/presentations (`SHQueryUserNotificationState`)
- DB encryption (SQLCipher)
- Ollama as an alternative AI provider
- Installer / code signing
- WPF + MVVM version (far future)
- Server/client architecture: a small local or remote server (e.g. ASP.NET Core Web API) that owns the data and stats, with the tray app as a client (far future)

## Milestones

| Status | Milestone | Contents | P1 items | Est. |
|---|---|---|---|---|
| ✅ | **M0** | Docs (this file, ARCHITECTURE.md, CLAUDE.md) → owner review. Then: git init, solution, projects, `Directory.Build.props`, `Directory.Packages.props`, empty test projects, `.gitignore` | 8 (partial), 10 | 0.5 d |
| ✅ | **M1** | Core models, `InputCounters`, `ActivityTracker` (sessions, events, Lock, snapshots) + tests. `RuleType` / `NotificationRequest` moved to M2. | 1, 2, 3, 6 (partial) | 0.5 d |
| ✅ | **M2** | `RuleType`, `NotificationRequest`, 4 rules, `RulesEngine` (priority, cooldown, snooze, events) + tests | 1, 2, 3 | 0.5 d |
| ✅ | **M3** | Threading layer: Host, options validation, `MinuteAggregatorService`, `RulesService`, single-instance mutex, graceful shutdown, tray with Exit only, concurrency tests, new `App.Tests` project. `Channel<ActivityMinute>` moved to M5 (built with its consumer). | 6, 10 | 0.5 d |
| ⬜ | **M4** | Windows: P/Invoke hooks thread, `IdleDetector`, `SystemEventsMonitor`, tray, `ToastNotifier`, `StaticExerciseSuggester` → **first working app** | 4, 5 | 0.5–1 d |
| ⬜ | **M5** | EF Core + SQLite: entities, `DbContext`, migration, repository, `Channel<ActivityMinute>` (aggregator → writer), `DbWriterService` + in-memory tests | 7 | 0.5 d |
| ⬜ | **M6** | Gemini suggester, `DpapiSecretStore`, `NotificationCoordinator` with `SemaphoreSlim` + fake-handler tests | 9, 8 | 0.5 d |
| ⬜ | **M7** | Security review pass (checklist below), vulnerable-package check, final docs update | 8 | 0.25 d |

## Security checklist (verified in M7)
- [ ] No key codes / characters / coordinates stored, logged, or sent (code review + test)
- [ ] API key only via DPAPI; not in repo, config, or logs
- [ ] Data folder is `%LOCALAPPDATA%\ActivityNotifier\`
- [ ] AI payload = aggregated numbers only; response validated; HTTPS only
- [ ] Runs as standard user (`asInvoker`)
- [ ] Config validated on start
- [ ] `dotnet list package --vulnerable --include-transitive` is clean
- [ ] Build has no warnings (TreatWarningsAsErrors)
