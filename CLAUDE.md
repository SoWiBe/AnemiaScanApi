# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

AnemiaScan MVP backend: ASP.NET Core Web API on **.NET 10.0** (`net10.0`, `x64`, nullable + implicit usings). The mobile app (Expo) lives in another repo; models are trained in the separate **AnemiaScanML** repo and only their artifacts (`.zip` / `.onnx` + cards) are copied here.

Solution `AnemiaScanApi.sln`: `AnemiaScanApi/` (the API) and `AnemiaScanApi.Tests/` (xUnit + FluentAssertions + Moq). No Dockerfile.

Production: `https://api-anemiascan.ru` — a self-hosted VPS in Kazakhstan (systemd unit `anemiascan-api`, nginx + TLS, self-hosted MongoDB). Deploy is automatic on push to `master` (`.github/workflows/deployapi.yml`); see `DEPLOYMENT.md`. Server addresses and IPs must not be written into the repo (it is public).

## Commands

Run from the repo root (containing the `.sln`):

```powershell
dotnet restore
dotnet build
dotnet test                                      # ~300 tests, ~10 s; 10 Mongo-backed tests are skipped unless TEST_MONGO_URL is set
dotnet run --project AnemiaScanApi               # launches the API
```

Secrets are stored via **User Secrets** (`UserSecretsId = 73f44fb7-f595-4c2e-b926-c42c4ca31890`). `appsettings.json` intentionally ships with empty `MongoDB`, `JwtSettings`, `EmailSender`, `Smtp`, and `CodeGenerator` values — supply them locally via `dotnet user-secrets set ...` or environment variables (`Section__Key` on the server), never by editing the checked-in file.

API docs (OpenAPI JSON + Scalar UI) are exposed **only in Development** or when `ApiDocs:Enabled=true`. Health probes: `/health` (liveness) and `/health/ready` (MongoDB + model files present).

## Architecture

Request flow: **Controller → Service (`Infrastructure/Services`) → Repository (`Infrastructure/Repositories`) → MongoDB**. Cross-cutting concerns are filters, attributes, middleware.

- **DI wiring** is centralized in `Extensions/` (`ServicesExtensions`, `JwtExtensions`, `LLMExtensions`, `SenderExtensions`, `LoggerExtensions`, `RateLimitingExtensions`, `HealthExtensions`). New services/repositories go into `ServicesExtensions.AddServices()`; `Program.cs` only composes extensions.
- **Base types** (do not roll your own):
  - Controllers → `Controllers.Core.BaseSasController` (`Logger`, `GetUserId()` from the `NameIdentifier` claim).
  - Services → `Infrastructure.Services.Core.BaseService<T>`.
  - Repositories → `Infrastructure.Core.BaseMongoRepository<T>` (`T : BaseMongoModel`). It receives the **singleton** `IMongoDatabase` from DI — never construct a `MongoClient` per repository/request.
  - Mongo entities → `Common.BaseMongoModel` (`[BsonId] Guid Id`).
- **Errors**: throw `Exceptions.SASException(message, statusCode)` from services; `Middleware/SASMiddleware.cs` turns it into a typed response (409/404/403/401/400 → same HTTP code, anything else → 500). Don't `try/catch` in controllers to build error JSON.
- **Auth**: JWT bearer (`JwtSettings`), BCrypt password hashes, 64-byte random refresh tokens stored on the user document. `[Authorize]` on actions. Identity always comes from the JWT, never from request body/query.
- **Email codes**: stored in MongoDB (`VerificationCodeStore`, collection `VerificationCodes`, one document per email, SHA-256 of the code, 5-minute lifetime checked on read plus a TTL index) so they survive a restart/deploy. `ICodeGenerator` uses a cryptographic RNG. `ValidationCodeFilter` expects `BaseAuthRequest.EmailCode`. Save the code before sending the email, not after.
- **Rate limiting**: policies in `Common/Constants/RateLimitPolicies.cs` (`anemia-prediction` per user, `email-codes` per IP). The app listens on loopback behind nginx; `UseForwardedHeaders` trusts exactly one proxy — keep it before `UseRateLimiter`.
- **Images**: `AnemiaScansRepository` stores a *compressed* copy in **MongoDB GridFS** (`anemia_scan_{Guid}`); predictions are computed from the original bytes. Filter `Filters/ValidateImageAttribute` exists and is registered in DI but is not currently applied to the upload action. The planned upload contract (PNG with alpha, outlined conjunctiva, `CaptureLight`, `DeviceModel`) is in `docs/image-upload-contract.md` and is **not enforced yet** — enable it only after the app release with outlining.
- **Logging**: Serilog. Sinks come **only** from the `Serilog` section of `appsettings.json` (`LoggerExtensions.AddLogging`); adding `WriteTo.*` in code duplicates every line. If the section is missing, a console sink is added as a fallback.
- **CORS**: single policy `DevCorsPolicy` (`http://localhost:8081`). Add a production policy instead of widening it.

## ML pipeline (`AnemiaScanApi/ML`, `LLM/`)

Two independent opinions about one photo, reconciled into one verdict (`ML/AnalysisVerdict.cs`):

1. **Classifier v13** (`LLM/anemia_v13_conjunctiva_full.zip` + `.card.json`): TensorFlow Inception features + LightGBM, loaded by `LLMExtensions.AddAnemiaPredictionModel()` via `PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput>` (`watchForChanges: false`, model name `ModelName.SasModel`). The **decision threshold (0.15) comes from the card**, not argmax: `AnemiaClassifier.Decide` → `ClassifierDecision(IsAnemic, AnemiaProbability, ClassifierVersion)`. Positive class is `Low_Hb`. The card is mandatory — a missing card fails startup. The zip and card are a pair; replace both together and update `LLMExtensions.ClassifierModelFile`, the `.csproj` `Content` items and `MlModelsHealthCheck`. LightGBM's native library needs `libgomp1` on Linux.
2. **CIELab Hb regression** (3 features L/a/b → boosting), two models chosen by age in `HemoglobinPredictionService`: child (`ML/hb_model.onnx`, ONNX, age 0–4) and adult (`ML/hb_model_adults.zip`, ML.NET, age 19–88; `HbMlNetPredictor`). Both implement `IHemoglobinModel`. `HemoglobinPrediction.InDomain` = age within the model's trained range **and** input segmented (transparent background, `ConjunctivaMask`). Out of domain → the number must not be shown as a measurement.
3. **Severity** (`ML/SeverityBands.cs`): WHO thresholds by sex/age, strictest scale if the profile is incomplete; the scale used is stored on the scan (`severity_reference`).
4. **Verdict**: the classifier decides; the regression only adds severity and an agreement flag (`Agree/Disagree/Unavailable`). Disagreement is shown, not hidden.

Honest numbers (out-of-fold CV on 215 adults, patient-level): classifier sensitivity 87.8% / specificity 59.2% at threshold 0.15 — about 4 of 10 healthy people get an "anemia" verdict, so UI wording must be "worth getting a blood test", never a diagnosis. Details in AnemiaScanML/README.md. Every analysis response carries the medical disclaimer (`MedicalDisclaimer`).

The API returns `anemiaProbability` (probability of `Low_Hb`, 0..1), not the old `confidence`. Old scan documents stay readable (`[BsonIgnoreExtraElements]`).

**Phone photos are an open blocker (measured 06.10.2026, `light-check` in AnemiaScanML).** On 22 real phone photos of 6 people both v13 and the retrained v14 answer "anemia" for everyone (v13 even for an all-black frame), and the probability jumps between two consecutive frames; the Hb number crosses the 12 g/dL line for 4 of 6 people. The dataset was shot with a close-up attachment, so CV numbers do not transfer. **Decision (11.10.2026): the photo verdict and the Hb number are not to be shown to users until the model is validated on phone photos of people with a lab hemoglobin** (protocol: `AnemiaScanML/PHONE_VALIDATION.md`). The API still returns them — hiding them in the response is not implemented yet. Related leaks to fix with it: `GET /courses/recommended` returns a course only when the last scan is "anemic", and enrollment progress builds its Hb timeline from the model's Hb.

## Courses

Course content lives in the repo: `AnemiaScanApi/Content/Courses/<slug>.json` (one file = one course; file name must equal the slug). There is no admin endpoint and no manual upload: `CourseContentSyncHostedService` syncs the folder into Mongo on every startup, so content reaches production only through git → PR → deploy.

- `Infrastructure/Courses/CourseDocumentParser` validates a file (mirrors `docs/schemas/course.schema.json`, plus what a schema can't express) and reports all errors at once with field paths. Unknown fields are errors.
- `CourseImportService` upserts by slug. **Task ids survive re-import** (matched by day + position, or an explicit `id`) because `CourseEnrollment` progress references them. Days are written before the catalog card; the card carries `content_hash`, written last, so an interrupted import is retried and an unchanged file causes no writes.
- A bad file is logged and skipped; it never blocks other courses or startup. Deleting a file does not delete the course — set `contentStatus: "Draft"` to hide it.
- `ShippedCourseContentTests` runs every file in the folder through the same validation — `dotnet test` is the pre-PR check for content.
- A `Published` course must not contain a day with no tasks and no rescan checkpoint: such a day can never be completed (see `CourseEnrollmentService.TryMarkDayCompleted`).

Author-facing format: `docs/course-content-schema.md`.

## Scans and lab ground truth

`AnemiaScan` (collection `AnemiaScans`) stores verdict, probability, classifier version, Hb, severity, agreement, model version. The user can attach the real lab result: `PUT /analysis/{scanId}/lab-hemoglobin` (g/dL, 3–25; date within ±14 days of the scan; ownership enforced in the Mongo filter). This is the only way to measure the models on real people. Scans live only in this collection: the copy that used to be embedded in the user document (`anemia_scans`) was removed on 12.10.2026, and `GET /profile/info` and course recommendation read scans from here. Known gap: `DeleteProfileAsync` deletes the user document but not the user's scans and GridFS images.

## Namespace layout quirks

Folder → namespace mapping is not strict:

- `Infrastructure/Services/*.cs` → `AnemiaScanApi.Services` (some files use `AnemiaScanApi.Infrastructure.Services`; check the neighbours)
- `Infrastructure/Settings/*.cs` → `AnemiaScanApi.Settings`
- `Infrastructure/Utils/*.cs` → `AnemiaScanApi.Utils` (and `.Utils.Core`)
- `Infrastructure/Exceptions/SASException.cs` → `AnemiaScanApi.Exceptions`

Match the surrounding files' pattern. `IAuthorizationService` / `IEmailSender` collide with framework types — `AuthorizationController.cs` disambiguates with `using ... = ...;`.

## Docs

- `docs/api-contract-changes.md` — changelog of the API contract for the frontend. **Update it in the same change whenever a request/response shape changes.**
- `docs/image-upload-contract.md` — photo format agreed with the frontend.
- `docs/plans/MVP_PLAN.md` — priorities (P0 numbers are referenced in code comments); `docs/adr/` — design decisions; `docs/course-content-schema.md` — course content format.
- `DEPLOYMENT.md` — server setup, deploy, troubleshooting.

## Working agreements

- Git: the only commit author is the user (Aleksey); **no `Co-Authored-By` trailers**. Commit messages in English.
- The app is not in production use yet, so API changes don't need backward-compat shims or deprecated fields — but tell the frontend via `docs/api-contract-changes.md`.
- Never commit photos of people's eyes (`light-check/`) or server details.
- Deferred on purpose by the user (don't push unprompted): SSH hardening, backups (receiver must be in Kazakhstan).
