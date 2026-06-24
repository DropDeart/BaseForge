# BaseForge — Mimari Kararlar (ARCH)

Bu doküman, BaseForge'un mimari kararlarını ve gerekçelerini ayrıntılı olarak tutar. Yeni bir özellik eklenmeden önce bu dosya güncellenir.

## 1. Genel Yaklaşım: Opinionated Library

BaseForge bir **framework değil, opinionated library**'dir. Hedef; library'nin esnekliğini korurken framework kolaylığını sunmaktır. Pratikte bu, tek satırlık DI entegrasyonu anlamına gelir:

```csharp
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL(connectionString);
    options.EnableCQRS();
    options.EnableAuditLog();
});
```

**Gerekçe:** Her mikroservis aynı altyapı kararlarını (CQRS, repository, audit, exception handling) tekrar kurmak zorunda kalmamalı; ancak istediğinde davranışı override edebilmeli.

## 2. Katmanlama (Clean Architecture)

Üç paket, bağımlılık yönü dışarıdan içeriye olacak şekilde ayrılır:

```
BaseForge.API  ──►  BaseForge.Infrastructure  ──►  BaseForge.Core
```

| Katman | Bağımlılık | İçerik |
| --- | --- | --- |
| `Core` | Yok (yalnızca MediatR sözleşmeleri) | Entity base'leri, interface'ler, CQRS sözleşmeleri, exception'lar |
| `Infrastructure` | `Core` | GenericRepository, ADO.NET query builder, DbContext base, DI extension'ları |
| `API` | `Core` + `Infrastructure` | BaseController, middleware, AddBaseForge() |

**Kurallar:**
- `Core` hiçbir somut altyapıya (DB, HTTP, MediatR implementasyonu) bağlı olamaz. Yalnızca MediatR'ın marker interface'lerini (`IRequest` vb.) referans alır — tam MediatR paketi Infrastructure/API'de register edilir.
- `Infrastructure` asla `API`'ye bağlı olamaz.
- `API` her iki katmana da bağlı olabilir.

**Gerekçe:** Test edilebilirlik ve paket bağımsızlığı. `Core` bağımlılıksız olduğu için sadece sözleşmeleri tüketmek isteyen servisler yalnızca onu çekebilir.

## 3. CQRS — MediatR Üzerine

- CQRS sıfırdan yazılmaz; **MediatR** üzerine inşa edilir.
- `Core` içinde `ICommand`, `IQuery`, `IHandler` base sözleşmeleri tanımlanır; bunlar MediatR'ın `IRequest`/`IRequestHandler` tiplerini sarmalar.
- Her servis bu sözleşmeleri extend eder.
- **Karar:** MediatR dışında başka bir CQRS/mediator kütüphanesi eklenmez.

## 4. Veri Erişimi — ADO.NET (EF Core Yok)

- **ORM kullanılmaz.** Entity Framework Core projeye eklenmez.
- Ham SQL + bir **base query builder** ile çalışılır; query builder lib içinde bulunur.
- `GenericRepository`, `IRepository<T>` sözleşmesini ADO.NET ile implemente eder.
- **Gerekçe:** Mikroservislerde SQL üzerinde tam kontrol, öngörülebilir performans ve ORM sürpriz davranışlarından kaçınma.

### Audit & Soft Delete

- `BaseEntity` üzerinde `CreatedAt`, `UpdatedAt`, `CreatedBy` alanları (audit log) tanımlıdır.
- Soft delete `ISoftDelete` üzerinden işaretlenir; silme işlemleri fiziksel değil mantıksaldır.

## 5. Mikroservis İletişimi

- **Database per Service:** Her mikroservis kendi PostgreSQL veritabanına sahiptir; servisler birbirinin DB'sine doğrudan erişmez.
- **Senkron:** gRPC.
- **Asenkron:** RabbitMQ (fire-and-forget, event-driven).

## 6. Kimlik Doğrulama

- Merkezi tek bir **Identity Service** vardır (JWT / OAuth2).
- Her servis JWT token'ı kendi middleware'inde **lokal olarak** validate eder; her istekte merkezi DB'ye çağrı yapılmaz.

## 7. Containerization

- Her servis için ayrı `Dockerfile`.
- Tüm servisler tek bir `docker-compose.yml` ile ayağa kalkar.
- Konfigürasyon `.env` dosyasından okunur; production'da `.env.production` kullanılır.

## 8. Dağıtım

- Üç paket public NuGet (nuget.org) olarak yayınlanır: `BaseForge.Core`, `BaseForge.Infrastructure`, `BaseForge.API`.

## Karar Günlüğü

| Tarih | Karar | Durum |
| --- | --- | --- |
| 2026-06-24 | Proje iskeleti (.NET 10, 3 src + 2 test projesi, .slnx) kuruldu | ✅ |
| 2026-06-24 | Opinionated Library + Clean Architecture + CQRS(MediatR) + ADO.NET kararları PDF spesifikasyonundan alındı | ✅ |
