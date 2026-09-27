# Getting Started

There are two ways to use BaseForge. Most people start with the **generator** — it produces a complete service that already uses the library. You can also add the **library** to an existing project by hand.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (to run generated services and their PostgreSQL / RabbitMQ / Loki)

## Option A — Generate a service (recommended)

### 1. Install the CLI

```bash
dotnet tool install -g BaseForge.CodeGen --prerelease
```

This installs the `baseforge` command. Update it later with `dotnet tool update -g BaseForge.CodeGen --prerelease`.

### 2. Open the Designer

Create (or `cd` into) a workspace folder — every service you generate will live in a sub-folder next to the others:

```bash
mkdir my-platform && cd my-platform
baseforge new orders
```

Your browser opens the **Designer** at `http://localhost:3500`. Add entities and fields, draw relations, pick access rules — the ER diagram updates live. See the [Designer guide](/guide/designer) for a tour.

### 3. Generate, build and run

Press **Generate + Build**. BaseForge writes `orders/spec.yaml` plus the full source code, then runs `dotnet build` and shows you the result. Press **Run** to start the whole stack with Docker Compose:

```bash
# or from the terminal
cd orders
docker compose up --build -d
```

Open `http://localhost:8080/scalar/v1` to explore the generated REST API.

### 4. Keep iterating

Come back any time to add entities or change rules — the Designer loads the existing spec:

```bash
baseforge update orders
```

::: tip Prefer YAML?
Everything the Designer does is stored in a plain `spec.yaml`. You can write it by hand and generate from the terminal with `baseforge new-service --spec orders.yaml`. See the [Service Spec reference](/guide/service-spec).
:::

## Option B — Use the library directly

Add the packages to an ASP.NET Core project:

```bash
dotnet add package BaseForge.API --prerelease
dotnet add package BaseForge.Infrastructure --prerelease
dotnet add package BaseForge.Core --prerelease
```

Create a `DbContext` that derives from `BaseForgeDbContext`, then wire BaseForge up in `Program.cs`:

```csharp
using BaseForge.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Structured logging (console, plus Grafana Loki if Serilog:LokiUrl is set)
builder.AddBaseForgeLogging("orders");

builder.Services.AddControllers();
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL<OrdersDbContext>(
        builder.Configuration.GetConnectionString("Default")!);
    options.EnableCQRS(typeof(Program).Assembly);
    options.EnableAuditLog();

    // Optional building blocks
    options.EnableJwt(jwt =>
    {
        jwt.Authority = "http://localhost:5090";
        jwt.Audience = "baseforge-api";
    });
    options.EnableRabbitMq(mq => mq.Host = "localhost");
});

var app = builder.Build();
app.UseBaseForge();   // correlation id, exception handling, request logging, /health
app.MapControllers();
app.Run();
```

Now define entities by deriving from `BaseEntity`, write commands and queries with `ICommand` / `IQuery`, and inherit your controllers from `BaseController`. Audit fields, soft delete, exception → HTTP status mapping and correlation ids are handled for you.

## What's next?

- [Designer](/guide/designer) — a tour of the visual editor
- [Service Spec](/guide/service-spec) — every YAML option explained
- [Identity](/guide/identity) — central authentication, social logins, roles
- [Architecture](/architecture) — the decisions behind BaseForge and why they were made
