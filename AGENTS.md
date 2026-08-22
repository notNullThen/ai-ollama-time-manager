# Repository guidance

This file is the short, automatically loaded project briefing. The deeper knowledge cache is in `docs/PROJECT_CONTEXT.md`.

## Start here

- For broad changes, unfamiliar areas, architecture questions, or debugging across layers, read `docs/PROJECT_CONTEXT.md` first.
- For a narrow change, use the relevant section of that document as a map, then inspect the current source files before editing. Code is authoritative if the document is stale.
- Update `docs/PROJECT_CONTEXT.md` when a change alters architecture, state ownership, calculation invariants, AI tool behavior, dependencies, setup, or verification commands. Keep this file concise.

## Project essentials

- This is a single-project .NET 10 Blazor Server application. The main UI is `TimeCalculator/Components/Pages/Home.razor`.
- `TimeCalculatorProgramm` is the mutable in-memory domain model. `Home.razor` constructs and owns it; there is no database or persistence.
- `AiInteraction` connects to local Ollama through the `AIOrchestrator` package. `AiAppFacade` exposes time-entry mutations as LLM-callable tools.
- Both manual UI actions and AI tool calls mutate the same `TimeCalculatorProgramm` instance.
- Time entries are start markers. Their durations are derived from the next chronological entry; the last entry has zero duration.
- `TotalTimeLeftToWork` is currently a signed balance (`TotalWorkTime - target`), so it is negative while work remains. `SetRemainedTime()` relies on that sign. Do not “fix” the sign without updating the dependent behavior and UI together.

## Work safely

- Preserve the existing identifiers `TimeCalculatorProgramm` and `TimeSpanExtentions.cs` unless a task explicitly includes a coordinated rename.
- Do not edit generated `bin/` or `obj/` content, or vendored `TimeCalculator/wwwroot/lib/bootstrap/`, unless explicitly requested.
- Keep domain calculations in `Core`, Ollama/orchestration adaptation in `AiCore`, and rendering/event wiring in Razor components.
- AI settings and report entries are session/component memory only. Do not imply persistence without implementing it.
- Never add secrets or local credentials to repository files.

## Build and verification

Run commands from the repository root:

```bash
dotnet restore TimeCalculator.slnx
dotnet build TimeCalculator.slnx
dotnet run --project TimeCalculator
```

Ollama must be running for AI behavior; ordinary builds do not require it. The repository currently has no automated test project, so at minimum build after code changes and manually exercise affected Blazor/AI flows when relevant. A local CSharpier tool is declared in `dotnet-tools.json`; restore it with `dotnet tool restore` before formatting.
