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

## 4. Veri Erişimi — EF Core 10 (ORM) + Dapper (ham SQL)

> **Karar değişikliği (2026-06-24):** PDF spesifikasyonundaki "ORM kullanılmaz, ADO.NET tercih edilir" maddesi proje sahibi tarafından revize edildi. Gerekçe: EF Core'un LINQ + change tracking üretkenliği ile ham SQL esnekliği aynı anda elde edilebiliyor; saf ADO.NET'in boilerplate maliyeti üretkenliği düşürüyor.

Hibrit yaklaşım benimsenir:

- **EF Core 10** birincil ORM'dir. Sorumlulukları: yazma işlemleri (insert/update/delete), change tracking (identity map / first-level cache), migration'lar ve CRUD'un büyük kısmı LINQ ile.
- **Dapper** (micro-ORM) ağır okuma ve karmaşık join sorgularında ham SQL için kullanılır; sonuçları DTO'lara hızlıca map eder. Dapper bir sorgu üreticisi/ORM değildir — SQL elle yazılır, yalnızca mapping sağlar.
- Dapper, EF Core `DbContext`'inin `DbConnection`'ı üzerinden çalıştırılır (`Database.GetDbConnection()`), böylece aynı bağlantı ve transaction paylaşılır.
- `GenericRepository`, `IRepository<TEntity, TKey>` sözleşmesini EF Core ile implemente eder. Karmaşık okuma senaryoları için Dapper tabanlı bir sorgu yardımcısı (`ISqlQuery` benzeri) sunulur.

**Rol dağılımı:**

| İhtiyaç | Araç |
| --- | --- |
| CRUD, ilişki yükleme, LINQ | EF Core |
| Change tracking, migration | EF Core |
| Karmaşık join / projeksiyon / rapor sorgusu | Dapper (ham SQL) veya EF `FromSql` |
| Toplu set-based update/delete | EF `ExecuteUpdate` / `ExecuteDelete` |
| Tam kontrol / saf bağlantı | `DbContext.Database.GetDbConnection()` |

PostgreSQL sağlayıcısı: `Npgsql.EntityFrameworkCore.PostgreSQL`.

### Audit & Soft Delete

- `BaseEntity` üzerinde `CreatedAt`, `UpdatedAt`, `CreatedBy` (audit) ve `IsDeleted`/`DeletedAt` (soft delete) alanları tanımlıdır.
- Audit alanları EF Core `SaveChanges` override'ında otomatik doldurulur.
- Soft delete EF Core **global query filter** ile uygulanır; silinmiş kayıtlar varsayılan sorgularda görünmez.
- **Not:** Dapper EF'in query filter'ını bilmez; Dapper ile yazılan ham SQL'de soft delete koşulu (`WHERE is_deleted = false`) elle eklenmelidir.

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
| 2026-06-24 | Opinionated Library + Clean Architecture + CQRS(MediatR) kararları PDF spesifikasyonundan alındı | ✅ |
| 2026-06-24 | Veri erişimi PDF'teki "ADO.NET, ORM yok" yerine **EF Core 10 (ORM) + Dapper (ham SQL)** olarak revize edildi | ✅ |
| 2026-06-24 | CQRS için MediatR **12.5.0** (son ücretsiz/Apache-2.0 sürüm; v13+ ticari) sabitlendi | ✅ |
| 2026-06-24 | nuget.org **Trusted Publishing** (OIDC, `.github/workflows/publish.yml`) kuruldu; klasik API key yerine | ✅ |
| 2026-06-24 | Backlog "ER Diagram": **BaseForge.Tools** paketi + `DbmlGenerator` (EF Core model → DBML) eklendi; kaynak=EF Core model, çıktı=DBML | ✅ |
