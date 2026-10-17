# MarwanQuTech — Backend

ASP.NET Core (.NET 10) · Clean Architecture · CQRS (MediatR) · EF Core / SQL Server · Redis

## Requirements
- .NET SDK 10.0.100 or newer (`dotnet --version`)
- Visual Studio 2022 17.14+ / Rider 2025.2+ / VS Code + C# Dev Kit (for the `.slnx` solution format)

## Build & test
```bash
dotnet restore
dotnet build
dotnet test
```

## Layers (dependency direction →)
```
WebApi → Infrastructure → Application → Domain
```
| Folder / project | Assembly & root namespace | References | Status |
|---|---|---|---|
| `Domain/` | `MarwanQuTech.Domain` | — | ✅ Step 1 — entities, value objects, enums, domain events |
| `Application/` | `MarwanQuTech.Application` | Domain | ✅ Step 2 — CQRS handlers, validation, caching, auth flows |
| `Infrastructure/` | `MarwanQuTech.Infrastructure` | Application | ✅ Step 3 — EF Core/SQL Server, Redis, WebP storage, SMTP, JWT/BCrypt |
| `WebAPI/` | `MarwanQuTech.WebApi` | Application, Infrastructure | ⏳ Step 4 |
| `tests/Domain.UnitTests/` | `MarwanQuTech.Domain.UnitTests` | Domain | ✅ xUnit + Shouldly |
| `tests/Application.UnitTests/` | `MarwanQuTech.Application.UnitTests` | Application | ✅ validators, pipeline, invalidation, e-mail |
| `tests/Infrastructure.IntegrationTests/` | `MarwanQuTech.Infrastructure.IntegrationTests` | Infrastructure | ✅ real SQL Server (LocalDB) + full MediatR pipeline |

Shared build settings (`net10.0`, nullable, implicit usings) live in `Directory.Build.props`;
NuGet versions live only in `Directory.Packages.props` — never put `Version=` in a `.csproj`.

## Domain rules at a glance
- **Bilingual content** — `LocalizedText(Ar, En)`. Drafts need the Arabic title only;
  `Publish()` requires title, summary, body and English slug in both languages.
- **Slugs** — generated from titles when left empty; Arabic harakat / tatweel stripped.
  `Slug_EN` is `NULL` until an English version exists.
- **Publishing** — first `PublishedAt` is kept on re-publish; unpublishing a project also un-features it.
- **Gallery** — max 20 images per project, ordering always contiguous (0..n-1).
- **Contact form** — e-mail normalized, Arabic-Indic digits in phone numbers converted to Latin.
- **Admin auth** — lockout after N failed logins; refresh-token rotation with reuse detection
  (replaying an already-rotated token revokes every session; a token revoked by logout or a
  password change is just rejected). Only token hashes are stored.
- **Error codes** — every `DomainException` carries a stable `Code` (e.g. `Article.NotFullyLocalized`)
  for frontend translation.

## Application layer at a glance
- **Vertical slices** — `Features/<Area>/` holds commands, queries, validators, handlers and DTOs together.
- **Pipeline** — `LoggingBehavior` (slow requests, unexpected errors) → `ValidationBehavior` (FluentValidation → `RequestValidationException`).
- **Errors** — `AppException` subtypes carry a stable `Code`: `NotFoundException` 404, `ConflictException` 409
  (taken slug), `UnauthorizedException` 401, `LockedOutException` 423, `RequestValidationException` 400.
- **Public reads are cached** in Redis (`CacheKeys`); admin reads never are.
- **Invalidation** — publish/unpublish raise domain events; edits to published content call
  `IPublicContentInvalidator`, which clears the cache prefix and revalidates Next.js tags
  (`projects`, `project:{slug}`, `articles`, `article:{slug}`, `sitemap`). Failures are logged, never thrown.
- **Images** — saved first, then the DB row; if saving the row fails the new file is deleted;
  replaced/removed files are deleted only after SaveChanges succeeds.
- **Contact form** — honeypot field `Website`; notification e-mail HTML-encodes all visitor input.
- **Auth** — same error for unknown e-mail / wrong password, lockout → 423, refresh rotation saves
  in every outcome so reuse detection persists. Changing the password ends all other sessions.
- **Article views** — counted in Redis (`IArticleViewCounter`), flushed by `FlushArticleViewsCommand`;
  on a failed flush the counters are restored.


## Infrastructure at a glance
| Port | Implementation | Without configuration |
|---|---|---|
| `IApplicationDbContext` | EF Core + SQL Server, `LocalizedText` as complex types (`Title_AR` / `Title_EN` …) | — (connection string required) |
| `ICacheService` | Redis (JSON, prefix `mqt:`) | in-memory |
| `IArticleViewCounter` | Redis hash + per-visitor dedup keys, atomic drain (Lua) | in-memory |
| `IImageStorage` | SkiaSharp → WebP, resize, EXIF stripped, path-traversal safe | — |
| `IEmailSender` | MailKit SMTP | logged, not sent |
| `IRevalidationService` | `POST {NextJs:BaseUrl}/api/revalidate` + `x-revalidate-secret` | skipped |
| `IPasswordHasher` / `ITokenService` | BCrypt (work factor 12) / HS256 JWT + SHA-256 refresh hashes | — |

- Audit timestamps: `AuditableEntityInterceptor`. Domain events: published after `SaveChangesAsync` succeeds.
- All `DateTime` values are stored as UTC and read back with `Kind = Utc`.
- `ArticleViewsFlushJob` flushes buffered views every `BackgroundJobs:ArticleViewsFlushMinutes` and on shutdown.
- `DatabaseInitializer` applies migrations and creates the admin from `AdminSeed:*` on first run.

## Local setup (Windows)
```bash
dotnet tool install --global dotnet-ef --version 10.*

# Secrets — stored outside the repo
dotnet user-secrets init --project WebAPI
dotnet user-secrets set "Jwt:SigningKey" "<64+ random characters>" --project WebAPI
dotnet user-secrets set "AdminSeed:Email" "you@example.com" --project WebAPI
dotnet user-secrets set "AdminSeed:Password" "<12+ chars, letters and digits>" --project WebAPI

# Database schema
dotnet ef migrations add InitialCreate --project Infrastructure --startup-project WebAPI --output-dir Persistence/Migrations
dotnet ef database update --project Infrastructure --startup-project WebAPI
```
Integration tests create and drop their own LocalDB databases. To use another SQL Server, set
`MARWANQUTECH_TEST_SQL` (server part of the connection string, without a database name).
`ICurrentUser` and the JWT bearer middleware are added by the WebApi (Step 4).
