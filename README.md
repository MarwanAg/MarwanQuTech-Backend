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
| `Application/` | `MarwanQuTech.Application` | Domain | ⏳ Step 2 |
| `Infrastructure/` | `MarwanQuTech.Infrastructure` | Application | ⏳ Step 3 |
| `WebAPI/` | `MarwanQuTech.WebApi` | Application, Infrastructure | ⏳ Step 4 |
| `tests/Domain.UnitTests/` | `MarwanQuTech.Domain.UnitTests` | Domain | ✅ xUnit + Shouldly |

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
