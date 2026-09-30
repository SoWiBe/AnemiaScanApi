# AnemiaScan API

[![Deploy](https://github.com/SoWiBe/AnemiaScanApi/actions/workflows/deployapi.yml/badge.svg)](https://github.com/SoWiBe/AnemiaScanApi/actions/workflows/deployapi.yml)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![MongoDB](https://img.shields.io/badge/MongoDB-8.0-47A248?logo=mongodb&logoColor=white)
![ML.NET](https://img.shields.io/badge/ML.NET-5.0-9B4F96?logo=dotnet&logoColor=white)
![ONNX Runtime](https://img.shields.io/badge/ONNX_Runtime-1.30-005CED?logo=onnx&logoColor=white)
![nginx](https://img.shields.io/badge/nginx-1.24-009639?logo=nginx&logoColor=white)
![Tests](https://img.shields.io/badge/tests-120_passing-2EA043?logo=xunit&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-blue)

ASP.NET Core Web API for non-invasive anemia screening from conjunctiva images, paired with
personalized recovery programs.

> **Medical disclaimer** — this API performs *screening*, not diagnosis. It is not a medical
> device and does not replace a clinical examination or a laboratory blood test. Every analysis
> response carries a disclaimer field; clients are expected to surface it alongside the result.

---

## Project status

Backend is deployed and serving traffic. The work in progress is model quality, not plumbing.

| Phase | Scope | State |
|---|---|---|
| **1 — Infrastructure & security** | Connection pooling, health probes, rate limiting, image downscaling, consent capture, disclaimer, production deployment | **Done**, except off-site backups |
| **2 — Model** | Honest sensitivity/specificity for the classifier, adult Hb thresholds, reconciling two models into one verdict | **In progress — critical path** |
| **3 — Content & client surface** | Course content, JSON importer, scan-history endpoint | Not started |
| **4 — Release** | Load testing, monitoring, store submission | Not started |

See [`AnemiaScanApi/docs/plans/MVP_PLAN.md`](AnemiaScanApi/docs/plans/MVP_PLAN.md) for the full
breakdown and [`AnemiaScanApi/docs/api-contract-changes.md`](AnemiaScanApi/docs/api-contract-changes.md)
for breaking changes clients need to track.

---

## Contents

- [Features](#features)
- [Machine learning](#machine-learning)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [API surface](#api-surface)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Deployment](#deployment)
- [Testing](#testing)
- [Project structure](#project-structure)
- [Contributing](#contributing)

---

## Features

| Area | Description |
|---|---|
| **Anemia screening** | Upload a conjunctiva image; returns a binary verdict with confidence, an estimated hemoglobin value, a severity band and a medical disclaimer |
| **Image storage** | Scans are downscaled to 1024 px and re-encoded to JPEG before landing in GridFS — models always run on the original upload |
| **Auth** | JWT bearer with refresh tokens, email verification codes, BCrypt password hashing |
| **Consent capture** | Policy version, timestamp and acceptance are written to the user document at sign-up; a dedicated endpoint re-signs a bumped policy |
| **Rate limiting** | Per-user limits on the CPU-heavy scan endpoint, per-IP limits on email dispatch |
| **Health probes** | Separate liveness and readiness endpoints; readiness pings MongoDB and verifies model files |
| **Course catalog** | Published recovery programs with audience-aware recommendations driven by scan history |
| **Enrollment & streaks** | Daily tasks, completion tracking, current and longest streak |
| **Checkpoint scans** | Rescans attached to checkpoint days unlock progression and build an Hb timeline |

---

## Machine learning

Two independent models run on every scan.

| Model | Runtime | Output | Source |
|---|---|---|---|
| Anemia classifier | ML.NET + TensorFlow, `PredictionEnginePool` with hot-reload | Binary `Low_Hb` verdict + confidence — this is what drives `IsAnemic` | `LLM/anemia_v10_more_aug.zip` |
| Hemoglobin regression | ONNX Runtime, singleton `InferenceSession` | Hb value in g/dL + WHO severity band | `ML/hb_model.onnx` |

The regression path extracts CIELab features from the conjunctiva region in C#
(`ML/CielabFeatureExtractor`), reproducing the Python training pipeline bit-for-bit — golden
fixtures in the test project guard against drift between the two implementations.

### Model status — read before quoting numbers

- The Hb regression scores **MAE 1.518 g/dL, r = 0.387** on a leakage-free protocol over 394
  CP-AnemiC records. That is a weak correlation and should not be presented as a measurement.
- The TensorFlow classifier — the model that actually produces the user-facing verdict — has
  **no published holdout metrics yet**. Establishing honest sensitivity and specificity is the
  project's critical path; the target is sensitivity ≥ 0.85.
- Severity thresholds (7 / 10 / 11 g/dL) come from the WHO scale for children aged 6–59 months,
  matching the training data. **Adult thresholds are not yet implemented**, so adult results are
  currently scored on a pediatric scale.

Details and the reasoning behind these choices live in
[`docs/adr/002-cielab-hb-regression-model.md`](AnemiaScanApi/docs/adr/002-cielab-hb-regression-model.md).

---

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10.0 (ASP.NET Core), `x64` |
| ML | ML.NET 5 + TensorFlow redist · ONNX Runtime 1.30 |
| Imaging | SixLabors.ImageSharp 3.1 (CIELab extraction, downscaling) |
| Database | MongoDB 8.0 + GridFS |
| Auth | JWT Bearer · BCrypt.Net-Next |
| Logging | Serilog — console plus rolling file sinks |
| Email | MailKit / SMTP |
| API docs | Scalar + Swashbuckle, development only |
| Reverse proxy | nginx with Let's Encrypt TLS |
| Tests | xUnit 2.9 · Moq 4.20 · FluentAssertions 6.12 |

---

## Architecture

```
Request → nginx (TLS) → Kestrel → Controller → Service → Repository → MongoDB
```

- **Controllers** (`Controllers/`) stay thin and inherit `BaseSasController` for `Logger` and `GetUserId()`.
- **Services** (`Infrastructure/Services/`) hold business logic, inherit `BaseService<T>`, and throw
  `SASException(message, statusCode)` for domain errors.
- **Repositories** (`Infrastructure/Repositories/`) wrap MongoDB access and inherit `BaseMongoRepository<T>`.
  The `IMongoClient`, `IMongoDatabase` and GridFS bucket are registered as singletons — a client per
  scoped repository meant a TLS handshake on every request.
- **Middleware** (`Middleware/SASMiddleware.cs`) maps `SASException` to the right HTTP status;
  anything uncaught becomes a 500. Controllers never catch exceptions to shape error JSON.
- **DI wiring** lives entirely in `Extensions/`. `Program.cs` composes those extensions and knows
  nothing about individual types.
- **Filters** — `ValidateImageAttribute` checks size, extension, MIME type and magic bytes;
  `ValidationCodeFilter` validates email codes held in `IMemoryCache`.

Behind nginx the app trusts `X-Forwarded-For` from loopback only, so per-IP rate limits partition
by real client address rather than bucketing every caller into `127.0.0.1`.

---

## API surface

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `GET` | `/health` | — | Liveness; no dependency checks |
| `GET` | `/health/ready` | — | Readiness; pings MongoDB and verifies model files |
| `GET` | `/legal` | — | Current policy version, policy URL and disclaimer text |
| `POST` | `/authorization/sign-up/` | — | Register. **Requires `consentAccepted`** |
| `POST` | `/authorization/sign-in/` | — | Email + password |
| `POST` | `/authorization/sign-in-by-code/` | — | Email + password + verification code |
| `POST` | `/authorization/refresh/` | — | Exchange refresh token |
| `POST` | `/authorization/sign-out/` | JWT | Revoke refresh token |
| `POST` | `/authorization/email/verify-registration/` | — | Validate registration data, send code |
| `POST` | `/authorization/email/send-code/` | — | Send a verification code |
| `POST` | `/authorization/verification-code/` | — | Check a code |
| `POST` | `/authorization/update-password/` | — | Reset password with a code |
| `POST` | `/analysis/anemia/prediction/` | JWT | Run a scan — rate limited |
| `POST` | `/analysis/anemia/prediction/schedule` | JWT | Same, background variant |
| `GET` | `/profile/info/` | JWT | Profile DTO including recorded consent |
| `PATCH` | `/profile` | JWT | Update profile fields |
| `POST` | `/profile/consent/` | JWT | Accept the current policy version |
| `GET` | `/courses` | JWT | Published catalog |
| `GET` | `/courses/recommended` | JWT | Recommendation based on the latest scan |
| `GET` | `/courses/{slug}` | JWT | Course detail |
| `POST` | `/courses/{slug}/enroll` | JWT | Enroll |
| `GET` | `/courses/enrollments` | JWT | Current enrollments |
| `GET` | `/courses/enrollments/{id}/today` | JWT | Today's theory and tasks |
| `POST` | `/courses/enrollments/{id}/days/{day}/tasks/{taskId}/complete` | JWT | Mark a task done |
| `POST` | `/courses/enrollments/{id}/days/{day}/checkpoint-scan` | JWT | Attach a rescan to a checkpoint |
| `GET` | `/courses/enrollments/{id}/progress` | JWT | Progress and Hb timeline |

Scans and email dispatch return `429` with a `Retry-After` header when a limit is hit.

---

## Getting started

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download), a MongoDB 8 instance, and
SMTP credentials for verification emails.

```powershell
git clone https://github.com/SoWiBe/AnemiaScanApi.git
cd AnemiaScanApi

# Supply secrets — see Configuration below
dotnet user-secrets set "MongoDB:ConnectionString" "mongodb://localhost:27017" --project AnemiaScanApi
dotnet user-secrets set "MongoDB:DatabaseName"     "SmartAnemiaScan"           --project AnemiaScanApi

dotnet restore
dotnet build
dotnet run --project AnemiaScanApi
```

The API listens on `http://localhost:5034` in development. Interactive docs are at `/scalar/v1`
and the OpenAPI document at `/openapi/v1.json` — **both are disabled in production** unless
`ApiDocs__Enabled=true` is set.

---

## Configuration

Secrets are managed with [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
(ID `73f44fb7-f595-4c2e-b926-c42c4ca31890`). The checked-in `appsettings.json` ships with empty
values on purpose — never commit real credentials.

```powershell
# MongoDB
dotnet user-secrets set "MongoDB:ConnectionString" "<connection-string>" --project AnemiaScanApi
dotnet user-secrets set "MongoDB:DatabaseName"     "<db-name>"           --project AnemiaScanApi

# JWT — the secret must be at least 32 characters
dotnet user-secrets set "JwtSettings:Secret"                       "<256-bit-secret>" --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:Issuer"                       "<issuer>"         --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:Audience"                     "<audience>"       --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:AccessTokenExpirationMinutes" "60"               --project AnemiaScanApi
dotnet user-secrets set "JwtSettings:RefreshTokenExpirationDays"   "30"               --project AnemiaScanApi

# SMTP transport and sender account
dotnet user-secrets set "Smtp:Server"           "<smtp-host>" --project AnemiaScanApi
dotnet user-secrets set "Smtp:Port"             "587"         --project AnemiaScanApi
dotnet user-secrets set "EmailSender:Email"     "<address>"   --project AnemiaScanApi
dotnet user-secrets set "EmailSender:Password"  "<password>"  --project AnemiaScanApi

# Verification codes
dotnet user-secrets set "CodeGenerator:Length"   "6"                                --project AnemiaScanApi
dotnet user-secrets set "CodeGenerator:Chars"    "ABCDEFGHJKLMNPQRSTUVWXYZ23456789" --project AnemiaScanApi
dotnet user-secrets set "CodeGenerator:CacheKey" "verify_code"                      --project AnemiaScanApi
```

In production the same keys arrive as environment variables using double underscores
(`MongoDB__ConnectionString`, `JwtSettings__Secret`, …).

### Tunable at runtime

These have working defaults and can be changed without a rebuild:

| Key | Default | Purpose |
|---|---|---|
| `RateLimiting__PredictionPermitLimit` | `10` | Scans per user per window |
| `RateLimiting__PredictionWindowMinutes` | `1` | Scan window length |
| `RateLimiting__EmailCodePermitLimit` | `5` | Emails per IP per window |
| `RateLimiting__EmailCodeWindowMinutes` | `10` | Email window length |
| `ImageStorage__MaxDimension` | `1024` | Longest side of a stored scan, px |
| `ImageStorage__JpegQuality` | `85` | JPEG quality on re-encode |
| `Legal__PolicyVersion` | `1.0` | Written into every consent record |
| `Legal__PolicyUrl` | — | Returned by `GET /legal` |
| `Legal__DisclaimerText` | built-in text | Overrides the medical disclaimer |
| `ApiDocs__Enabled` | `false` | Exposes OpenAPI and Scalar in production |

### ML models

Both model files are copied to the build output and resolved relative to it:
`LLM/anemia_v10_more_aug.zip` and `ML/hb_model.onnx`. The TensorFlow model is registered with
`watchForChanges: true`, so replacing the file reloads it without a restart.

`SciSharp.TensorFlow.Redist` requires **AVX2**. Virtual machines exposing a generic `qemu64` CPU
model will crash with `Illegal instruction` on startup — verify with
`grep -o avx2 /proc/cpuinfo` before provisioning a host.

---

## Deployment

Pushing to `master` triggers [`deployapi.yml`](.github/workflows/deployapi.yml): publish for
`linux-x64`, ship the artifact over SSH, rewrite `.env` from repository secrets, and restart the
systemd unit. nginx terminates TLS in front of Kestrel; MongoDB runs on the same host bound to
loopback only.

Server provisioning, MongoDB configuration, backup scripts and health-check usage are documented
in [`DEPLOYMENT.md`](DEPLOYMENT.md). Reusable configuration lives in [`deploy/`](deploy/).

---

## Testing

```powershell
dotnet test
```

120 tests, no database or network required — repositories are mocked and ML assertions run
against checked-in fixtures.

| Test class | Coverage |
|---|---|
| `StreakServiceTests` | Streak increment, reset, idempotency, longest-streak tracking |
| `CourseCatalogServiceTests` | Catalog listing, recommendations, scan ordering, day preview mapping |
| `CourseEnrollmentServiceTests` | Enroll (404/409/success), task completion, checkpoint guard, ownership guard |
| `ProfileControllerTests` | `GET /profile/info` returns a DTO and never leaks the password hash or refresh token |
| `AuthorizationServiceConsentTests` | Sign-up rejects missing consent and stale policy versions |
| `ProfileServiceConsentTests` | Re-signing the current policy, version mismatch, unknown user |
| `AnemiaAnalysisServiceTests` | Compressed image reaches GridFS; disclaimer travels with the response |
| `ImageCompressorTests` | Downscaling, aspect ratio, pass-through for small and broken input |
| `CielabFeatureExtractorTests` | Golden comparison against the Python feature pipeline |
| `HbOnnxPredictorTests` | ONNX inference against golden Hb values |
| `HemoglobinPredictionServiceTests` | End-to-end regression path, graceful null on bad images |
| `SeverityBandsTests` | WHO severity band boundaries |

---

## Project structure

```
AnemiaScanApi.sln
├── AnemiaScanApi/                         # Main API project
│   ├── Common/                            # Domain models, enums, DTOs, constants
│   │   ├── Enums/                         # CourseContentStatus, EnrollmentStatus, TargetAudience, …
│   │   ├── Constants/                     # MongoCollection, MedicalDisclaimer, RateLimitPolicies, …
│   │   ├── Requests/                      # Input DTOs
│   │   └── Responses/                     # Output DTOs
│   ├── Controllers/                       # HTTP surface
│   ├── Infrastructure/
│   │   ├── Core/                          # BaseMongoRepository<T>, IMongoRepository<T>
│   │   ├── Repositories/                  # MongoDB access
│   │   ├── Services/                      # Business logic
│   │   ├── HealthChecks/                  # MongoDB and ML model probes
│   │   ├── Settings/                      # Strongly-typed configuration
│   │   ├── Exceptions/SASException.cs
│   │   └── Utils/                         # CodeGenerator, EmailSender, ImageCompressor
│   ├── ML/                                # CIELab extraction, ONNX predictor, severity bands
│   ├── LLM/                               # TensorFlow model artifact
│   ├── Extensions/                        # DI wiring, JWT, Serilog, ML, rate limiting, health
│   ├── Filters/                           # ValidateImageAttribute, ValidationCodeFilter
│   ├── Middleware/SASMiddleware.cs        # Exception → HTTP mapping
│   ├── docs/                              # ADRs, API contract changes, MVP plan
│   └── Program.cs
├── AnemiaScanApi.Tests/                   # Unit tests and golden fixtures
├── deploy/                                # mongod config, backup scripts, systemd units
├── DEPLOYMENT.md
└── .github/workflows/deployapi.yml
```

---

## Contributing

1. Branch from `master`: `git checkout -b feature/your-feature`.
2. Keep commits small and descriptive.
3. `dotnet build` must be error-free and `dotnet test` green before review.
4. Changing a response shape or a required field? Record it in
   [`docs/api-contract-changes.md`](AnemiaScanApi/docs/api-contract-changes.md) — the mobile client
   tracks that file.
5. Storage or API contract decisions belong in an ADR under `AnemiaScanApi/docs/adr/`.
6. Open a pull request against `master`.

---

## License

MIT.

**Authors & maintainers**

| | |
|---|---|
| Aleksey Gryzlov | [aleksey.dantist@gmail.com](mailto:aleksey.dantist@gmail.com) · [github.com/SoWiBe](https://github.com/SoWiBe) |
| Karim Abrahmanov | [karim.abrahmanov09@gmail.com](mailto:karim.abrahmanov09@gmail.com) · [github.com/dinsoul09](https://github.com/dinsoul09) |
