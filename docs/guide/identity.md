# Identity & Authorization

BaseForge uses **one central Identity service** for the whole platform. It issues tokens; every other service validates them **locally** (no call to Identity per request).

Identity is a ready-made reference implementation built on **OpenIddict + ASP.NET Identity**. You don't write it — you configure it with an `auth.yaml` and generate it.

## Generate an Identity service

From the Designer's **I** view, or from the terminal:

```bash
baseforge new-identity --spec auth.yaml --output ./identity
```

What you get:

- OAuth2 / OpenID Connect endpoints (`/connect/token`, `/connect/authorize`, discovery, JWKS)
- A shared sign-in SPA (login, registration, profile page, admin panel)
- Social sign-in: Google, GitHub, Microsoft, Facebook, Apple
- A gRPC `User` service, so other services can resolve `identity/User` references
- A dashboard listing every service in the workspace with **live health status**

## `auth.yaml`

```yaml
service: identity
database: identity_db
issuer: http://localhost:5090/

scopes:
  - name: api
    resource: baseforge-api

clients:
  - clientId: web                  # browser app: authorization_code + PKCE
    public: true
    grants: [authorization_code, refresh_token]
    scopes: [api]
    redirectUris: [http://localhost:3000/callback]
  - clientId: service-worker       # service-to-service
    public: false
    grants: [client_credentials]
    scopes: [api]

seedAdmin:
  email: admin@example.com

roles: [Editor, SuperAdmin]        # Admin and User always exist
registration:
  enabled: false                   # closed by default
  defaultRole: User

providers:
  google: { clientId: "..." }      # secrets go to .env
  github: { clientId: "..." }

corsOrigins: [http://localhost:3000]
```

::: warning Secrets never go into YAML
Client secrets, the seed admin password and the signing certificate password are written to a git-ignored `.env` file (e.g. `Auth__Providers__Google__ClientSecret`). `appsettings.json` and the copied `auth.yaml` keep them empty.
:::

## Connecting a service

In a service spec, add an `auth` block (or flip the **Connect to the central Identity with JWT** toggle in the Designer):

```yaml
auth:
  authority: http://host.docker.internal:8081
  audience: baseforge-api
  protect: true
```

The generated `Program.cs` calls `options.EnableJwt(...)`. In production, override the address with `Auth__Authority` and set `Auth__Issuer` to Identity's issuer.

## Authorization

Authentication answers *"who are you?"*; authorization answers *"may you do this?"*. BaseForge generates both from the spec.

```yaml
auth:
  defaultAccess: authenticated     # for actions not listed below
  superRoles: [SuperAdmin]         # pass every rule automatically
entities:
  Order:
    ownerField: BuyerId            # stamped from the token on create
    access:
      list: [Admin, owner]         # admins see all, others only their own
      create: authenticated
      update: [owner]
      delete: [Admin]
```

| Rule | Generated as |
| --- | --- |
| `anonymous` | `[AllowAnonymous]` |
| `authenticated` | `[Authorize]` |
| `[Admin, Editor]` | `[Authorize(Roles = "Admin,Editor")]` |
| `[Admin, owner]` | `[Authorize]` + an ownership check |

- The owner field can't be forged on create or changed on update.
- Non-owners: **403** on update/delete, **404** on `getById` of someone else's record, only their own rows in `list`.
- The controller makes the decision and the handler enforces it — so trusted gRPC calls between services are not affected.

Combine it with a **visibility filter** to hide rows (e.g. drafts) from everyone except admins and the author:

```yaml
readFilter:
  where: { IsPublished: true }
  bypassRoles: [Admin]
  bypassOwner: true
```

## User profile fields

Add domain-specific fields to the user without a side table:

```yaml
userProfile:
  props:
    Specialty: string
    DiplomaNo: { type: string, nullable: true, maxLength: 32 }
    VerificationStatus:
      type: enum
      values: [Pending, Approved, Rejected]
      default: Pending
      editableBy: admin      # only admins can change it
      inToken: true          # becomes a JWT claim
```

The sign-in SPA renders the profile and admin forms automatically, `/api/account/me` returns them, and services that reference `identity/User` receive them over gRPC.

For the full reasoning behind these designs, see [Architecture §6](/architecture#_6-authentication).
