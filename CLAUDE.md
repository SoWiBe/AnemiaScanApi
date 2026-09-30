# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core Web API on **.NET 10.0** (`net10.0`, `x64`, nullable + implicit usings enabled). Single project `AnemiaScanApi/AnemiaScanApi.csproj` under solution `AnemiaScanApi.sln`. No test projects, no Dockerfile.

## Commands

Run from the repo root (`AnemiaScanApi/`, containing the `.sln`):

```powershell
dotnet restore
dotnet build
dotnet run --project AnemiaScanApi              # launches the API
dotnet build AnemiaScanApi/AnemiaScanApi.csproj # build a single project
```

Secrets are stored via **User Secrets** (`UserSecretsId = 73f44fb7-f595-4c2e-b926-c42c4ca31890`). `appsettings.json` intentionally ships with empty `MongoDB`, `JwtSettings`, `EmailSender`, `Smtp`, and `CodeGenerator` values — supply them locally via `dotnet user-secrets set ...` or environment variables, not by editing the checked-in file.

API docs at runtime: OpenAPI JSON at `/openapi/{documentName}.json` and Scalar UI via `MapScalarApiReference` (mapped in `Program.cs`). The Swagger UI helper `UseCustomSwaggerUi()` exists in `ServicesExtensions` but is **not wired into the pipeline** — add `app.UseCustomSwaggerUi()` if you need the Swashbuckle UI.

## Architecture

Request flow: **Controller → Service (`Infrastructure/Services`) → Repository (`Infrastructure/Repositories`) → MongoDB**. Cross-cutting concerns run as ASP.NET filters, attributes, or middleware.

- **DI wiring** is centralized in `Extensions/` (`ServicesExtensions`, `JwtExtensions`, `LLMExtensions`, `SenderExtensions`, `LoggerExtensions`). New services/repositories must be registered inside `ServicesExtensions.AddServices()` — `Program.cs` only composes these extensions and does not know about individual types.
- **Base types** to inherit from (do not roll your own):
  - Controllers → `Controllers.Core.BaseSasController` (provides `Logger` and `GetUserId()` from the `ClaimTypes.NameIdentifier` JWT claim).
  - Services → `Infrastructure.Services.Core.BaseService<T>` (just an `ILogger<T>` field).
  - Repositories → `Infrastructure.Core.BaseMongoRepository<T>` where `T : BaseMongoModel`. It builds its own `MongoClient` per instance from `MongoDbSettings`.
  - Mongo entities → `Common.BaseMongoModel` (defines `[BsonId] Guid Id`).
- **Error handling** goes through `Middleware/SASMiddleware.cs`. Throw `Exceptions.SASException(message, statusCode)` from services to produce a typed `ExceptionResponse` (409/404/403/401/400 → matching HTTP; anything else → 500). Uncaught exceptions become 500s. Do not `try/catch` in controllers for the purpose of returning error JSON — let the middleware handle it.
- **ML model** is loaded by `LLMExtensions.AddAnemiaPredictionModel()` via `PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>` with `watchForChanges: true`. The model file path is computed as `Path.Combine(AppContext.BaseDirectory, "../LLM", "anemia_v10_more_aug.zip")` — i.e. **one directory above the build output**, resolving to the project's `LLM/` folder at dev time. If you move or rename the .zip, update this path. Registered model name constant is `Common.Constants.ModelName.SasModel`.
  - `AnemiaAnalysisService.WriteAnalyseAsync` treats `PredictedLabel == "Low_Hb"` as anemic; this label string is dictated by the trained model and should not be changed casually.
- **Auth**: JWT bearer configured in `JwtExtensions.AddJwtAuthentication` from the `JwtSettings` section (`Secret`, `Issuer`, `Audience`, `AccessTokenExpirationMinutes`, `RefreshTokenExpirationDays`; `ClockSkew = Zero`). Passwords hashed with **BCrypt.Net**. Refresh tokens are 64 random bytes (base64) stored on the `SasUser` document; `SignOutAsync` clears them. Controllers/actions requiring auth use `[Authorize]`.
- **Email verification codes** use `IMemoryCache` (registered in `AddServices()`) with a 5-minute absolute+sliding TTL. Cache key format is `{CodeGeneratorSettings.CacheKey}:{email.ToLower()}` — always produce/read it via `ICodeGenerator.GetCacheKey(email)` / `GenerateAlphanumericCode(email)`; the `ValidationCodeFilter` reads the same key and expects `BaseAuthRequest.EmailCode` on the request DTO.
- **Image storage**: `AnemiaScansRepository` writes uploads to **MongoDB GridFS** (bucket built from `MongoDbSettings.ConnectionString/DatabaseName`) with filename `anemia_scan_{Guid}`. Downloads use the same filename convention. Collections are named via `Common.Constants.MongoCollection` (`Users`, `AnemiaScans`).
- **File uploads** validated with `Filters/ValidateImageAttribute` (size, extension, MIME, magic bytes for JPEG/PNG/GIF/BMP/WebP). The filter looks up the file by an action-argument name (default `"image"`) — pass the actual parameter name if it differs. Use `FileExtensions.UseAsBytesAsync(IFormFile)` to materialize the uploaded stream.
- **Logging**: Serilog is configured in two places. `LoggerExtensions.AddLogging` bootstraps a console logger and swaps in the host logger; runtime sinks (console + rolling `logs/myapp.txt` and error-only `logs/errors.txt`) come from `Serilog` section of `appsettings.json`.
- **CORS**: single named policy `DevCorsPolicy` allowing origin `http://localhost:8081` only (dev-only). Add a production policy rather than widening this one.

## Namespace layout quirks

Folder → namespace mapping is not strict. Several files sit in `Infrastructure/...` folders but declare short namespaces:

- `Infrastructure/Services/*.cs` → `AnemiaScanApi.Services`
- `Infrastructure/Settings/*.cs` → `AnemiaScanApi.Settings`
- `Infrastructure/Utils/*.cs` → `AnemiaScanApi.Utils` (and `.Utils.Core`)
- `Infrastructure/Exceptions/SASException.cs` → `AnemiaScanApi.Exceptions`

Interfaces under `Infrastructure/Services/Core/` and `Infrastructure/Utils/Core/` do use the full folder namespace. When adding new types, match the surrounding files' pattern rather than assuming folder = namespace, otherwise DI registration in `ServicesExtensions` will need matching `using` changes. Note also the name collision on `IAuthorizationService` / `IEmailSender` (project's vs framework's) — `AuthorizationController.cs` disambiguates with `using ... = ...;` aliases; follow that pattern when consuming these.

## ADRs

Architecture notes and API design decisions live in `AnemiaScanApi/docs/adr/` (e.g. `001-use-mongodb-for-analysis-storage.md`, `api-specification.md`). Consult these before changing storage or API contracts.
