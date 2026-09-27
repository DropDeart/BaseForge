# Coding Conventions

This document defines the coding standards to follow in BaseForge and in microservices that use it.

## Naming Conventions

| Element | Rule | Example |
| --- | --- | --- |
| Interface | Starts with the `I` prefix | `IRepository<T>`, `ICommand` |
| Base class | Starts with the `Base` prefix | `BaseEntity`, `BaseController` |
| Handler | Ends with `Handler` | `CreateUserCommandHandler` |
| Query | Ends with the `Query` suffix | `GetUserByIdQuery` |
| Command | Ends with the `Command` suffix | `CreateUserCommand` |

## Layer Rules

- The `Core` layer takes no external dependencies (MediatR interfaces only).
- The `Infrastructure` layer depends on `Core`; it **must not** depend on `API`.
- The `API` layer may depend on both.

## Language and Style

- Language: **C# / .NET 10**, `LangVersion=latest`.
- `Nullable` and `ImplicitUsings` are **enabled** in all projects.
- `TreatWarningsAsErrors=true` — warnings are errors. Analysis level: `latest-recommended`.
- Public members have XML docs (`GenerateDocumentationFile=true`).
- Deliberate exceptions are managed with `NoWarn` (e.g. `CA1707` in test projects).

## Data Access

- **EF Core 10** is the primary ORM: writes, change tracking and migrations. Most CRUD is written with LINQ.
- **Dapper** is used for raw SQL in heavy reads / complex join queries (result → DTO mapping). It runs over the EF `DbContext`'s connection.
- When SQL is written by hand (Dapper or EF `FromSql`), **parameterized** queries are mandatory (against SQL injection).
- In queries written with Dapper the soft-delete condition (`is_deleted = false`) is added by hand; the EF global query filter does not cover Dapper.
- All entities derive from `BaseEntity`; audit fields (`CreatedAt`, `UpdatedAt`, `CreatedBy`) and soft delete are managed automatically via EF `SaveChanges` / query filters.

## CQRS

- Command/query separation is applied.
- There is exactly one handler per command/query.
- No CQRS library other than MediatR is added.

## Folder Layout

- Entity bases → `Core/Entities`
- Contracts → `Core/Interfaces`, `Core/CQRS`
- Exception types → `Core/Exceptions`
- Repository implementations → `Infrastructure/Repositories`
- Data access / query helpers → `Infrastructure/Data`
- DI extensions → `Infrastructure/Extensions`, `API/Extensions`
- Controller base + middleware → `API/Controllers`, `API/Middleware`

## Tests

- Unit tests live in `BaseForge.UnitTests`, integration tests in `BaseForge.IntegrationTests`.
- Test method names use the `MethodName_Condition_ExpectedResult` format (underscores are allowed).
- Framework: xUnit.

## Commits & Branches

- Prefer meaningful, small commits.
- `docs/ARCH.md` is updated before a new feature is added.
