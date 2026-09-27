# What is BaseForge?

**BaseForge** is an opinionated, reusable **base library and code generator for .NET 10 microservices**. Instead of rebuilding the same architecture from scratch for every new backend, you extend BaseForge — and let its generator write the repetitive parts for you.

It comes in two halves that work together:

| | What it is | You use it to… |
| --- | --- | --- |
| **The library** | `BaseForge.Core`, `BaseForge.Infrastructure`, `BaseForge.API` NuGet packages | Get CQRS, repositories, audit/soft delete, JWT, RabbitMQ, logging and health checks with one `AddBaseForge()` call |
| **The generator** | `baseforge` CLI + a browser-based **Designer** | Describe entities in YAML (or visually) and get a complete, compilable, Docker-ready service |

## Why?

Every microservice needs the same plumbing: a clean layering, a CQRS pipeline, repositories, audit fields, soft delete, exception handling, authentication, inter-service calls, messaging, logging, health checks, Docker files… Writing it by hand is slow and each service drifts a little from the last.

BaseForge makes those decisions **once**, documents *why* in the [architecture decisions](/architecture), and ships them as a library plus a generator:

- **Library, not framework.** You keep full control — every behavior can be overridden. You just don't have to write it.
- **One-line integration.** `builder.Services.AddBaseForge(...)` wires everything up.
- **Generated code is plain code.** No runtime magic: the generator emits ordinary controllers, handlers, DTOs, EF Core entities and proto files that you can read, debug and edit.

## Highlights

- 🧱 **Clean Architecture + CQRS** on MediatR, with `ICommand` / `IQuery` / handler contracts.
- 🗄️ **EF Core 10 + Dapper** — LINQ and change tracking for writes, raw SQL for heavy reads, sharing one connection and transaction.
- 🔐 **Central Identity service** (OpenIddict + ASP.NET Identity) — OAuth2/OIDC, social logins, roles, ownership rules, user profile fields.
- 🔌 **gRPC** between services with automatically generated protos, clients and servers.
- 📨 **RabbitMQ** pub/sub with a **transactional outbox**, inbox idempotency and dead-letter queues.
- 🔭 **Observability** — Serilog + Grafana Loki, a correlation id across HTTP → gRPC → RabbitMQ, and `/health` everywhere.
- 🎨 **Visual Designer** — design entities, relations, access rules and Identity in the browser, see a live ER diagram, generate, build and run with Docker in one click.
- 🏢 **Multi-tenancy**, **append-only entities**, **JSONB fields**, **enums**, **list filters**, **YARP gateway** and more — all from the spec.

## Tech stack

| Layer | Technology |
| --- | --- |
| Framework | .NET 10 |
| Architecture | Clean Architecture + CQRS + Repository Pattern |
| CQRS | MediatR |
| Service-to-service (sync) | gRPC |
| Service-to-service (async) | RabbitMQ |
| Auth | Central Identity Service + JWT / OAuth2 |
| Database | PostgreSQL (one database per microservice) |
| ORM / Data access | EF Core 10 (writes + tracking) + Dapper (raw SQL reads) |
| Containers | Docker + Docker Compose |
| Distribution | Public NuGet (nuget.org) |

## Packages

| Package | Description |
| --- | --- |
| [`BaseForge.Core`](https://www.nuget.org/packages/BaseForge.Core) | Interfaces and entity bases only — no external dependencies |
| [`BaseForge.Infrastructure`](https://www.nuget.org/packages/BaseForge.Infrastructure) | EF Core repositories, Dapper query helpers, DbContext base, RabbitMQ event bus, outbox/inbox |
| [`BaseForge.API`](https://www.nuget.org/packages/BaseForge.API) | Controller base, middleware, `AddBaseForge()` / `UseBaseForge()`, JWT, logging, health checks |
| [`BaseForge.Tools`](https://www.nuget.org/packages/BaseForge.Tools) | Developer tools: DBML ER diagram generation from an EF Core model |
| [`BaseForge.CodeGen`](https://www.nuget.org/packages/BaseForge.CodeGen) | The `baseforge` .NET tool: code generator + Designer |

::: tip Status
BaseForge is in **beta** (current: `0.6.1-beta`). The API is stabilizing but breaking changes are still possible between minor versions — they are always listed in the [release notes](/releases/v0.6.1-beta).
:::

Ready? Head to [Getting Started](/guide/getting-started).
