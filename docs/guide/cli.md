# CLI Reference

The `baseforge` command is installed with the `BaseForge.CodeGen` .NET tool:

```bash
dotnet tool install -g BaseForge.CodeGen --prerelease
dotnet tool update  -g BaseForge.CodeGen --prerelease
```

## Commands

| Command | Description |
| --- | --- |
| `baseforge new <service>` | Open the [Designer](/guide/designer) with an empty spec |
| `baseforge update <service>` | Open the Designer with `<service>/spec.yaml` (and `identity/auth.yaml`, if present) loaded |
| `baseforge new-service --spec <file.yaml>` | Generate the ER diagram, ask for confirmation, then generate the service |
| `baseforge new-identity --spec <auth.yaml>` | Generate the config-driven central Identity service |
| `baseforge er --spec <file.yaml>` | Generate only a draw.io ER diagram from a spec |

## Options

| Option | Applies to | Description |
| --- | --- | --- |
| `--spec <file>` | `new-service`, `new-identity`, `er` | The YAML spec file (required) |
| `--output <folder>` | `new-service`, `new-identity`, `er` | Output folder (`er`: `.`, `new-service`: `./<service>`) |
| `--yes` | `new-service` | Continue without asking for confirmation |
| `--port <n>` | `new`, `update` | Designer port (default `3500`) |
| `--no-browser` | `new`, `update` | Don't open the browser automatically (remote/headless machines) |

## Workspace layout

Run the CLI from a **workspace folder**. Each service is generated into its own sub-folder, and a shared `services.json` registry at the root keeps track of ports and the Identity address so services can find each other:

```
my-platform/
├── services.json        ← shared registry (ports, authority)
├── observability/       ← Loki + Grafana, created on first generation
├── identity/
│   ├── auth.yaml
│   └── …
├── products/
│   ├── spec.yaml
│   └── …
└── orders/
    ├── spec.yaml
    └── …
```

Specs that reference each other (`externalRefs`, `subscribes`) are resolved by looking for sibling specs in the same workspace.

## ER diagrams from an EF Core model

Besides `baseforge er` (spec → draw.io), the `BaseForge.Tools` package turns any EF Core model into [DBML](https://dbml.dbdiagram.io/) you can paste into [dbdiagram.io](https://dbdiagram.io):

```csharp
using BaseForge.Tools;

// Only the model is read — no database connection is needed.
string dbml = DbmlGenerator.Generate(dbContext);
File.WriteAllText("docs/er.dbml", dbml);
```

Real table/column names, provider-specific column types (e.g. PostgreSQL `uuid` / `timestamptz`), primary keys and foreign-key relations are reflected.
