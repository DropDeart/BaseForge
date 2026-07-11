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
| ORM / Data Access | EF Core 10 (yazma + change tracking) + Dapper (ham SQL okuma / karmaşık join) |
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

- **EF Core 10** birincil ORM'dir: yazma (insert/update/delete), change tracking ve migration EF Core ile yapılır; CRUD'un çoğu LINQ ile yazılır.
- **Dapper** ağır okuma ve karmaşık join sorgularında ham SQL için kullanılır (sonuç → DTO mapping). Dapper, EF'in `DbContext`'iyle aynı `DbConnection` ve transaction üzerinde çalışır.
- Karmaşık join'lerde elle SQL serbesttir (EF `FromSql` veya Dapper). Parametreli sorgu zorunlu (SQL injection'a karşı).
- Soft delete, audit log (`CreatedAt`, `UpdatedAt`, `CreatedBy`) `BaseEntity`'de tanımlıdır; EF `SaveChanges` sırasında otomatik doldurulur, soft delete global query filter ile uygulanır.

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
│   │   ├── Repositories/      → GenericRepository (EF Core) implementasyonu
│   │   ├── Data/              → DbContext base (EF Core), Dapper bağlantı/sorgu yardımcıları
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
BaseForge.Infrastructure  → Repository implementasyonları (EF Core), Dapper sorgu yardımcıları, DbContext base, RabbitMQ event bus
BaseForge.API             → Controller base, middleware, DI extensions
BaseForge.Tools           → Geliştirme araçları (EF Core model'inden DBML ER diyagramı)
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
- Veri erişimi: EF Core 10 birincil ORM'dir (yazma/tracking/migration); ağır okuma ve karmaşık join'lerde Dapper ile ham SQL yazılır
- MediatR dışında yeni bir CQRS kütüphanesi eklenmez

## Gelecek Planlar (Backlog)

- [x] Code Generator: Entity tanımından otomatik servis, repository ve SQL üretimi → `BaseForge.CodeGen` (CLI + Designer web arayüzü, `baseforge new/update/new-service`)
- [x] ER Diagram görselleştirme → `BaseForge.Tools.DbmlGenerator` (EF Core model → DBML / dbdiagram.io)
- [x] gRPC proto generator → otomatik server+client proto üretimi, kardeş-spec zengin çözümleme, `identity/User` özel durumu (bkz. docs/ARCH.md §5.1)
- [x] Identity Service referans implementasyonu → `services/BaseForge.Identity` (OpenIddict + ASP.NET Identity, config-driven, sosyal sağlayıcılar, gRPC User erişimi)
- [x] Entity alan seviyesi kısıtlar: `nullable` / `maxLength` / `default` (PropSpec) — YAML'da hem düz string hem zengin obje formu, EF Core `[MaxLength]`, Command validasyonu, gRPC null-safe proto dönüşümü
- [x] Docker host portları seçilebilir (`DockerPortsSpec`: REST/gRPC/Postgres) — Designer'da üç opsiyonel alan, aynı makinede başka projelerle port çakışmasını önlemek için (varsayılan boşsa eski sabit portlar korunur)
- [x] Designer'da "Çalıştır"/"Durdur" — üretilen servisi `docker compose up --build -d --wait` ile tam stack olarak ayağa kaldırır, `docker compose down` ile durdurur (process-tree yönetimi yerine Docker'ın kendi lifecycle'ı kullanılır — orphan process riski yok)
- [x] Identity secret'ları (OAuth Client Secret, seed admin şifresi) artık `.env`'de (gitignore'lu, `DotNetEnv` ile yerel `dotnet run`'da okunur, Docker'da `env_file`) — `appsettings.json`'da boş kalır
- [x] `baseforge update <servis>` CLI komutu — `new`'in aksine, diskteki `<servis>/spec.yaml` (ve varsa `identity/auth.yaml`) yüklenmiş olarak Designer'ı açar; var olan servise entity eklemek/düzenlemek için YAML'a elle dokunmaya gerek kalmaz.
  **Bilinen kısıt:** identity servisinin adı `auth.yaml`'da "identity" dışında bir şeye değiştirilmişse `update` onu bulamıyor (klasör adı hardcoded) — düzeltilmedi.
- [x] RabbitMQ / asenkron event iletişimi — `IIntegrationEvent`/`IEventBus` (Core/Infrastructure, MediatR ile yerel dağıtım), `EnableRabbitMq` (API), CodeGen `publishes`/`subscribes` (entity/servis bazlı, kardeş-spec zengin çözümleme). Detay: docs/ARCH.md §5.2. `via: event` kalıcı olarak no-op — read-model senkronizasyonu için ayrı bir gelecek özellik olarak rezerve.
  **Bilinen kısıtlar:** CLI-only v1 (Designer'da form karşılığı yok), DLQ/retry politikası yok (nack + requeue:false), kanal havuzu yok.
- [ ] docs/ARCH.md — `update` komutu ve Docker port/Çalıştır-Durdur/.env özellikleri henüz orada belgelenmedi (CLAUDE.md kuralı: yeni özellik öncesi ARCH.md güncellenir)
