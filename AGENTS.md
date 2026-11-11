# Repository Guidelines

## Project Structure & Module Organization

`AnemiaScanApi.sln` contains the .NET 10 API and `AnemiaScanApi.Tests/`. In `AnemiaScanApi/`, HTTP endpoints live in `Controllers/`, business logic in `Infrastructure/Services/`, MongoDB access in `Infrastructure/Repositories/`, and dependency registration in `Extensions/`. Keep controllers thin and follow the controller → service → repository flow. `ML/` and `LLM/` contain inference code and model artifacts; training happens in the separate AnemiaScanML repository. Course documents live in `Content/Courses/`; technical documentation is in `AnemiaScanApi/docs/`. Deployment instructions and scripts are in `DEPLOYMENT.md` and `deploy/`.

## Build, Test, and Development Commands

Run from the repository root with the .NET 10 SDK:

```powershell
dotnet restore                         # Restore dependencies
dotnet build                           # Build the solution
dotnet test                            # Run the test suite
dotnet run --project AnemiaScanApi      # Start the API locally
dotnet test --collect:"XPlat Code Coverage" # Collect coverage
```

The HTTP launch profile uses `http://localhost:5034`. Supply local configuration before starting the API.

## Coding Style & Naming Conventions

Follow surrounding C# code: four-space indentation, file-scoped namespaces, PascalCase types and methods, camelCase parameters and locals, and `Async` suffixes for asynchronous methods. Nullable reference types and implicit usings are enabled. Namespace mappings vary under `Infrastructure/`; match neighboring files. Reuse existing base classes, register dependencies through `Extensions/ServicesExtensions.cs`, and use `SASException` for service errors. No repository-wide formatter configuration was found.

## Testing Guidelines

Tests use xUnit, FluentAssertions, and Moq, with shared data under `AnemiaScanApi.Tests/Fixtures/`. Mirror feature folders, name classes `*Tests`, and use descriptive names such as `ConsecutiveDays_IncrementStreak`. Add regression tests for behavior changes. Mongo-backed tests require `TEST_MONGO_URL`; otherwise they are skipped. No minimum coverage threshold is configured. Run `dotnet test` before submitting; it also validates shipped course documents.

## Commit & Pull Request Guidelines

Use English messages with existing prefixes: `feat:`, `fix:`, or `refactor:`. Do not add `Co-Authored-By` trailers. PRs should describe the behavior change, relevant issues, configuration impacts, and validation results. Update `AnemiaScanApi/docs/api-contract-changes.md` whenever request or response shapes change.

## Security & Configuration

Use .NET User Secrets or environment variables (`Section__Key`); never commit credentials, server addresses, or personal eye photos. Keep model artifacts and their cards synchronized. Preserve medical disclaimers and the documented restriction on exposing unvalidated photo predictions to users; consult `CLAUDE.md` for details.
