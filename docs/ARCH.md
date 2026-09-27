# Architecture Decisions

This document records BaseForge's architectural decisions and the reasoning behind them in detail. It is updated **before** a new feature is added.

## 1. General Approach: Opinionated Library

BaseForge is **not a framework — it is an opinionated library**. The goal is to keep the flexibility of a library while offering the convenience of a framework. In practice this means one-line DI integration:

```csharp
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL(connectionString);
    options.EnableCQRS();
    options.EnableAuditLog();
});
```

**Rationale:** Every microservice should not have to rebuild the same infrastructure decisions (CQRS, repository, audit, exception handling) — yet it must still be able to override behavior when it needs to.

## 2. Layering (Clean Architecture)

Three packages, with dependencies pointing from the outside in:

```
BaseForge.API  ──►  BaseForge.Infrastructure  ──►  BaseForge.Core
```

| Layer | Depends on | Contents |
| --- | --- | --- |
| `Core` | Nothing (MediatR contracts only) | Entity bases, interfaces, CQRS contracts, exceptions |
| `Infrastructure` | `Core` | GenericRepository, Dapper query helpers, DbContext base, DI extensions |
| `API` | `Core` + `Infrastructure` | BaseController, middleware, `AddBaseForge()` |

**Rules:**
- `Core` must not depend on any concrete infrastructure (DB, HTTP, MediatR implementation). It only references MediatR's marker interfaces (`IRequest`, etc.) — the full MediatR package is registered in Infrastructure/API.
- `Infrastructure` must never depend on `API`.
- `API` may depend on both layers.

**Rationale:** Testability and package independence. Because `Core` has no dependencies, services that only want to consume contracts can pull in just that package.

## 3. CQRS — Built on MediatR

- CQRS is not written from scratch; it is built on **MediatR**.
- `Core` defines the `ICommand`, `IQuery` and `IHandler` base contracts, which wrap MediatR's `IRequest`/`IRequestHandler` types.
- Every service extends these contracts.
- **Decision:** No CQRS/mediator library other than MediatR is added.

## 4. Data Access — EF Core 10 (ORM) + Dapper (raw SQL)

> **Decision change (2026-06-24):** The original specification's "no ORM, prefer ADO.NET" rule was revised by the project owner. Rationale: the productivity of EF Core's LINQ + change tracking and the flexibility of raw SQL can be had at the same time; the boilerplate cost of pure ADO.NET hurts productivity.

A hybrid approach is used:

- **EF Core 10** is the primary ORM. Responsibilities: writes (insert/update/delete), change tracking (identity map / first-level cache), migrations and most CRUD via LINQ.
- **Dapper** (micro-ORM) is used for raw SQL in heavy reads and complex joins; it maps results to DTOs quickly. Dapper is not a query builder/ORM — SQL is written by hand, it only provides mapping.
- Dapper runs over the EF Core `DbContext`'s `DbConnection` (`Database.GetDbConnection()`), so the same connection and transaction are shared.
- `GenericRepository` implements the `IRepository<TEntity, TKey>` contract with EF Core. For complex read scenarios a Dapper-based query helper (`ISqlQuery`-style) is provided.

**Division of roles:**

| Need | Tool |
| --- | --- |
| CRUD, loading relations, LINQ | EF Core |
| Change tracking, migrations | EF Core |
| Complex join / projection / report query | Dapper (raw SQL) or EF `FromSql` |
| Bulk set-based update/delete | EF `ExecuteUpdate` / `ExecuteDelete` |
| Full control / bare connection | `DbContext.Database.GetDbConnection()` |

PostgreSQL provider: `Npgsql.EntityFrameworkCore.PostgreSQL`.

### Audit & Soft Delete

- `BaseEntity` defines `CreatedAt`, `UpdatedAt`, `CreatedBy` (audit) and `IsDeleted`/`DeletedAt` (soft delete).
- Audit fields are filled automatically in the EF Core `SaveChanges` override.
- Soft delete is applied with an EF Core **global query filter**; deleted records do not appear in default queries.
- **Note:** Dapper does not know about EF's query filter; in raw SQL written with Dapper the soft-delete condition (`WHERE is_deleted = false`) must be added by hand.

## 5. Microservice Communication

- **Database per Service:** Each microservice owns its PostgreSQL database; services never access each other's DB directly.
- **Synchronous:** gRPC.
- **Asynchronous:** RabbitMQ (fire-and-forget, event-driven) — see §5.2.

### 5.1. gRPC — Automatic Proto Generation

Every `via: grpc` external reference (`ExternalRefSpec`) generates **real** gRPC client+server code during `baseforge new-service` (previously only an empty, `Id`-only interface skeleton was generated).

- **Server side (automatic, no opt-out):** Every generated service exposes ALL of its entities as a gRPC service (`Protos/{entity}.proto` + `Grpc/{Entity}GrpcService.cs`). The server implementation calls the existing CQRS `Get{Entity}ByIdQuery` through MediatR — data access is not rewritten. To avoid ordering dependencies this behavior is unconditional (a provider exposes all its entities even before its consumer is generated).
- **Client-side resolution:** Apart from `ExternalRefSpec.Target` (`"service/Entity"`), CodeGen does not know the shape of the target. Solution: using the target's service segment, a sibling `{service}.yaml` is looked up **in the folder containing the spec file** (via `SpecLoader`). If found, the target entity's real `Props` are read and a rich (real-field) proto+client is generated; if not, a warning is written to `Console.Error` and it silently falls back to a minimal (Id-only) stub — it never throws.
- **`identity/User` special case:** Because Identity does not use its own `ServiceSpec` (it has a separate `AuthSpec`), sibling-spec reading does not apply. The `user.proto` is generated from `auth.yaml` (see §6.3); both `IdentityGenerator` and the `CodeGenerator`'s `target: identity/User` special case read the **same** source (single source, no drift). Fixed fields (`ApplicationUser`): Id, UserName, Email, FullName.
- **Kestrel — two ports required:** Without TLS (h2c), ASP.NET Core Kestrel cannot automatically distinguish HTTP/1.1 and HTTP/2 on the same port (verified in a live test: `EndpointDefaults: Http1AndHttp2` alone makes REST work but silently downgrades gRPC to HTTP/1.1). So every generated service defines **two separate endpoints**: `Http` (8080, REST/Scalar) and `Grpc` (8081, h2c). On the client side, TLS-less HTTP/2 is enabled with `AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true)`.
- **Limitations:** External references to entities with the same name from two different source services collide (the second is skipped). No JWT propagation on gRPC calls (trust relies on the Docker network boundary). decimal/datetime/date/guid are carried as `string` in proto (no native equivalent).
- **Cross-service host address:** Each generated service runs in its own isolated `docker-compose.yml` (its own Docker network), so a provider's bare name (e.g. `"identity"`) cannot be resolved via network DNS. Rich gRPC clients' `ProviderHost` (and the RabbitMQ broker address, see §5.2) is therefore generated as `host.docker.internal` — Docker Desktop's container-to-host address.
- **Reference scenario:** `samples/products.yaml` + `samples/warehouse.yaml` + `samples/orders.yaml` → `services/BaseForge.Products`, `services/BaseForge.Warehouse`, `services/BaseForge.Orders` (committed, real and compilable). Orders connects to Products (sibling spec) and Identity (`identity/User`) over gRPC; Warehouse also connects to Products.

### 5.2. RabbitMQ — Automatic Event Pub/Sub

`ExternalRefSpec.Via = "event"` is **not implemented** and is reserved for a different future feature (read-model synchronization — a producer publishes CRUD events, a consumer updates a local shadow table); today it is a no-op. Asynchronous pub/sub works through two separate, simpler YAML fields: per-entity `publishes` and per-service `subscribes`.

**Library side (`BaseForge.Core`/`Infrastructure`/`API`):**

- `IIntegrationEvent : INotification` (Core) — extends MediatR's notification contract. The publisher calls `IEventBus.PublishAsync<TEvent>()`; on the consumer side `RabbitMqConsumerHostedService` (Infrastructure, `BackgroundService`) deserializes the message from the queue to the matching CLR type and dispatches it locally with `IPublisher.Publish()` (MediatR). The developer writes an ordinary `INotificationHandler<TEvent>` and never sees RabbitMQ — **no new CQRS library is added** (consistent with the §3 decision).
- `builder.Services.AddBaseForge(options => options.EnableRabbitMq(mq => { ... }))` — the exact same fluent pattern as `EnableJwt`. If there are subscriptions (`mq.Subscribe<TEvent>(eventType, queueName)`), the consumer hosted service is added automatically; the outbox relay (below) is always added.
- Broker: a single topic exchange (`baseforge.events`). The routing key is derived from the event's `EventType` (`service/EntityKind` → `service.EntityKind`).
- Packages: `RabbitMQ.Client` (fully async API) + `Microsoft.Extensions.Hosting.Abstractions` — added to `BaseForge.Infrastructure` (no dependency on ASP.NET Core; the host-framework-agnostic rule is kept).

**Transactional Outbox (2026-07-16 — solves the dual-write risk):**

`IEventBus.PublishAsync<TEvent>()` **no longer writes to RabbitMQ directly**. In the earlier design the handler first committed to the DB with `_unitOfWork.SaveChangesAsync()` and then wrote to the broker with `_eventBus.PublishAsync()` — there was no atomicity between the two: if the DB commit succeeded but the publish failed (network/broker error), the change was persisted but the event was never published.

- `OutboxEventBus : IEventBus` (Infrastructure, **Scoped**) — `PublishAsync` adds the event as an `OutboxMessage` row (Core, plain POCO — does not implement `IAuditEntity`/`ISoftDelete`/`ITenantEntity`, not subject to global query filters) to the caller's current `BaseForgeDbContext` change tracker. It does not write to the DB and does no I/O.
- In the CodeGen templates (`Templates.cs`) the `_eventBus.PublishAsync(...)` call now happens **before** `_unitOfWork.SaveChangesAsync(...)` — so the outbox row is written atomically **in a single `SaveChangesAsync` call, in the same transaction** as the triggering business entity change.
- `OutboxPublisherHostedService` (Infrastructure, `BackgroundService`) — periodically scans `BaseForgeDbContext.OutboxMessages` (`RabbitMqOptions.OutboxPollingInterval`, default 2s), sends unprocessed (`ProcessedAt IS NULL`) rows to the real broker with `IRabbitMqPublisher.PublishRawAsync()` (the renamed former `RabbitMqEventBus` — no longer generic, it writes the ready envelope JSON as-is) and marks `ProcessedAt` on success.
- Multi-instance safety: rows are selected with `SELECT ... FOR UPDATE SKIP LOCKED` — two instances never process the same row at the same time, no extra lease/claim column is needed, and if a process crashes the Postgres lock is released automatically.
- The `IEventBus` registration changed from Singleton to **Scoped** (because it must write to the same scope's `BaseForgeDbContext` as the caller) — `OutboxPublisherHostedService` is always registered.
- **Max retries + dead marking (2026-07-16):** When `RabbitMqOptions.OutboxMaxRetries` (default 10) is exceeded the row is marked `OutboxMessage.IsDead = true` and drops out of the relay's `WHERE` clause (`AND "IsDead" = false`) — it is not retried forever, but it is not deleted either (it stays in the table for manual inspection).
- **Cleanup/retention job (2026-07-16):** At the end of every scan tick (whether or not there were new messages), processed rows (`ProcessedAt` set) older than `RabbitMqOptions.OutboxRetention` (default 7 days) are bulk-deleted with `ExecuteDeleteAsync`. `IsDead` rows are exempt from this cleanup.

**CodeGen side:**

- `EntitySpec.Publishes: List<string>` (`created`/`updated`/`deleted`) — the matching Create/Update/Delete command publishes `{Entity}{Kind}Event` (now **before** `SaveChangesAsync`, into the outbox) (`Features/{Entity}s/{Entity}Events.cs`).
- `ServiceSpec.Subscribes: List<SubscribeSpec>` (`event: "service/EntityKind"`, `handler: ClassName`) — if the target entity is found in a sibling spec (or **its own spec** — self-subscription needs no special case) and the Kind is in its `publishes` list, a shadow event/data class with the real fields ("rich") is generated; otherwise one with just `Id` ("minimal"), plus an `INotificationHandler<T>` stub (`Integration/{Handler}.cs`). Sibling-spec lookup shares the same `LoadSiblingSpec` helper as gRPC external reference resolution (§5.1).
- CLI/YAML-only v1: `publishes`/`subscribes` themselves (which entity publishes/listens to which event) still have no form in the Designer web UI. Unlike `via: event`, these two fields are not represented in the `/meta` endpoint or `EntityEditor.tsx`; they can be planned as a fast-follow.
- **Designer form for RabbitMQ tuning (2026-07-16):** `ServiceSpec.RabbitMqTuning` (`OutboxMaxRetries`/`OutboxRetentionDays`, optional) — the exact same pattern as `DockerPortsSpec` (nullable nested object, directly controlled input). In the Designer it is **always** visible under the service form (right below DockerPorts), independent of `publishes`/`subscribes` — because the Designer's TS model does not represent `publishes`/`subscribes`, "does this service use RabbitMQ" could not be computed reliably on the client. If filled, `mq.OutboxMaxRetries`/`mq.OutboxRetention` override lines are added to the generated `Program.cs`'s `options.EnableRabbitMq(mq => ...)` block.
- Docker topology: the `rabbitmq` service in the root `docker-compose.yml` (already scaffolded with `RABBITMQ_*` variables in `.env.example`) becomes the single shared broker. Generated services do not start a RabbitMQ container in their isolated compose files; the `RabbitMq:Host` default in `appsettings.json` is `host.docker.internal` (see the cross-service host note in §5.1).

**v1 limitations (deliberate, documented simplicity):**

- ~~Consumer side: no DLQ/retry policy~~ **DLQ solved (2026-07-16):** Previously, a message rejected with `nack(requeue: false)` was **silently and permanently deleted** by RabbitMQ because no dead-letter exchange was defined on the queue — genuine data loss. Now, for every subscription a `{queue}.dead` queue bound to a shared `{ExchangeName}.dlx` (fanout) exchange is declared (the main queue is opened with the `x-dead-letter-exchange` argument); a rejected message is no longer lost and can be inspected in `{queue}.dead` (from the RabbitMQ management UI) and replayed manually. **Deliberately out of scope:** automatic retry-N-times-with-delay-then-DLQ — it requires TTL+DLX chaining (delay queue pattern), and trusting it is set up correctly without validating against a live broker is risky; a separate future task.
- Outbox relay: **at-least-once delivery, not exactly-once** — if the process crashes after the publish reaches RabbitMQ but before `ProcessedAt` is committed, the message may be sent again on the next scan. The actual problem (the event being lost entirely when publishing fails after commit) is fully solved. ~~No `EventId`-based idempotency on the consumer~~ **Solved (2026-07-16) — Inbox pattern:** `InboxMessage` (Core, does not implement marker interfaces for the same reason as `OutboxMessage`) + `BaseForgeDbContext.InboxMessages`. Before dispatching to MediatR, `RabbitMqConsumerHostedService.HandleDeliveryAsync` looks up the same `EventId` in `InboxMessages` — if found, it `ack`s without running the handler again. Marking happens **AFTER the handler** (mark-after, not mark-before): if the handler crashes, the Inbox row has not been committed yet, so the redelivery still looks "unprocessed" and is retried — with mark-before the event would be wrongly considered "already processed" and lost. Remaining limitation: these two steps (the handler's own DB effects + the Inbox row) are NOT in one transaction — if a crash happens between a successful handler and the Inbox commit/ack, a rare genuine duplicate is possible (much better than before, not perfect).
- ~~No cleanup/retention job in the outbox~~ **Solved (2026-07-16)** — see above.
- ~~Unlimited retries in the outbox~~ **Solved (2026-07-16)** — dead marking after max retries, see above. (The outbox's own "dead" rows have no separate DLQ/broker path, they stay in the table — the broker DLQ on the consumer side is a different mechanism.)
- The `OutboxMessages` table, like all other entity tables, is created without migrations, only with `Database.EnsureCreated()`.
- ~~No channel pool~~ **Solved (2026-07-16):** A bounded (capacity 10) `RentChannelAsync`/`ReturnChannelAsync` pool was added to `RabbitMqConnectionManager`; `RabbitMqPublisher.PublishRawAsync` now uses it instead of opening/closing a channel per publish. The consumer hosted service already holds a single channel for the application's lifetime, so it was unchanged.
- As with gRPC calls, there is no JWT/identity propagation in messages.

### 5.3. JSON/JSONB Field Type

`json` was added to the spec type system: `string` (serialized JSON text) on the C# side, Postgres `jsonb` on the database side (`[Column(TypeName = "jsonb")]` rather than EF Core's fluent API — the same pattern as the existing `MaxLength`, embedded into the entity class as a DataAnnotation attribute).

- **Decision:** `["json"] = ("string", "jsonb")` was added to `TypeMap.cs`; `[Column(TypeName = "jsonb")]` is generated **only on the entity class**, not on Create/Update command DTOs (the Column attribute is only meaningful on EF-mapped types; DTOs are not mapped — unlike `MaxLength`, which is also meaningful on DTOs for ASP.NET model validation).
- **Rationale:** For flexible/schemaless payload fields (e.g. event-specific data of audit/trace events) a single column is enough instead of a separate table/JOIN; Postgres's native `jsonb` support also enables querying/indexing (later via `EF.Functions.JsonContains`, etc.).
- **Limitations:** Maps only to Postgres `jsonb` (if another provider such as SQL Server is targeted, this type must be reconsidered). `maxLength` is meaningless for json, so it is rejected automatically by `SpecValidator`'s string/text-only check. It is carried as `string` in gRPC proto (the same "no native equivalent" limitation as decimal/datetime/guid).

### 5.4. Append-Only Entities

`EntitySpec.AppendOnly: bool` — when `true`, the Update/Delete command, its handler and controller action are **never generated**; only Create/GetById/List remain.

- **Decision:** A **per-entity** flag, not per-service (a service can have both mutable and append-only entities — e.g. `Product` mutable, `TraceEvent` append-only, in the same service).
- **Rationale:** Audit/trace records that require regulatory compliance such as GMP/Annex 11 and 21 CFR Part 11 must never be modifiable/deletable through the API. Guaranteeing this with an **endpoint that physically does not exist** at the generator level is safer than just "the client should not call Update/Delete" (contract/documentation).
- **Limitations:** With `AppendOnly=true`, `publishes` cannot contain anything other than `created` (`updated`/`deleted`) and `anonymousActions` cannot contain `update`/`delete` — `SpecValidator` catches this as an error before generation (fail loud instead of silently ignoring).

### 5.5. Multi-Tenancy

`ServiceSpec.MultiTenant: bool` — when `true`, **all** entities of the service implement `ITenantEntity` (`Guid TenantId`, `BaseForge.Core.Entities`); `options.EnableMultiTenancy()` is called.

- **Decision:** Service-wide, **not** per-entity — real isolation must cover every table; per-entity choice would be a foot-gun (forgetting one table = tenant leak). `BaseEntity<TKey>` was not changed (that would be a breaking change affecting all existing services) — the new `ITenantEntity` marker interface, following the same pattern as `ISoftDelete`, is added by CodeGen only to entities of services with `MultiTenant: true`; `TenantId` is not defined by the user in YAML, it is injected automatically.
- **Mechanism:** `ICurrentTenant` (Core, same shape as `ICurrentUser`) + `CurrentTenant` (API, reads the JWT `tenant_id` claim) are registered in DI with `EnableMultiTenancy()`. In `BaseForgeDbContext`:
  - `ApplyAuditAndSoftDelete` stamps `TenantId` onto `ITenantEntity`s in the `Added` state; if `ICurrentTenant.TenantId` is null it throws `InvalidOperationException` (fail loud instead of a silent NULL row).
  - Because EF Core allows only **one** query filter per entity type, `OnModelCreating` builds the `ISoftDelete` and `ITenantEntity` filters into a single combined filter with `Expression.AndAlso` (4 cases: neither / soft-delete only / tenant only / both).
  - The generated DbContext's constructor forwards `ICurrentUser?`/`ICurrentTenant?` to `BaseForgeDbContext` (`(options, currentUser = null, currentTenant = null) : base(...)`) — without this forwarding, tenant stamping never works.
- **A known reflection trap (caught during generation, verified with a unit test):** When referencing the current context (`this`) in the query filter expression, it must be **explicitly typed as the base class** with `Expression.Constant(this, typeof(BaseForgeDbContext))` — if `Expression.Constant(this)` uses the runtime type (always the derived DbContext class generated by CodeGen), the `private` `CurrentTenantId` property (defined only on `BaseForgeDbContext`; private members are not inherited by the derived type via `FlattenHierarchy`) cannot be found via reflection and every query throws `ArgumentException`.
- **Limitations:** The tenant claim name is fixed: `tenant_id`. Adding a record to a multi-tenant entity without a tenant context (e.g. from a background service without `ICurrentTenant`) throws — this is a deliberate design (instead of a silent cross-tenant leak).

### 5.6. Centralized Logging and Correlation ID

Each microservice used to be trapped in its own console output — there was no way to follow a request across services through an HTTP → gRPC → RabbitMQ event chain. Centralized, structured logging with Serilog + Grafana Loki and a `CorrelationId` that crosses all three boundaries (HTTP/gRPC/RabbitMQ) were added.

**Design decision — always on:** Like `ExceptionHandlingMiddleware`/`RequestLoggingMiddleware`, this is **not** an opt-in toggle in `spec.yaml` — `ServiceSpec`/`CodeModel`/Designer UI were not touched, it was only added to the fixed CodeGen templates. If the Loki URL is empty/unreachable, the service keeps logging to the console (consistent with RabbitMQ's pattern of connecting to a shared broker via `host.docker.internal` — Loki being up is not a prerequisite); if the Loki sink hits a write error internally (2026-07-16), a diagnostic line is now written to stderr through `Serilog.Debugging.SelfLog` — it is not completely silent.

- `ICorrelationIdAccessor` (Core, `BaseForge.Core.Logging`) — ambient access contract for the current flow's correlation id. `CorrelationIdAccessor` (Infrastructure) implements it with `AsyncLocal<string?>`, registered as Singleton; it flows correctly along the async call chain (HTTP → handler → outbox write, gRPC call, consumer's MediatR dispatch) and does not leak between concurrent flows.
- **HTTP boundary:** `CorrelationIdMiddleware` (API) — added at the very start of the pipeline (even before `ExceptionHandlingMiddleware`). It uses the incoming `X-Correlation-Id` header (or generates one), writes it to the accessor, adds it to the Serilog `LogContext`, and writes the same header back to the response.
- **gRPC boundary:** `CorrelationIdClientInterceptor`/`CorrelationIdServerInterceptor` (API, `BaseForge.API.Grpc`) — `correlation-id` is added to the outgoing call's metadata, and read on the server side into the accessor/LogContext. The CodeGen Program.cs template automatically adds `.AddInterceptor<CorrelationIdClientInterceptor>()` to every `AddGrpcClient<...>()` call and the server interceptor to `AddGrpc()`.
- **RabbitMQ boundary:** A `CorrelationId` field was added to `EventEnvelope` (Infrastructure) — **no** DB schema/migration change (`OutboxMessage.Payload` is already a full JSON blob; the new field is just a member of that JSON). `OutboxEventBus.PublishAsync` embeds the accessor's current value when building the envelope; `RabbitMqConsumerHostedService.HandleDeliveryAsync` restores this id into the new scope's accessor and `LogContext` before dispatching to MediatR — the logs of the handler processing the event carry the same id as the original request that triggered it.
- **Serilog wiring:** the new `WebApplicationBuilder.AddBaseForgeLogging(serviceName)` (API) — called **separately** from `AddBaseForge` (services, DI), at the `builder.Host.UseSerilog(...)` level (Serilog replaces the logging provider through the Host). Each log line is tagged with a `Service` field; if the `Serilog:LokiUrl` appsettings key is set it is pushed with `Serilog.Sinks.Grafana.Loki`, otherwise it is written only to the console.
- Shared `loki` + `grafana` containers were added to the root `docker-compose.yml` (same pattern as Postgres/RabbitMQ); Grafana auto-provisions the Loki datasource via `grafana/provisioning/datasources/loki.yaml` (no manual "Add datasource" required).

**v1 limitations (deliberate, documented simplicity):**

- ~~No ready-made dashboard in Grafana~~ **Solved (2026-07-16):** `grafana/dashboards/baseforge-logs.json` (provisioning: `grafana/provisioning/dashboards/dashboards.yaml`) — a log panel filterable with `Service`/`CorrelationId` template variables + a log volume per service time-series panel. More advanced queries are still done with LogQL in Explore.
- Log retention/rotation: configured to 7 days with `grafana/loki-config.yaml` (2026-07-16) (`retention_period: 168h`, `compactor.retention_enabled: true`) — to change it, edit this file and restart with `docker compose up -d loki`. Verified on 2026-09-26 on a live Loki 3.2.0 container (service logs arrive with the `service` label, Grafana datasource and dashboard provisioning work).
- ~~Loki + Grafana were only defined in this repo's root compose; since there was no Loki running in the user's workspace, logs silently went only to the console~~ **Solved (2026-09-26):** on the first service/identity generation, `observability/` (compose + the `grafana/` files above, embedded from a single source; a random Grafana admin password in `.env`) is written to the workspace root; if the folder exists it is left alone. The generated `launchSettings.json` provides `Serilog__LokiUrl=http://localhost:3100` and the `localhost` equivalent of the Authority for local `dotnet run` — `host.docker.internal` only resolves inside containers.
- If `Serilog:LokiUrl` is empty/unreachable, logs fall back to the console; when unreachable, a diagnostic message is now written to stderr via `SelfLog` (2026-07-16) — but this is visibility only, not a full health check/retry.
- The gRPC client interceptor only supports unary calls (CodeGen currently only generates unary `GetById` — no streaming RPCs).

### 5.7. Health Checks and Service Status Monitoring

The `healthcheck:` blocks in docker-compose used to exist only for infrastructure containers (Postgres `pg_isready`, RabbitMQ `rabbitmq-diagnostics ping`) — there was no app-level probe showing whether the generated service's own application container was alive, so the Identity dashboard's "Services" section only showed a `services.json` snapshot frozen at codegen time (name/port/entity count, no liveness).

**Design decision — always on:** Like logging in §5.6, `/health` is **not** an opt-in toggle in `spec.yaml` — its whole purpose is for Identity to be able to poll every service reliably; if it were opt-in, some services would not show up on the dashboard.

- **`/health` endpoint (BaseForge.API):** `AddBaseForge` always calls `AddHealthChecks()`; if a connection string was given with `UsePostgreSQL` (it always is), `PostgresHealthCheck` (Infrastructure, raw `NpgsqlConnection` + `SELECT 1` — without adding a separate `AspNetCore.HealthChecks.NpgSql` dependency) is added as a `"postgresql"` check. `UseBaseForge` (now takes `WebApplication` — extended from `IApplicationBuilder` because endpoint mapping is required) maps `/health` independently of JWT/`[Authorize]` (`Protect` is applied per controller, there is no global filter) with a small custom JSON response writer: `{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy","durationMs":12}]}`.
- **Docker healthcheck:** A `curl -f http://localhost:8080/health` based `healthcheck:` block was added to CodeGen's `docker-compose.yml`/`Dockerfile` templates (`Templates.cs`, and `IdentityGenerator.BuildCompose`/`BuildDockerfile` for identity); because `mcr.microsoft.com/dotnet/aspnet:10.0` does not include curl, `apt-get install curl` was added to the final Docker stage.
- **Identity's live polling (`ServicesApiController.Status`, `GET /api/services/status`):** Identity polls every registered service except itself via `host.docker.internal:{restPort}/health` — because each generated service runs in its own independent docker-compose network (container DNS is not shared), the same host-mapped port approach as the existing cross-service gRPC pattern (§7.1, `CrossServiceHost = "host.docker.internal"`) is used. This is the **first server-to-server `HttpClient`** in the codebase (`"ServiceHealthClient"`, 2-second timeout, named via `AddHttpClient`) — until now inter-service communication was only gRPC/RabbitMQ.
- **Dashboard (React):** `Home.tsx` merges the existing static `services.json` list (name/port/entity count) with the live `{name, healthy, checkedAt}` list returned by `/api/services/status` by name; it refreshes every 10 seconds with `setInterval`, and shows a green/red/grey dot + "Up"/"Down"/"Checking…" badge on each card.

**v1 limitations (deliberate, documented simplicity):**

- No historical uptime/downtime record or chart — only the current state (pull/polling, not push).
- `/health` and `/api/services/status` are unauthenticated — consistent with the trust level the static `services.json` already shares (internal/ops purpose; the dashboard is already within the same trust boundary).
- No automatic alerts/notifications (email/Slack when a service goes down) — a separate future feature.
- When running locally (`dotnet run`, outside a container), resolution of `host.docker.internal` is not guaranteed — a known limitation already shared by the existing gRPC cross-service pattern, not a new risk.

### 5.8. Gateway / BFF — YARP-Based Reverse Proxy

Until now, inter-service communication (§5.1) only provided "resolve a single record by ID" (gRPC, `Integration/{Entity}Client.cs`) — there was no mechanism for a frontend to reach ALL CRUD/list endpoints of multiple services through a single origin (each service lives in an isolated docker-compose network on its own host port). This adds a permanent **gateway/BFF** feature that lets one service expose the REST surface of the others to the frontend through a single door.

**Design decision — entity-agnostic, config-only proxy.** The gateway does NOT know/proxy a sibling service's entities one by one — the **entire** `/api/*` surface of every sibling service in `ServiceSpec.Gateway.ProxiedServices` is transparently forwarded with [YARP](https://microsoft.github.io/reverse-proxy/) under `/api/gateway/{service}/{**catch-all}`. The benefit: when a new entity is added to a sibling service the gateway does NOT need to be regenerated — only port/route information is generated up front; entity information is not needed at all.

- **Spec:** `gateway: { proxiedServices: [core, its, netsis] }` (`GatewaySpec`, `ServiceSpec.cs`).
- **Port resolution (`CodeGenerator.ResolveGatewayTargets`):** each proxied service's host REST port is read from the shared `services.json` registry at the workspace root (`ServiceRegistry.LoadForWorkspace`, the SAME pattern as identity gRPC port resolution in §7.1) — NOT from the sibling's raw `spec.yaml` `DockerPorts`, because for a target that has not been generated yet / has no port set this falls back to a fixed `8080` and multiple proxied services could collide on the same (wrong) address. A target not found in the registry is silently skipped (a warning is written) — that service must be generated/updated first.
- **Generation:** a `ReverseProxy` section in appsettings.json (a `Routes`+`Clusters` pair for each proxied service; the `/api/gateway/{service}` prefix is stripped with `PathRemovePrefix` and `/api` is added with `PathPrefix` — the previously used `PathPattern: /api/{catch-all}` encoded the `/` in multi-segment captures and turned two-or-more-segment paths such as `Listings/{id}` into 404s at the target, found live in production) and `builder.Services.AddReverseProxy().LoadFromConfig(...)` + `app.MapReverseProxy()` in `Program.cs` (`Templates.cs`). The `Yarp.ReverseProxy` package reference is added conditionally only to projects with `Gateway` set (`Templates.Project`, `ProjectFileModel.HasGateway`).
- **CORS:** CORS on the proxied services is irrelevant from the browser's point of view (the browser never goes to them directly) — only the gateway service's OWN `corsOrigins` (the existing general CORS mechanism, `Cors:AllowedOrigins`) must include the frontend's origin.

**v1 limitation (deliberate, documented simplicity):** `MapReverseProxy()` endpoints run outside the MVC pipeline — `[Authorize]` is NOT applied to them, and the gateway hop does no authentication itself. YARP forwards the `Authorization` header as-is by default, and the real authorization boundary still sits in the target service (`protect: true` + `[Authorize]`) — this is not a security hole (the gateway does NOT ADD an extra verification layer, but it does NOT BREAK the existing boundary either), there is just no pre-check at the gateway. Also: the proxied service's port is "baked" into appsettings.json at generation time — if a sibling's port changes later, the gateway must be regenerated too (the same known limitation as the existing gRPC-client port coupling).

## 6. Authentication

- There is a single central **Identity Service** (JWT / OAuth2).
- Every service validates the JWT token **locally** in its own middleware; there is no call to a central DB on every request.

### 6.1. Authorization Model (roles + ownership)

Authentication ("are you signed in?") and authorization ("are you allowed to do this?") are separate. `auth.protect` + `anonymousActions` only expressed the former; Identity put `Admin`/`User` roles into the token but services did not read them — every registered user could call every write endpoint. In a real-world project this gap was closed with ~60 hand-written "owner/admin required" checks and an `AdminAuth.cs` copied into every service.

**Spec (service):**

```yaml
auth:
  protect: true
  defaultAccess: authenticated   # for actions not listed in access (default: authenticated)
  superRoles: [SuperAdmin]       # optional — these roles pass every rule (roles + owner) automatically
entities:
  Post:
    ownerField: AuthorId         # optional, guid prop
    access:
      list: anonymous
      getById: anonymous
      create: [Admin, Editor]
      update: [Admin, owner]
      delete: [Admin]
```

Per-action values: `anonymous` → `[AllowAnonymous]`; `authenticated` → `[Authorize]`; role list → `[Authorize(Roles = "...")]`; if the list contains `owner` → `[Authorize]` + ownership check (the listed roles and `superRoles` pass the check). `anonymousActions` is kept for backward compatibility (same as `access: { x: anonymous }`); both cannot be used on the same entity.

**Ownership (`ownerField`):**
- `create`: the owner field is not read from the request; it is stamped with the token's `sub` (records cannot be created on behalf of someone else).
- `update`: the owner field is never changed (ownership cannot be transferred through the API).
- If the `update`/`delete` rule contains `owner`: when the caller is not the owner and none of their roles is in the rule/`superRoles` → `ForbiddenException` → 403.
- If the `list`/`getById` rule contains `owner`: in the same situation only their own records are returned (someone else's record in getById is a 404 — existence is not leaked).

**The controller decides, the handler enforces.** The controller computes the role/ownership state and passes it to the command/query as a `[BindNever]`/`[JsonIgnore]` field (`RestrictToOwnerId`); the handler applies the filter/check only if this field is set. Rationale: gRPC server services call the same handlers without a user context (service-to-service, trusted calls) — if the check were done directly in the handler with `ICurrentUser`, service-to-service reads would come back empty. Because the field cannot be bound from the client (closed to model binding), it cannot be bypassed by the request.

**Role claim (`EnableJwt`):** `MapInboundClaims = false`, `RoleClaimType = "role"`, `NameClaimType = "sub"` — OpenIddict's short claim names stay as they are, and `[Authorize(Roles = ...)]` and `User.IsInRole` work without extra code. Breaking change: code in services that looks up claims with `ClaimTypes.Role`/`ClaimTypes.NameIdentifier` must now look for the short names (`role`/`sub`) (`CurrentUser.UserId` checks both).

**Identity (auth.yaml):**

```yaml
roles: [Admin, User, Editor]     # seeded; Admin and User are always added
registration:
  enabled: false                 # default: closed
  defaultRole: User
```

While registration is closed: `/api/account/register` returns 404, the SPA hides the register link, and **no account is created for a user arriving for the first time through an external provider (Google, etc.)** — external sign-in is also a registration path; only pre-existing users (added from the admin panel) can sign in with an external provider. Rationale for the closed default: a safe default for a public generator; projects that need registration explicitly write `enabled: true` in the spec.

**Known limitations:** `superRoles` does not bypass the tenant filter in multi-tenant services (§5.5) — a SuperAdmin also only sees data from their own `tenant_id`; platform-wide (cross-tenant) reads are a separate feature. Counter (`counters`) endpoints remain public regardless of `access`. Role names in a service spec are checked against the `roles` list of the sibling `identity/auth.yaml` if it is found (a warning otherwise); if it is not found, the check is skipped.

### 6.2. List Filters and Read Visibility

```yaml
Post:
  filterable: [Status, AuthorId]      # ?status=Live&authorId=... (equality; paginated lists only)
  readFilter:
    where: { IsPublished: true }      # everyone sees only these (AND)
    bypassRoles: [Admin]              # + the service's superRoles automatically
    bypassOwner: true                 # the owner also sees their own records (drafts)
```

- **filterable:** props, relation FKs (`{Relation}Id`) and external reference fields; types string/number/bool/guid/date/enum. Added to the list query as nullable properties; a filter that is not supplied is not applied. Cannot collide with pagination/search names (`Page`, `Search` …).
- **readFilter:** a generalization of the hand-written "hide drafts from anonymous users" pattern. Applied to list and getById; a record that does not match the condition is a 404 in getById (existence is not leaked). `where` values are translated to C# literals at generation time according to their type (bool/enum/string/int). Same principle as ownership (§6.1): the controller decides (`ApplyReadFilter`, `ReadFilterOwnerId` — `[BindNever]`, cannot be bound from the client), the handler enforces; gRPC service-to-service reads are not affected. For non-paginated lists it is applied in memory.
- **Why separate from the access rule?** `access` determines "who can enter this endpoint", `readFilter` determines "which rows those who enter will see"; in a blog where the list is public (`list: anonymous`) but drafts should only be visible to the author/admin, both are needed together.

### 6.3. User Profile Fields (`userProfile`)

Because Identity is not generated from a spec (the embedded reference service is copied), domain-specific user fields (e.g. `Specialty`, `DiplomaNo`, `VerificationStatus`) used to require a hand-written side entity + a hand-edited `user.proto`. Declarative fields were added to `auth.yaml`:

```yaml
userProfile:
  props:
    Specialty: string
    DiplomaNo: { type: string, nullable: true, maxLength: 32 }
    VerificationStatus: { type: enum, values: [Pending, Approved, Rejected], default: Pending, editableBy: admin, inToken: true }
```

- **Field definition** is the same as service `props` (`PropSpec`: type, nullable, maxLength, default, enum `values`) + two extra keys: `editableBy: self | admin` (default `self` — the user edits it from the profile page; `admin` only from the admin panel) and `inToken` (default `false`; if `true` the field becomes a JWT claim with its camelCase name — the value is stale until the token is refreshed). The `json` type and the use of `editableBy`/`inToken` in service specs are rejected; names colliding with Identity's own fields (`Email`, `FullName`, …) and standard claim names are invalid.
- **No separate table:** the fields are added directly to `ApplicationUser` (`partial` class; the generated `Entities/ApplicationUser.Profile.cs`). Enums become `User{Field}` C# enums, stored as strings in the DB (same principle as enums in services).
- **Schema sync:** Because Identity uses `EnsureCreated`, columns for fields added later to an existing database would not be created. The generated `UserProfile.EnsureColumnsAsync` runs `ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS` for every field at startup (NOT NULL columns are added with a default — `default` or the type's zero value — so existing rows stay valid). Columns are never dropped/retyped (risk of data loss; do it by hand).
- **API:** `GET /api/account/me` and admin user rows return a `profile` dictionary; `PUT /api/account/profile` accepts only `self` fields in `profile` (400 if an `admin` field is sent); `PUT /api/admin/users/{id}/profile` edits all fields. Partial update: fields not sent are unchanged. Values are validated by type (enum value, maxLength, nullable). `GET /api/account/profile-schema` returns field metadata — the shared sign-in SPA renders the profile and admin forms dynamically from it (the SPA is pre-built and embedded, so the fields are not known at build time).
- **gRPC:** `user.proto` is no longer a fixed file; it is generated from auth.yaml (`UserMessage` 1–4 fixed, profile fields from 5 onward in order). When resolving an `identity/User` external reference, CodeGen reads `identity/auth.yaml` in the workspace and generates the **same** proto and rich `UserReference` fields; if auth.yaml is not found it falls back to the profile-less embedded proto. If the field order changes, the proto numbers change — Identity and consuming services must be regenerated together.
- **Known limitations:** profile fields are not asked on the registration form (filled on the profile page after registration; `admin` fields are closed to the user anyway). Profile changes do not publish events (`publishes`) — Identity event sync is a separate open item.

## 7. Containerization

- A separate `Dockerfile` per service.
- All services come up with a single `docker-compose.yml`. **Note:** CodeGen currently generates a separate, isolated `docker-compose.yml` for each service (with its own Postgres); the shared root `docker-compose.yml` is only used for single-instance infrastructure (Postgres + RabbitMQ, see §5.2) — generated services connect to this shared broker via `host.docker.internal`.
- Configuration is read from the `.env` file; `.env.production` is used in production.

### 7.1. Service Registry (`ServiceRegistry`) — Port/Authority Accuracy

On every generation, `ServiceRegistry.cs` keeps a shared `services.json` at the workspace root (one level above the generated service folder): `Name`, `RestPort`, `GrpcPort`, `PostgresPort`, `IsIdentity`, `Authority`, `Audience`, `Protected`. It has two consumers:

- **The Identity dashboard** — during generation, the current state of this registry is copied into identity's own `wwwroot` and baked into the image (`SnapshotForIdentity`); the "Services" section reads it.
- **CodeGen itself** — when a service references `identity/User` (`via: grpc`), Identity's **real** gRPC port is read from this registry (`ServiceRegistry.LoadForWorkspace`); when a sibling service (non-identity) is referenced, the port is read directly from the sibling's own `spec.yaml` (`DockerPorts.Grpc`). **Decision change (2026-07-13):** Previously the `Grpc:{Provider}` address in appsettings.json had the port hardcoded as `8081` — regardless of which port the provider actually used. This went unnoticed as long as everyone used the default ports; now that ports routinely differ (see §7.2) it would become a real connection error. If no port is found in the registry/sibling spec, it silently falls back to the old default (`8081`, `8082` for identity) — no exception is thrown.

### 7.2. Designer — Auto-Incrementing Port/Authority Suggestions

The Designer reads this registry via `/api/workspace` and, when a **new** service/identity is opened (spec.yaml/auth.yaml not yet on disk), pre-fills the REST/gRPC/Postgres ports as **real, editable default values** (not just placeholder text), one above the highest value used in the workspace — the user can still change them by hand. Likewise the Authority field is suggested as `http://host.docker.internal:{identity's real REST port}` if there is an Identity entry in the workspace (previously hardcoded `http://localhost:5090` — an address never reachable from inside a Docker container, since `5090` is only Identity's local `dotnet run` port). After a service/identity is generated (both can be generated one after another in the same Designer session), the workspace is re-read; fields the user has not changed (still equal to the previous suggestion) are updated live, and a manually entered value is never overwritten.

## 8. Distribution

- The library packages are published to public NuGet (nuget.org): `BaseForge.Core`, `BaseForge.Infrastructure`, `BaseForge.API`, `BaseForge.Tools`, plus the `BaseForge.CodeGen` .NET tool (`baseforge` CLI).

## Decision Log

| Date | Decision | Status |
| --- | --- | --- |
| 2026-06-24 | Project skeleton (.NET 10, 3 src + 2 test projects, .slnx) set up | ✅ |
| 2026-06-24 | Opinionated Library + Clean Architecture + CQRS (MediatR) decisions taken from the original specification | ✅ |
| 2026-06-24 | Data access revised from the spec's "ADO.NET, no ORM" to **EF Core 10 (ORM) + Dapper (raw SQL)** | ✅ |
| 2026-06-24 | MediatR pinned at **12.5.0** for CQRS (last free/Apache-2.0 version; v13+ is commercial) | ✅ |
| 2026-06-24 | nuget.org **Trusted Publishing** (OIDC, `.github/workflows/publish.yml`) set up instead of a classic API key | ✅ |
| 2026-06-24 | Backlog "ER Diagram": **BaseForge.Tools** package + `DbmlGenerator` (EF Core model → DBML) added; source = EF Core model, output = DBML | ✅ |
| 2026-07-07 | Synchronous gRPC communication made real: automatic proto generation (server+client), rich sibling-spec resolution, `identity/User` special case, Kestrel two-port (h2c) fix. RabbitMQ still in backlog. | ✅ |
| 2026-07-10 | RabbitMQ async event pub/sub added: `IIntegrationEvent`/`IEventBus` (Core/Infrastructure, reuses MediatR for local dispatch), `EnableRabbitMq` (API, following the `EnableJwt` pattern), CodeGen `publishes`/`subscribes` (see §5.2). `via: event` permanently a no-op — reserved for a separate future feature. In the same pass, rich gRPC clients' `ProviderHost` was fixed to `host.docker.internal` (it never worked across isolated compose networks). | ✅ |
| 2026-07-13 | `json`/`jsonb` prop type added (see §5.3): `TypeMap` + `[Column(TypeName = "jsonb")]` only on the entity class (not on DTOs). | ✅ |
| 2026-07-13 | Append-only entity support added (see §5.4): `EntitySpec.AppendOnly` — Update/Delete command/handler/controller action are never generated; for the GMP/21 CFR Part 11 audit/trace scenario. | ✅ |
| 2026-07-13 | Multi-tenancy added (see §5.5): `ServiceSpec.MultiTenant`, `ITenantEntity`/`ICurrentTenant`/`EnableMultiTenancy()`, a combined soft-delete+tenant query filter via `Expression.AndAlso` in `BaseForgeDbContext`. Two real bugs were found and fixed during generation: (1) the generated DbContext constructor never forwarded `ICurrentUser`/`ICurrentTenant`, (2) `Expression.Constant(this)` in the query filter used the runtime type, so the `private CurrentTenantId` property could not be found by reflection on the derived type (fixed with `Expression.Constant(this, typeof(BaseForgeDbContext))`, verified with a unit test). | ✅ |
| 2026-07-13 | Docker port/Authority accuracy added (see §7.1/7.2): Postgres port + public `LoadForWorkspace` in `ServiceRegistry`; the gRPC cross-service appsettings address now uses the real port (read from the provider's own spec or the identity registry) — previously hardcoded `8081`, a real connection bug. The Designer now pre-fills ports/Authority for a new service/identity from the workspace registry without collisions (editable); suggestions update live when Identity and a regular service are generated back-to-back in the same session. | ✅ |
| 2026-07-16 | Transactional Outbox Pattern added (see §5.2): `IEventBus` no longer writes to RabbitMQ directly; `OutboxEventBus` writes the event as an `OutboxMessage` row in the same `SaveChangesAsync` transaction; `OutboxPublisherHostedService` (multi-instance safe with `FOR UPDATE SKIP LOCKED`) sends it to the real broker through a separate, reliable relay. The dual-write / event loss risk between DB commit and RabbitMQ publish is solved (with an at-least-once guarantee). `RabbitMqEventBus` refactored into `RabbitMqPublisher`/`IRabbitMqPublisher` (wire format unchanged). | ✅ |
| 2026-07-16 | Centralized logging (Serilog + Grafana Loki) + Correlation ID added (see §5.6): `ICorrelationIdAccessor` (AsyncLocal) is shared between the HTTP middleware, gRPC client/server interceptors and the RabbitMQ outbox/consumer — a request is logged with the same id across all three boundaries (HTTP/gRPC/RabbitMQ). `AddBaseForgeLogging` (Host level, separate from `AddBaseForge`) always writes to the console, and to Loki if `Serilog:LokiUrl` is set. Shared `loki`/`grafana` containers added to the root `docker-compose.yml`. Always on (not an opt-in spec toggle like RabbitMQ/JWT) — `ServiceSpec`/Designer UI untouched. | ✅ |
| 2026-09-26 | Role + ownership based authorization model added (see §6.1): `entity.access` (anonymous / authenticated / role list + `owner`), `entity.ownerField`, `auth.defaultAccess`, `auth.superRoles`; `roles` and a **closed-by-default** `registration` in Identity (first sign-in with an external provider also counts as registration). The controller decides, the handler enforces via `RestrictToOwnerId` — gRPC's user-context-less service-to-service reads are not affected. `EnableJwt` now uses `MapInboundClaims=false` + `RoleClaimType="role"`. Backward compatible: specs that do not use `access` generate exactly the same controllers. Two pre-existing bugs fixed in the same pass: image upload returned 500 because a new service had no `wwwroot`; the role list in the Identity admin panel was fixed to `Admin`/`User`. **Release note:** because the generated code uses new `BaseController` helpers, a new version of BaseForge.API must be released together with the generator (`CodeGenerator.BaseForgeVersion`). | ✅ |
| 2026-09-26 | User profile fields added (see §6.3): `auth.yaml` `userProfile.props` (`editableBy: self\|admin`, `inToken`), fields directly on `ApplicationUser` (partial) instead of a separate table; `user.proto` generated from auth.yaml and `identity/User` consumers get the same proto; schema sync to an existing DB with `ADD COLUMN IF NOT EXISTS`; dynamic profile/admin forms in the shared sign-in SPA from a metadata endpoint. | ✅ |
