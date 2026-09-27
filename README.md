# BaseForge

**Forge .NET microservices from a spec.**

[![NuGet](https://img.shields.io/nuget/vpre/BaseForge.API?label=NuGet&color=f2612f)](https://www.nuget.org/packages?q=BaseForge)
[![.NET 10](https://img.shields.io/badge/.NET-10-512bd4)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-dropdeart.github.io%2FBaseForge-f2612f)](https://dropdeart.github.io/BaseForge/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

BaseForge is an opinionated **base library** and **visual code generator** for .NET 10 microservices. Describe your entities in YAML — or click them together in the browser-based Designer — and get clean, production-ready services with CQRS, authentication, gRPC, events, logging and Docker already wired.

📖 **Documentation:** https://dropdeart.github.io/BaseForge/ · 🇹🇷 [Türkçe README](README.tr.md)

---

## Why BaseForge?

Every microservice needs the same plumbing — layering, CQRS, repositories, audit fields, soft delete, error handling, auth, inter-service calls, messaging, logging, health checks, Docker files. BaseForge makes those decisions once, documents *why*, and ships them as:

- **A library** — `builder.Services.AddBaseForge(...)` gives you all of it in one line. It's a library, not a framework: every behavior can be overridden.
- **A generator** — the `baseforge` CLI and its Designer turn a spec into plain, readable C# (controllers, handlers, EF Core entities, DTOs, protos, Dockerfile) that you own.

## Features

- 🎨 **Visual Designer** — entities, relations, access rules and Identity in the browser, with a live ER diagram; generate, build and run with one click
- 🧱 **Clean Architecture + CQRS** on MediatR, repositories, audit fields, soft delete
- 🗄️ **EF Core 10 + Dapper** — LINQ for writes, raw SQL for heavy reads, one shared connection
- 🔐 **Central Identity** (OpenIddict + ASP.NET Identity) — OAuth2/OIDC, social logins, roles, ownership rules, user profile fields
- 🔌 **gRPC** between services with generated protos, clients and servers
- 📨 **RabbitMQ** events with a transactional outbox, inbox idempotency and dead-letter queues
- 🔭 **Observability** — Serilog + Grafana Loki, correlation ids across HTTP → gRPC → RabbitMQ, `/health` everywhere
- 🏢 **Multi-tenancy**, **append-only entities**, **enums**, **JSONB**, **list filters**, **YARP gateway**

## Quick start

```bash
# 1. Install the CLI
dotnet tool install -g BaseForge.CodeGen --prerelease

# 2. Open the Designer in a workspace folder
mkdir my-platform && cd my-platform
baseforge new orders
```

The Designer opens at `http://localhost:3500`. Add entities, press **Generate + Build**, then **Run** — your service is up at `http://localhost:8080/scalar/v1`.

Prefer YAML? Write a spec and generate from the terminal:

```yaml
# orders.yaml
service: orders
database: orders_db
entities:
  Order:
    props:
      Status: { type: enum, values: [Draft, Paid], default: Draft }
      Total: decimal
      BuyerId: guid
    ownerField: BuyerId
    access:
      list: [Admin, owner]
    publishes: [created]
```

```bash
baseforge new-service --spec orders.yaml
```

### Using the library directly

```bash
dotnet add package BaseForge.API --prerelease
```

```csharp
builder.AddBaseForgeLogging("orders");
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL<OrdersDbContext>(connectionString);
    options.EnableCQRS(typeof(Program).Assembly);
    options.EnableAuditLog();
});

var app = builder.Build();
app.UseBaseForge();
```

## Packages

| Package | Description |
| --- | --- |
| `BaseForge.Core` | Interfaces and entity bases only (no external dependencies) |
| `BaseForge.Infrastructure` | EF Core repositories, Dapper query helpers, DbContext base, RabbitMQ event bus, outbox/inbox |
| `BaseForge.API` | Controller base, middleware, `AddBaseForge()` / `UseBaseForge()`, JWT, logging, health checks |
| `BaseForge.Tools` | Developer tools: DBML ER diagram generation from an EF Core model |
| `BaseForge.CodeGen` | The `baseforge` .NET tool — code generator + Designer |

## Tech stack

.NET 10 · ASP.NET Core · EF Core 10 · Dapper · MediatR · PostgreSQL · gRPC · RabbitMQ · OpenIddict · YARP · Serilog · Grafana Loki · Docker

## Documentation

| | |
| --- | --- |
| [Getting Started](https://dropdeart.github.io/BaseForge/guide/getting-started) | Install, generate and run your first service |
| [Designer](https://dropdeart.github.io/BaseForge/guide/designer) | A tour of the visual editor |
| [Service Spec](https://dropdeart.github.io/BaseForge/guide/service-spec) | Every YAML option explained |
| [Identity & Authorization](https://dropdeart.github.io/BaseForge/guide/identity) | Central auth, roles, ownership, profile fields |
| [Deploying to Production](https://dropdeart.github.io/BaseForge/guide/deployment) | Docker, reverse proxy, HTTPS, checklist |
| [Architecture Decisions](https://dropdeart.github.io/BaseForge/architecture) | What was decided and why |

## Repository layout

```
src/
  BaseForge.Core/            → entity bases, interfaces, CQRS contracts, exceptions
  BaseForge.Infrastructure/  → EF Core repository, Dapper helpers, messaging, DI extensions
  BaseForge.API/             → BaseController, middleware, AddBaseForge()
  BaseForge.CodeGen/         → baseforge CLI: code generator + Designer host
  BaseForge.Designer.Web/    → Designer UI (React + Vite + TypeScript)
  BaseForge.Tools/           → DBML ER diagram generator
services/                    → Identity reference service + sample generated services
samples/                     → example specs (blog, orders, auth, …)
docs/                        → documentation site (VitePress, English + Turkish)
tests/
```

## Development

```bash
dotnet build      # the whole solution
dotnet test       # tests
```

To refresh your globally installed `baseforge` tool from local source (this is **not** an official release):

```powershell
.\scripts\update-cli.ps1                 # full build (including the Designer / Identity UIs)
.\scripts\update-cli.ps1 -SkipWebBuild   # CLI / codegen only
```

To work on the documentation site:

```bash
cd docs
npm install
npm run dev
```

Official releases are published to nuget.org by `.github/workflows/publish.yml` when a GitHub Release is created.

## License

[MIT](LICENSE)
