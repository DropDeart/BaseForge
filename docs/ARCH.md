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
- **Asenkron:** RabbitMQ (fire-and-forget, event-driven) — henüz implemente edilmedi (bkz. Karar Günlüğü, backlog).

### 5.1. gRPC — Otomatik Proto Üretimi

Her `via: grpc` dış referans (`ExternalRefSpec`), `baseforge new-service` sırasında **gerçek** gRPC client+server kodu üretir (önceden yalnızca boş bir `Id`-only interface iskeleti üretiliyordu).

- **Server-side (otomatik, opt-out yok):** Üretilen her servis, kendi TÜM entity'lerini bir gRPC servisi olarak expose eder (`Protos/{entity}.proto` + `Grpc/{Entity}GrpcService.cs`). Server implementasyonu mevcut CQRS `Get{Entity}ByIdQuery`'yi MediatR üzerinden çağırır — veri erişimi tekrar yazılmaz. Sıralama bağımlılığından kaçınmak için bu davranış koşulsuzdur (sağlayıcı servis, tüketicisi üretilmeden önce de tüm entity'lerini expose eder).
- **Client-side çözümleme:** `ExternalRefSpec.Target` (`"servis/Entity"`) dışında CodeGen hedefin alan şeklini bilmez. Çözüm: hedefin servis segmentiyle, **spec dosyasının bulunduğu klasörde** kardeş `{servis}.yaml` aranır (`SpecLoader` ile). Bulunursa hedef entity'nin gerçek `Props`'u okunup zengin (gerçek alanlı) bir proto+client üretilir; bulunamazsa `Console.Error`'a uyarı yazılıp minimal (yalnızca Id) bir fallback stub'a sessizce düşülür — asla hata fırlatılmaz.
- **`identity/User` özel durumu:** Identity kendi `ServiceSpec`'ini kullanmadığından (ayrı `AuthSpec`) kardeş-spec okuma çalışmaz. `services/BaseForge.Identity/Protos/user.proto` gerçek, statik bir dosyadır; hem `IdentityGenerator`'ın kopyalama döngüsü hem `CodeGenerator`'ın `target: identity/User` özel durumu **aynı** embedded kaynağı okur (tek fiziksel kaynak, drift riski yok). Sabit alanlar (`ApplicationUser`): Id, UserName, Email, FullName.
- **Kestrel — iki port zorunlu:** ASP.NET Core Kestrel, TLS olmadan (h2c) aynı portta HTTP/1.1 ve HTTP/2'yi otomatik ayırt edemez (canlı testte doğrulandı: `EndpointDefaults: Http1AndHttp2` tek başına REST'i çalıştırır ama gRPC'yi sessizce HTTP/1.1'e düşürür). Bu yüzden her üretilen servis **iki ayrı endpoint** tanımlar: `Http` (8080, REST/Scalar) ve `Grpc` (8081, h2c). Client tarafında `AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true)` ile TLS'siz HTTP/2 istekleri etkinleştirilir.
- **Kısıtlar:** İki farklı kaynak servisten aynı adlı entity'ye dış referans çakışır (ikincisi atlanır). gRPC çağrılarında JWT propagasyonu yok (güven, docker ağı sınırına dayanıyor). decimal/datetime/date/guid proto'da `string` taşınır (native karşılık yok).
- **Referans senaryo:** `samples/products.yaml` + `samples/warehouse.yaml` + `samples/orders.yaml` → `services/BaseForge.Products`, `services/BaseForge.Warehouse`, `services/BaseForge.Orders` (committed, gerçek/derlenebilir). Orders, Products'a (kardeş spec) ve Identity'ye (`identity/User`) gRPC ile bağlanır; Warehouse da Products'a bağlanır.

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
| 2026-07-07 | gRPC senkron iletişim gerçek hale getirildi: otomatik proto üretimi (server+client), kardeş-spec zengin çözümleme, `identity/User` özel durumu, Kestrel iki-port (h2c) düzeltmesi. RabbitMQ hâlâ backlog'da. | ✅ |
