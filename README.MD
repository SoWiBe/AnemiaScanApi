# AnemiaScan API

ASP.NET Core Web API (.NET 10) for non-invasive anemia screening via image analysis and personalized recovery programs.

> **Medical disclaimer** — this API is a screening tool only and does not replace professional medical diagnosis. Always consult a qualified healthcare provider.

---

## Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Documentation](#api-documentation)
- [Testing](#testing)
- [Project Structure](#project-structure)
- [Contributing](#contributing)

---

## Features

| Area | Description |
|---|---|
| **Anemia Screening** | Upload conjunctiva / fingernail / palm images; ML model returns prediction + confidence score |
| **Auth** | JWT bearer + refresh tokens; email verification codes; BCrypt password hashing |
| **User Profile** | Profile CRUD, password change |
| **Course Catalog** | Published recovery programs; audience-aware recommendations based on scan history |
| **Enrollment & Streak** | Enroll in a course; mark daily tasks done; streak tracking (current / longest) |
| **Checkpoint Scans** | Attach a rescan to a checkpoint day to unlock day completion and track Hb progress |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10.0 (ASP.NET Core) |
| ML | ML.NET 5 + TensorFlow, `PredictionEnginePool` with hot-reload |
| Database | MongoDB 7 + GridFS (image storage) |
| Auth | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Logging | Serilog (console + rolling file) |
| Email | MailKit / SMTP |
| API Docs | Scalar (`MapScalarApiReference`) |
| Tests | xUnit 2.9 · Moq 4.20 · FluentAssertions 6.12 |

---

## Architecture

```
Request → Controller → Service → Repository → MongoDB
```

- **Controllers** (`Controllers/`) — thin; inherit `BaseSasController` for `Logger` and `GetUserId()`.
- **Services** (`Infrastructure/Services/`) — business logic; inherit `BaseService<T>`; throw `SASException(message, statusCode)` for domain errors.
- **Repositories** (`Infrastructure/Repositories/`) — MongoDB CRUD; inherit `BaseMongoRepository<T>`.
- **Middleware** (`Middleware/SASMiddleware.cs`) — catches `SASException` and maps it to the correct HTTP status; uncaught exceptions → 500.
- **DI wiring** — all registrations live in `Extensions/ServicesExtensions.AddServices()`; `Program.cs` only composes extensions.
- **Filters** — `ValidateImageAttribute` (size, extension, magic bytes); `ValidationCodeFilter` (email verification codes via `IMemoryCache`).

---

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- MongoDB 7+ instance (local or Atlas)
- SMTP server access (for email codes)

---

## Getting Started

```powershell
# 1. Clone
git clone https://github.com/SoWiBe/AnemiaScanApi.git
cd AnemiaScanApi

# 2. Supply secrets (see Configuration below)
dotnet user-secrets set "MongoDB:ConnectionString" "mongodb://localhost:27017" --project AnemiaScanApi

# 3. Restore & build
dotnet restore
dotnet build

# 4. Run
dotnet run --project AnemiaScanApi
```

The API starts on `https://localhost:5001` (or the port shown in the console).  
Interactive docs are available at `/scalar/v1` once running.

---

## Configuration

Secrets are managed via [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) (ID `73f44fb7-f595-4c2e-b926-c42c4ca31890`). The checked-in `appsettings.json` ships with empty values intentionally — never commit real credentials.

```powershell
# MongoDB
dotnet user-secrets set "MongoDB:ConnectionString" "<connection-string>" --project AnemiaScanApi
dotnet user-secrets set "MongoDB:DatabaseName"     "<db-name>"           --project AnemiaScanApi

# JWT
dotnet user-secrets set "JwtSettings:Secret"                      "<256-bit-secret>"  --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:Issuer"                      "<issuer>"          --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:Audience"                    "<audience>"        --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:AccessTokenExpirationMinutes" "60"               --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:RefreshTokenExpirationDays"   "30"               --project AnemiaScanApi

# SMTP / Email
dotnet user-secrets set "Smtp:Host"     "<smtp-host>" --project AnemiaScanApi
dotnet user-secrets set "Smtp:Port"     "587"         --project AnemiaScanApi
dotnet user-secrets set "Smtp:Username" "<user>"      --project AnemiaScanApi
dotnet user-secrets set "Smtp:Password" "<password>"  --project AnemiaScanApi
```

In production, supply the same keys as environment variables (`MongoDB__ConnectionString`, etc.) or via your platform's secret store.

### ML Model

The anemia model (`anemia_v10_more_aug.zip`) must be placed in `AnemiaScanApi/LLM/`. The loader looks one directory above the build output, so the path resolves automatically at dev time. Replace the file and restart to hot-reload a new model version.

---

## API Documentation

[Scalar](https://scalar.com) UI is wired in `Program.cs` via `MapScalarApiReference`. Once the API is running, open:

```
https://localhost:5001/scalar/v1
```

The OpenAPI JSON document is at `/openapi/v1.json`.

---

## Testing

```powershell
dotnet test
```

The test project (`AnemiaScanApi.Tests/`) uses **xUnit + Moq + FluentAssertions**. All tests are pure unit tests — repositories are mocked, no database connection required.

| Test class | Coverage |
|---|---|
| `StreakServiceTests` | Streak increment, reset, idempotency, longest-streak tracking |
| `CourseCatalogServiceTests` | Catalog listing, recommendation engine, scan-order correctness, day preview mapping |
| `CourseEnrollmentServiceTests` | Enroll (404/409/success), task completion, checkpoint-day guard, ownership guard |

---

## Project Structure

```
AnemiaScanApi.sln
├── AnemiaScanApi/                         # Main API project
│   ├── Common/                            # Domain models, enums, DTOs, constants
│   │   ├── Enums/                         # CourseContentStatus, EnrollmentStatus, TargetAudience, …
│   │   ├── Constants/                     # MongoCollection, ExceptionMessage, ModelName, …
│   │   ├── Requests/                      # Input DTOs (auth, profile, courses, …)
│   │   └── Responses/                     # Output DTOs
│   ├── Controllers/                       # HTTP surface
│   │   ├── Core/BaseSasController.cs
│   │   ├── AuthorizationController.cs
│   │   ├── AnalysisController.cs
│   │   ├── ProfileController.cs
│   │   ├── CoursesController.cs
│   │   └── CourseEnrollmentsController.cs
│   ├── Infrastructure/
│   │   ├── Core/                          # BaseMongoRepository<T>, IMongoRepository<T>
│   │   ├── Repositories/                  # MongoDB repository implementations + interfaces
│   │   ├── Services/                      # Business logic implementations + interfaces (Core/)
│   │   ├── Settings/                      # Strongly-typed config classes
│   │   ├── Exceptions/SASException.cs
│   │   └── Utils/                         # CodeGenerator, EmailSender
│   ├── Extensions/                        # DI wiring, JWT, Serilog, ML, SMTP setup
│   ├── Filters/                           # ValidateImageAttribute, ValidationCodeFilter
│   ├── Middleware/SASMiddleware.cs        # Global exception → HTTP response mapping
│   ├── docs/                              # ADRs and API spec
│   └── Program.cs
└── AnemiaScanApi.Tests/                   # Unit test project
    ├── Services/                          # Test classes
    └── Fixtures/                          # CourseBuilder, SasUserBuilder
```

---

## Contributing

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/your-feature`.
3. Keep commits small and descriptive.
4. Ensure `dotnet build` passes with zero errors and `dotnet test` is green.
5. Open a pull request against `master`.

---

## License

MIT — see [LICENSE](LICENSE) for details.

**Authors & Maintainers**

| | |
|---|---|
| Aleksey Gryzlov | [aleksey.dantist@gmail.com](mailto:aleksey.dantist@gmail.com) · [github.com/SoWiBe](https://github.com/SoWiBe) |
| Karim Abrahmanov | [karim.abrahmanov09@gmail.com](mailto:karim.abrahmanov09@gmail.com) · [github.com/dinsoul09](https://github.com/dinsoul09) |
