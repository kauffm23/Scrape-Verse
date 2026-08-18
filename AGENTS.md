# Repository Guidelines

## Project Structure & Module Organization

`LabWatch.slnx` groups four .NET 10 projects under `src/`:

- `LabWatch.Core`: domain models, validation, hashing, drift detection, and deterministic matching.
- `LabWatch.Infrastructure`: Bright Data integration, ingestion, EF Core/SQLite persistence, and demo workflows.
- `LabWatch.Web`: the Blazor dashboard; pages live in `Components/Pages/` and static assets in `wwwroot/`.
- `LabWatch.Fixture`: the local V1/V2 advisory fixture used to demonstrate scraper drift and recovery.

Tests are in `tests/LabWatch.Tests/`. Supporting prompts and demo notes belong in `docs/`, reusable PowerShell helpers in `scripts/`, and checked-in sample payloads in `samples/`.

## Build, Test, and Development Commands

Run commands from the repository root:

```powershell
dotnet restore LabWatch.slnx
dotnet build LabWatch.slnx --configuration Release
dotnet test LabWatch.slnx --configuration Release
./scripts/start-local.ps1
```

`start-local.ps1` launches the dashboard at `http://localhost:5180` and the fixture endpoint at `http://localhost:5181/advisories`. Use `./scripts/set-fixture-layout.ps1 -Layout v2 -Token "..."` to exercise layout drift.

## Coding Style & Naming Conventions

Use four-space indentation and standard modern C# conventions: PascalCase for types, methods, and public members; camelCase for parameters and locals; and `I` prefixes for interfaces. Nullable reference types and implicit usings are enabled. Keep domain logic in `Core`, external systems and persistence in `Infrastructure`, and UI concerns in `Web`. The solution uses `LangVersion=latest` and treats compiler warnings as errors, so builds must be warning-free.

## Testing Guidelines

Tests use xUnit and follow the existing `*Tests.cs` naming pattern (for example, `ValidationTests.cs`). Add focused tests for success, failure, and boundary cases. Ingestion changes should cover quarantine behavior, idempotency, and preservation of last-known-good data. Run the full solution test command before submitting; no fixed coverage threshold is configured.

## Commit & Pull Request Guidelines

Recent history uses Conventional Commit prefixes such as `feat:` and `fix:`. Write imperative, scoped summaries, for example `fix: preserve accepted snapshot after validation failure`. Pull requests should explain the behavior change, list verification commands, link relevant issues, and include screenshots for Blazor UI changes. Call out schema, fixture, or configuration changes explicitly.

## Security & Configuration

Never commit API tokens, fixture admin tokens, PHI, or real laboratory inventory. Supply Bright Data and fixture credentials through environment variables described in `README.md`. Keep demo data synthetic and preserve the human approval gate for scraper healing.

## Hackathon

This project is being developed for a hackathon.

Before making implementation or architectural decisions, read
`HACKATHON.md` and ensure all work complies with its rules and constraints.