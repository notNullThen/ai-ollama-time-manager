# Time Calculator project context

Last reviewed: 2026-08-22

This document is a durable codebase map for future development sessions. It summarizes stable architecture, behavior, and traps; it is not a substitute for reading the files being changed.

## Product and runtime

The application is a local agentic-AI demo that builds a working-day time report from either manual inputs or natural-language instructions. A local Ollama model decides which application functions to call; the `AIOrchestrator` NuGet package provides the orchestration and tool-execution loop.

Current technical shape:

- One ASP.NET Core/Blazor project: `TimeCalculator/TimeCalculator.csproj`
- Target framework: .NET 10 (`net10.0`)
- UI mode: Interactive Server, configured in `Program.cs` and `Components/App.razor`
- AI dependency: `AIOrchestrator` package version `0.1.2`
- CSS/layout: Bootstrap plus project styles in `wwwroot/app.css` and page/layout scoped CSS
- External AI service: Ollama, default URL `http://localhost:11434`
- Default model in code: `ministral-3:3b`
- Storage: none; time entries and AI settings live only in the current component/circuit memory

The README still mentions `gemma4:e4b` in parts of its setup/overview, while `Core/Types/AiSettings.cs` currently defaults to `ministral-3:3b`. Treat the code as authoritative unless intentionally reconciling the documentation.

## Repository map

```text
TimeCalculator.slnx                  Single-project solution
TimeCalculator/
  Program.cs                         DI, middleware, static assets, Blazor endpoint
  Core/                              Domain state and time calculations
    TimeCalculatorProgramm.cs        Central mutable report/editor state
    TimeSpanExtentions.cs            hh:mm formatting helpers
    Types/                            TimeEntry, TimeType, AiSettings
  AiCore/                            AIOrchestrator/Ollama adapter
    AiInteraction.cs                 Manager lifecycle, requests, cancellation, events
    AiAppFacade.cs                   LLM-visible tools, constraints, table serialization
    Types/AiTimeEntry.cs             AI-facing serialized row shape
  Components/
    Pages/Home.razor                 Composition root and report-state owner
    TimeEntryForm.razor              Manual editor plus AI prompt controls
    TimeEntriesTable.razor           Report rendering, replace, and remove actions
    AI/                              Settings, model picker, context/prompt debugging
    Features/                        Daily work-hours editor
    JsonViewer*.razor                Recursive AI-debug JSON display
    ThemeToggle.razor                Theme UI backed by ThemeService
    App.razor, Routes.razor          App shell, assets, routing, render mode
  Services/                          Scoped theme and browser-console logging bridges
  wwwroot/                           Project JS/CSS and vendored Bootstrap
Dockerfile                           .NET 10 multi-stage production build
docker-compose.yml                  Web container on host port 8080
dotnet-tools.json                    Local CSharpier formatter manifest
```

`bin/`, `obj/`, and `wwwroot/lib/bootstrap/` are generated or vendored and should normally be excluded from searches and edits.

## State ownership and UI flow

`Home.razor` is the effective composition root for the feature. It creates one `TimeCalculatorProgramm` with `DailyWorkHours = 8` and passes that same instance to its child components.

The main flow is:

1. `Home.razor` owns report state and top-level UI flags.
2. `TimeEntryForm.razor` mutates the current-entry editor directly for manual input and creates an `AiInteraction` over the same model.
3. `TimeEntriesTable.razor` reads the model and invokes replace/remove methods directly.
4. Child components raise callbacks so `Home.razor` calls `StateHasChanged()` after mutations.
5. AI context updates also trigger the form's `OnChanged` callback, refreshing totals and the table.

Consequences:

- Manual and AI entry creation share exactly the same domain methods and state.
- Refreshing/navigating away loses the report and edited AI settings.
- `TimeCalculatorProgramm` is not registered with dependency injection; the scoped services in `Program.cs` are only `ThemeService` and `IConsoleLogger`.
- The default route is `/`; the other pages are framework-style error/not-found endpoints.

## Domain model and calculation invariants

### Entries represent interval starts

`TimeEntry.Time` is a time-of-day marker. Its `Type` describes the interval from that marker until the next entry. `CalculateTime()` sorts entries by `Time`, then derives each `Duration` as `next.Time - current.Time`. The final row has zero duration because there is no following marker.

`TimeType` values are:

- `Work`: interval counts toward both total tracked time and total work time.
- `Break`: interval counts toward total tracked time but not total work time.
- `DayEnd`: terminal marker; its own interval is excluded from totals.

`TotalTime` is the sum of all derived durations except intervals whose current row is `DayEnd`. `TotalWorkTime` sums only `Work` intervals.

### Signed work balance

Despite its name, `TotalTimeLeftToWork` returns:

```text
TotalWorkTime - TimeSpan.FromHours(DailyWorkHours)
```

It is therefore negative until the target is reached, zero at the target, and positive for overtime. `SetRemainedTime()` depends on this convention: it creates a `DayEnd` marker at `last entry time - signed balance`, which adds the missing duration when the balance is negative.

Changing the sign or display semantics requires coordinated changes to `GetTimeLeftToWork()`, `SetRemainedTime()`, the AI `EndTheDay()` path, and the totals UI in `Home.razor`.

### Editing and mutation

- `CurrentTimeEntry` is a mutable editor buffer.
- `AddTimeEntry()` optionally converts the separate `Duration` (the UI's “Start in” input) into an absolute time relative to the last list item, clones the buffer into the list with a new ID, recalculates, and resets the buffer.
- `ReplaceEntryWithCurrent(id)` clones the editor buffer into the matched row while retaining that row's ID, then recalculates. It throws when the ID is absent.
- `RemoveTimeEntry(id)` removes matches and recalculates.
- Setting daily work hours triggers recalculation even though entry durations themselves do not depend on the target.

Inputs in `TimeEntryForm.razor` clamp hours to 23 and minutes to 59. Invalid numeric manual input becomes zero. AI time strings are parsed with `TimeSpan.Parse`; AI type strings use case-sensitive `Enum.Parse<TimeType>`.

## AI orchestration

### `AiInteraction`

`TimeEntryForm.razor` constructs `AiInteraction` with the shared `TimeCalculatorProgramm` and injected browser-console logger. `AiInteraction.Init()` creates an `AIOrchestrator.Core.AiManager` using:

- current `AiSettings.ModelName`
- current `AiSettings.BaseUrl`
- temperature `0.0`
- a three-minute Ollama HTTP timeout
- the current `AiAppFacade`

`AskAsync()` prevents concurrent runs with `IsBusy`, creates a cancellation token source, invokes `AiManager.StartAsync`, logs cancellation/errors, and raises busy-state events for UI refresh. `Cancel()` requests cancellation.

Changing model/settings calls `Init()` and replaces the manager, so previous AI conversation/context is discarded. Toggling “Multiple functions” recreates both the facade and manager for the same domain model and also discards previous AI context. The report entries remain because the shared `TimeCalculatorProgramm` is retained.

### `AiAppFacade`

The facade inherits `AiAppFacadeBase` and advertises three tools through `GetDescription()`:

- `AddTimeEntry(time, type, description)`
- `ReplaceEntry(guid, time, type, description)`
- `EndTheDay()`

The facade delegates to domain methods rather than maintaining separate data. `GetConstraints()` injects a freshly serialized time-entry table into the management prompt, allowing each orchestration step to see current application state. The AI-facing table uses `hh:mm` strings and string GUID/type fields.

When altering a tool signature or behavior, update all of these together:

- the callable facade method
- its `GetDescription()` metadata and parameter rules
- any constraints/context serialization affected by the change
- the underlying domain method and relevant UI refresh behavior

### Ollama model UI

`AI/AiSettings.razor` and `AI/ModelPicker.razor` both call `AIOrchestrator.OllamaClient.OllamaModels.GetModelsAsync(baseUrl)`. The settings panel supports filtering/sorting and applies its bound settings only by reinitializing the AI; it does not persist them. The compact picker changes the model immediately and reinitializes the AI.

The README notes that binding the web server for other devices and the Docker Compose route do not currently support AI. In those cases, `localhost` is resolved from the server/container rather than necessarily reaching the user's host Ollama service.

## Browser-side helpers and services

- `ThemeService` is scoped and bridges `ThemeToggle.razor` to `wwwroot/theme.js`. Theme mode (`system`, `dark`, or `light`) is the only persisted preference, stored in browser `localStorage` under `tc.theme`.
- `ConsoleLogger` keeps an in-memory log list and forwards messages to `wwwroot/logging.js` through JS interop.
- `wwwroot/utils.js` provides debug-panel copy, autoscroll, and model-button packing helpers.
- `wwwroot/interactive-check.js` reports Blazor interactivity diagnostics.
- `JsonViewer.razor` attempts to parse the AI context/prompt as one JSON value and falls back to raw text; `JsonNodeViewer.razor` renders parsed JSON recursively.

## Where changes usually belong

| Desired change | Primary files | Also inspect |
| --- | --- | --- |
| Time totals, durations, entry semantics | `Core/TimeCalculatorProgramm.cs`, `Core/Types/*` | `Home.razor`, `TimeEntriesTable.razor`, `AiAppFacade.cs` |
| Manual entry UX | `TimeEntryForm.razor` | domain model, `Home.razor` callbacks |
| New or changed AI tool | `AiCore/AiAppFacade.cs` | domain model, AI debug UI, package API |
| AI request lifecycle/cancellation | `AiCore/AiInteraction.cs` | `TimeEntryForm.razor` busy/stop controls |
| Ollama URL/model selection | `Core/Types/AiSettings.cs`, `Components/AI/*` | `AiInteraction.Init()`, README/container networking |
| Table replace/remove behavior | `TimeEntriesTable.razor` | editor-buffer semantics in the domain model |
| Theme or browser integration | `Services/ThemeService.cs`, `ThemeToggle.razor`, `wwwroot/theme.js` | `App.razor`, `app.css` |
| App startup/rendering | `Program.cs`, `App.razor`, `Routes.razor` | launch settings, Docker files |

## Build, run, and verification

From the repository root:

```bash
dotnet restore TimeCalculator.slnx
dotnet build TimeCalculator.slnx
dotnet run --project TimeCalculator
```

The launch profile serves HTTP on port 5211 and HTTPS on 7199. Docker Compose maps host port 8080 to container port 8080.

For AI smoke testing, run Ollama locally, ensure the configured model is pulled, open the app, select the model, submit a prompt, and verify both the table mutation and debug context. Cancellation, model changes, and the multiple-functions toggle are separate lifecycle paths worth checking when touched.

There is currently no test project and no CI configuration in this repository. `dotnet test TimeCalculator.slnx` therefore provides no meaningful behavioral coverage. At minimum, run `dotnet build TimeCalculator.slnx` after code changes. For calculation changes, adding focused unit tests around `TimeCalculatorProgramm` is preferable to relying only on UI smoke tests.

Formatting conventions live in `.editorconfig` (four spaces for C#, file-scoped namespaces preferred, standard .NET naming) and the repository declares CSharpier in `dotnet-tools.json`. Restore local tools with:

```bash
dotnet tool restore
```

## Known naming and maintenance notes

- `TimeCalculatorProgramm` has a double `m`, and the file `TimeSpanExtentions.cs` misspells “Extensions” even though the contained class is `TimeSpanExtensions`. These names are part of the current code map; rename them only as a deliberate coordinated cleanup.
- The core model exposes several public mutable fields as well as properties. Components currently depend on direct mutation, so encapsulation changes can have broad Razor impact.
- Bootstrap is checked into `wwwroot/lib/bootstrap`; do not search or reformat it during ordinary work.
- Keep this document high-signal. Update existing statements instead of appending a chronological diary, and remove facts that cease to be useful.
