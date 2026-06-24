# BaseForge — CLAUDE.md

Bu dosya, BaseForge projesinin mimari kurallarını ve geliştirme kontratını içerir. Claude Code bu dosyayı otomatik okur ve tüm geliştirme sürecinde referans alır.

## Proje Özeti

**BaseForge**, .NET 10 tabanlı, mikroservis mimarisine uygun, tekrar kullanılabilir bir base library'dir. Her yeni backend projesinde sıfırdan mimari kurmak yerine bu lib extend edilerek kullanılır. Public NuGet paketi olarak yayınlanacaktır.

## Teknoloji Stack

| Katman | Teknoloji |
| --- | --- |
| Framework | .NET 10 |
| Mimari | Clean Architecture + CQRS + Repository Pattern |
| CQRS | MediatR (sıfırdan yazılmayacak) |
| Servisler arası (sync) | gRPC |
| Servisler arası (async) | RabbitMQ |
| Auth | Merkezi Identity Service + JWT / OAuth2 |
| Veritabanı | PostgreSQL (her mikroservis kendi DB'si) |
| ORM / Data Access | ADO.NET (raw SQL, query builder) |
| Container | Docker + Docker Compose |
| Paket | Public NuGet (nuget.org) |

## Mimari Kararlar

### Library mi, Framework mi?

**Library** olarak geliştirilecek. "Opinionated Library" yaklaşımı benimsenmiştir:

- Library esnekliği korunur
- Framework kolaylığı sağlanır
- Tek satır DI entegrasyonu hedeflenir

```csharp
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL(connectionString);
    options.EnableCQRS();
    options.EnableAuditLog();
});
```

### Mikroservis Kuralları

- Her mikroservis kendi veritabanına sahiptir (Database per Service pattern)
- Senkron iletişim → gRPC
- Asenkron iletişim → RabbitMQ (fire and forget, event-driven)
- Auth merkezi tek bir Identity Service üzerinden yapılır
- Her servis JWT token'ı kendi middleware'ide validate eder (merkezi DB çağrısı olmadan)

### CQRS Yaklaşımı

- MediatR üzerine inşa edilir
- `ICommand`, `IQuery`, `IHandler` base interface'leri lib içinde tanımlanır
- Her servis bu interface'leri extend eder

### Veritabanı

- ORM kullanılmaz, ADO.NET tercih edilir
- Base query builder lib içinde bulunur
- Soft delete, audit log (`CreatedAt`, `UpdatedAt`, `CreatedBy`) `BaseEntity`'de tanımlıdır

## Klasör Yapısı

```
BaseForge/
├── src/
│   ├── BaseForge.Core/
│   │   ├── Entities/          → BaseEntity, IAuditEntity, ISoftDelete
│   │   ├── Interfaces/        → IRepository, IUnitOfWork
│   │   ├── CQRS/              → ICommand, IQuery, IHandler (MediatR üzeri)
│   │   └── Exceptions/        → BaseException, NotFoundException, ValidationException
│   │
│   ├── BaseForge.Infrastructure/
│   │   ├── Repositories/      → GenericRepository implementasyonu
│   │   ├── Data/              → DbContext base, ADO.NET query builder
│   │   └── Extensions/        → DI extension metodları
│   │
│   └── BaseForge.API/
│       ├── Controllers/       → BaseController
│       ├── Middleware/        → Exception, Auth, Logging middleware
│       └── Extensions/        → builder.AddBaseForge()
│
├── tests/
│   ├── BaseForge.UnitTests/
│   └── BaseForge.IntegrationTests/
│
├── docs/
│   ├── ARCH.md               → Detaylı mimari kararlar
│   └── CONVENTIONS.md        → Kod standartları ve naming kuralları
│
├── CLAUDE.md                 → Bu dosya
└── docker-compose.yml
```

## NuGet Paket Yapısı

```
BaseForge.Core            → Sadece interface ve entity base'leri (bağımlılık yok)
BaseForge.Infrastructure  → Repository implementasyonları, ADO.NET builder
BaseForge.API             → Controller base, middleware, DI extensions
```

## Geliştirme Kuralları

### Naming Conventions

- Interface'ler `I` prefix'i ile başlar: `IRepository<T>`, `ICommand`
- Base sınıflar `Base` prefix'i ile başlar: `BaseEntity`, `BaseController`
- Handler'lar `Handler` ile biter: `CreateUserCommandHandler`
- Query'ler `Query` suffix'i ile biter: `GetUserByIdQuery`
- Command'lar `Command` suffix'i ile biter: `CreateUserCommand`

### Katman Kuralları

- `Core` katmanı hiçbir dış bağımlılık almaz (sadece MediatR interface'leri)
- `Infrastructure` katmanı `Core`'a bağımlıdır, `API`'ye bağımlı olamaz
- `API` katmanı her ikisine de bağımlı olabilir

## Her Yeni Mikroservis İçin Checklist

- [ ] `BaseForge.Core` NuGet paketi eklenir
- [ ] `BaseForge.Infrastructure` NuGet paketi eklenir
- [ ] `BaseForge.API` NuGet paketi eklenir
- [ ] `builder.Services.AddBaseForge()` çağrılır
- [ ] Kendi DB migration'ları tanımlanır
- [ ] gRPC proto dosyası oluşturulur
- [ ] docker-compose.yml'a servis eklenir
- [ ] JWT middleware konfigüre edilir

## Docker Kuralları

- Her servis için ayrı Dockerfile bulunur
- Tüm servisler tek bir `docker-compose.yml` üzerinden ayağa kalkar
- Environment variable'lar `.env` dosyasından okunur
- Production'da `.env.production` kullanılır

## Agentic Workflow

Bu proje Claude Code ile birlikte geliştirilmektedir.

- `CLAUDE.md` → Claude'un ana referans dosyası (bu dosya)
- `docs/ARCH.md` → Detaylı mimari kararlar ve gerekçeleri
- `docs/CONVENTIONS.md` → Kod yazım standartları

### Claude'un özel talimatları

- Yeni bir özellik eklerken önce `docs/ARCH.md` güncellenir
- Kod üretilirken yukarıdaki naming conventions'a uyulur
- Her zaman Clean Architecture katman kurallarına uyulur
- ADO.NET tercih edilir, Entity Framework Core kullanılmaz
- MediatR dışında yeni bir CQRS kütüphanesi eklenmez

## Gelecek Planlar (Backlog)

- [ ] Code Generator: Entity tanımından otomatik servis, repository ve SQL üretimi
- [ ] ER Diagram görselleştirme
- [ ] gRPC proto generator
- [ ] Identity Service referans implementasyonu
- [ ] Örnek mikroservis projesi (BaseForge kullanan demo)
