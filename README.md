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
| `Infrastructure/` | `MarwanQuTech.Infrastructure` | Application | ⏳ Step 3 |
| `WebAPI/` | `MarwanQuTech.WebApi` | Application, Infrastructure | ⏳ Step 4 |
| `tests/Domain.UnitTests/` | `MarwanQuTech.Domain.UnitTests` | Domain | ✅ xUnit + Shouldly |
| `tests/Application.UnitTests/` | `MarwanQuTech.Application.UnitTests` | Application | ✅ validators, pipeline, invalidation, e-mail |

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
  (replaying a revoked token revokes every session). Only token hashes are stored.
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

Ports implemented in Infrastructure (Step 3): `IApplicationDbContext`, `ICacheService`, `IImageStorage`,
`IEmailSender`, `IPasswordHasher`, `ITokenService`, `IRevalidationService`, `IArticleViewCounter`;
`ICurrentUser` and `TimeProvider` are registered by the WebApi.
