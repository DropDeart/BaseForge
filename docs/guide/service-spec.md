# Service Spec (`spec.yaml`)

A service is described by a single YAML file. The Designer reads and writes this file, and `baseforge new-service --spec <file>` generates a service from it.

## A complete example

```yaml
service: blog
database: blog_db

auth:
  authority: http://host.docker.internal:8081
  audience: baseforge-api
  protect: true
  defaultAccess: authenticated
  superRoles: [SuperAdmin]

corsOrigins: [http://localhost:5173]

entities:
  Post:
    props:
      Title: { type: string, maxLength: 200 }
      Body: text
      Status: { type: enum, values: [Draft, Published], default: Draft }
      AuthorId: guid
      ViewCount: int
    counters: [ViewCount]
    ownerField: AuthorId
    access:
      list: anonymous
      getById: anonymous
      update: [Admin, owner]
      delete: [Admin]
    filterable: [Status, AuthorId]
    readFilter:
      where: { Status: Published }
      bypassRoles: [Admin]
      bypassOwner: true
    externalRefs:
      author: { target: identity/User, store: AuthorId, via: grpc }

  Comment:
    props:
      Body: text
    relations:
      post: { kind: many-to-one, target: Post }
    publishes: [created]

subscribes:
  - event: blog/CommentCreated
    handler: NotifyPostAuthorOnComment
```

## Service-level keys

| Key | Type | Description |
| --- | --- | --- |
| `service` | string | Service name — project, namespace and folder are derived from it |
| `database` | string | PostgreSQL database name |
| `entities` | map | Entity name → entity definition (below) |
| `auth` | object | JWT connection to the central Identity — omit for a public service |
| `auth.authority` / `auth.audience` | string | Identity address and API audience (default `baseforge-api`) |
| `auth.protect` | bool | Put `[Authorize]` on controllers (default `true`) |
| `auth.defaultAccess` | rule | Access for actions not listed in an entity's `access` (default `authenticated`) |
| `auth.superRoles` | list | Roles that pass every role and ownership rule |
| `multiTenant` | bool | Add `TenantId` + isolation to every entity ([details](/architecture#_5-5-multi-tenancy)) |
| `corsOrigins` | list | Allowed browser origins (`Cors:AllowedOrigins`) |
| `dockerPorts` | object | `rest`, `grpc`, `postgres` host ports (empty = defaults) |
| `rabbitMqTuning` | object | `outboxMaxRetries`, `outboxRetentionDays` |
| `subscribes` | list | Events this service listens to: `event: service/EntityKind`, `handler: ClassName` |
| `gateway` | object | `proxiedServices: [a, b]` — expose other services under `/api/gateway/{service}` via YARP |

## Entity keys

| Key | Type | Description |
| --- | --- | --- |
| `props` | map | Field name → type (short form) or field object (rich form) |
| `relations` | map | Relations to entities in the same service |
| `externalRefs` | map | References to entities in other services |
| `publishes` | list | `created` / `updated` / `deleted` — publish integration events via the outbox |
| `access` | map | Per-action rule: `list`, `getById`, `create`, `update`, `delete` |
| `ownerField` | string | A `guid` field stamped with the caller's id on create |
| `anonymousActions` | list | Legacy shorthand for `access: { action: anonymous }` |
| `filterable` | list | Fields exposed as equality filters on the list endpoint |
| `readFilter` | object | Row visibility: `where`, `bypassRoles`, `bypassOwner` |
| `counters` | list | `int` fields that get a public `POST /{id}/increment-{field}` endpoint |
| `appendOnly` | bool | Never generate Update/Delete (audit/trace records) |
| `paginated` / `sortable` / `searchable` | bool | List behavior (all default `true`) |

## Field types

| Spec type | C# type | PostgreSQL type |
| --- | --- | --- |
| `string`, `text` | `string` | `text` |
| `int` / `long` / `short` | `int` / `long` / `short` | `integer` / `bigint` / `smallint` |
| `decimal` | `decimal` | `numeric` |
| `double` / `float` | `double` / `float` | `double precision` / `real` |
| `bool` | `bool` | `boolean` |
| `datetime` | `DateTimeOffset` | `timestamptz` |
| `date` | `DateOnly` | `date` |
| `guid`, `uuid` | `Guid` | `uuid` |
| `json` | `string` | `jsonb` |
| `enum` | generated C# enum | `text` (stored by name) |

Short form and rich form can be mixed:

```yaml
props:
  Title: string                                     # short form
  Sku: { type: string, maxLength: 32 }              # rich form
  Discount: { type: decimal, nullable: true }
  Status: { type: enum, values: [Draft, Active], default: Draft }
```

Rich-form keys: `type`, `nullable`, `maxLength` (string/text only), `default`, `values` (enum only).

## Relations

```yaml
relations:
  category: { kind: many-to-one, target: Category, nullable: true }
  lines:    { kind: one-to-many, target: OrderLine }
```

`kind` is `one-to-many`, `many-to-one` or `one-to-one`. `many-to-one`/`one-to-one` put a foreign key (`{Relation}Id`) on this entity; `nullable: true` makes it optional.

## External references

```yaml
externalRefs:
  product: { target: products/Product, store: ProductId, via: grpc }
```

No foreign key is created — only an ID column (`store`). With `via: grpc` BaseForge generates a typed gRPC client. If a sibling spec (`products.yaml`) is found next to this one, the client carries the target's real fields; otherwise it falls back to an ID-only stub. `identity/User` is always available.

## Access rules

A rule is one of:

- `anonymous` — no sign-in required
- `authenticated` — any signed-in user
- a role list, e.g. `[Admin, Editor]` — optionally including `owner`

`owner` means "the user whose id is in `ownerField`". Non-owners get **403** on update/delete, **404** on someone else's record in `getById`, and only their own rows in `list`. See [Architecture §6.1](/architecture#_6-1-authorization-model-roles-ownership) for the full model.

## Validation

Specs are validated before any code is written — invalid combinations (e.g. `maxLength` on an `int`, `updated` in `publishes` of an append-only entity, an `owner` rule without `ownerField`) fail loudly with a clear message instead of generating broken code.
