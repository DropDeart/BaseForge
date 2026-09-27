# The Designer

The Designer is a browser-based editor for BaseForge specs. It ships **inside** the `baseforge` .NET tool — there is nothing extra to install.

```bash
baseforge new orders           # start a new service
baseforge update orders        # open an existing orders/spec.yaml (and identity/auth.yaml, if present)
baseforge new orders --port 4000 --no-browser
```

It opens at `http://localhost:3500` (the next free port is used if that one is taken). Everything you do is saved to plain YAML on disk when you generate — the Designer is a convenience, not a lock-in.

## Layout

The icon rail on the left switches between three views, plus the language switch and the close button:

| Rail | View | What you do there |
| --- | --- | --- |
| **S** | Service | Service settings, entities, fields, relations, external references, access rules, list filters |
| **I** | Identity | The central auth service: providers, seed admin, roles, registration, user profile fields |
| **E** | ER diagram | A live diagram of the current spec; copy DBML or open it in dbdiagram.io |
| **EN / TR** | Language | Switch the interface language (remembered in your browser) |
| **⏻** | Close | Stops the Designer, its port and any containers it started |

## Service view

### Service settings

- **Service name / Database** — used for the project name, namespace and PostgreSQL database.
- **Connect to the central Identity with JWT** — turns on JWT validation (`authority`, `audience`) and `[Authorize]` protection. When on, you also get **default access** and **super roles** (see [Identity → Authorization](/guide/identity#authorization)).
- **Multi-tenancy** — adds `TenantId` and tenant isolation to every entity.
- **REST / gRPC / Postgres ports** — host ports for Docker. The Designer reads the workspace registry and pre-fills ports that don't collide with services you already generated.
- **Outbox tuning** — max retries and retention days for services that publish events.

### Entities and fields

Add entities on the left. For each one you can:

- Add **fields** and pick a type from the dropdown. The ⚙ button reveals `nullable`, `maxLength`, `default`, enum values and the `counter` flag.
- Toggle **Pagination**, **Sorting**, **Search** and **Append-only**.
- Choose an **owner field** and an **access rule per action** (`list`, `getById`, `create`, `update`, `delete`).
- Pick **list filters** (equality query parameters) and a **visibility filter** (e.g. hide drafts from everyone except admins and the author).
- Draw **relations** to other entities in the same service (`one-to-many`, `many-to-one`, `one-to-one`, optionally nullable).
- Add **external references** to entities in other services (`service/Entity`, stored as an ID, resolved via gRPC).

Audit fields — `Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, soft delete — come from `BaseEntity`; you never add them yourself.

## Identity view

Configure the central authentication service that every other service trusts:

- **Central settings** — service name, database, issuer and ports.
- **Providers** — Google, GitHub, Microsoft, Facebook (and Apple via YAML). Toggle one on and paste its client id/secret. Secrets are written to `.env`, never to committed files.
- **Seed admin** — the first admin account.
- **Roles** — `Admin` and `User` always exist; add your own (e.g. `Editor`, `SuperAdmin`).
- **Self-registration** — off by default; when on, choose the role new users receive.
- **Profile fields** — domain-specific fields added to the user, editable by the user or only by admins, optionally included in the token.

## Generate, build, run

**Generate + Build** writes the spec and the full source, runs `dotnet build`, and shows the file list and build result. If a `.slnx`/`.sln` is found nearby you can have the new project added to it.

After a successful build:

- **Run** starts the service with `docker compose up --build -d --wait` and links you to the API reference (Scalar) or, for Identity, the sign-in page.
- **Stop** runs `docker compose down`.

## ER diagram view

The diagram is drawn live from the spec: solid connectors are in-service foreign keys, dashed boxes are external service references (no FK, just an ID). **Copy DBML** puts a [DBML](https://dbml.dbdiagram.io/) version on your clipboard; **Open in dbdiagram.io** does the same and opens the site.

::: tip Closing
Close the Designer with **⏻** or the **Close** button. If you just close the tab, the Designer notices the missing heartbeat and shuts itself down after a few minutes.
:::
