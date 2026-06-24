# BaseForge

.NET 10 tabanlı, mikroservis mimarisine uygun, yeniden kullanılabilir bir **base library**. Yeni backend projeleri için sıfırdan mimari kurmak yerine bu lib extend edilerek kullanılır. Public NuGet paketi olarak yayınlanacaktır.

## Teknoloji Stack

| Katman | Teknoloji |
| --- | --- |
| Framework | .NET 10 |
| Mimari | Clean Architecture + CQRS + Repository Pattern |
| CQRS | MediatR |
| Servisler arası (sync) | gRPC |
| Servisler arası (async) | RabbitMQ |
| Auth | Merkezi Identity Service + JWT / OAuth2 |
| Veritabanı | PostgreSQL (her mikroservis kendi DB'si) |
| ORM / Data Access | ADO.NET (raw SQL, query builder) |
| Container | Docker + Docker Compose |
| Paket | Public NuGet (nuget.org) |

## NuGet Paketleri

| Paket | Açıklama |
| --- | --- |
| `BaseForge.Core` | Sadece interface ve entity base'leri (dış bağımlılık yok) |
| `BaseForge.Infrastructure` | Repository implementasyonları, ADO.NET builder |
| `BaseForge.API` | Controller base, middleware, DI extension'ları |

## Hızlı Başlangıç

```csharp
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL(connectionString);
    options.EnableCQRS();
    options.EnableAuditLog();
});
```

## Yapı

```
src/
  BaseForge.Core/            → Entity base'leri, interface'ler, CQRS sözleşmeleri, exception'lar
  BaseForge.Infrastructure/  → GenericRepository, ADO.NET query builder, DI extension'ları
  BaseForge.API/             → BaseController, middleware, AddBaseForge()
tests/
  BaseForge.UnitTests/
  BaseForge.IntegrationTests/
docs/
  ARCH.md                    → Detaylı mimari kararlar
  CONVENTIONS.md             → Kod standartları ve naming kuralları
```

## Geliştirme

```bash
dotnet build      # tüm solution
dotnet test       # testler
```

> Bu proje Claude Code ile birlikte geliştirilmektedir. Mimari ve kod kuralları için bkz. [`CLAUDE.md`](CLAUDE.md), [`docs/ARCH.md`](docs/ARCH.md), [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md).

## Lisans

MIT
