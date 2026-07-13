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
- **Asenkron:** RabbitMQ (fire-and-forget, event-driven) — bkz. §5.2.

### 5.1. gRPC — Otomatik Proto Üretimi

Her `via: grpc` dış referans (`ExternalRefSpec`), `baseforge new-service` sırasında **gerçek** gRPC client+server kodu üretir (önceden yalnızca boş bir `Id`-only interface iskeleti üretiliyordu).

- **Server-side (otomatik, opt-out yok):** Üretilen her servis, kendi TÜM entity'lerini bir gRPC servisi olarak expose eder (`Protos/{entity}.proto` + `Grpc/{Entity}GrpcService.cs`). Server implementasyonu mevcut CQRS `Get{Entity}ByIdQuery`'yi MediatR üzerinden çağırır — veri erişimi tekrar yazılmaz. Sıralama bağımlılığından kaçınmak için bu davranış koşulsuzdur (sağlayıcı servis, tüketicisi üretilmeden önce de tüm entity'lerini expose eder).
- **Client-side çözümleme:** `ExternalRefSpec.Target` (`"servis/Entity"`) dışında CodeGen hedefin alan şeklini bilmez. Çözüm: hedefin servis segmentiyle, **spec dosyasının bulunduğu klasörde** kardeş `{servis}.yaml` aranır (`SpecLoader` ile). Bulunursa hedef entity'nin gerçek `Props`'u okunup zengin (gerçek alanlı) bir proto+client üretilir; bulunamazsa `Console.Error`'a uyarı yazılıp minimal (yalnızca Id) bir fallback stub'a sessizce düşülür — asla hata fırlatılmaz.
- **`identity/User` özel durumu:** Identity kendi `ServiceSpec`'ini kullanmadığından (ayrı `AuthSpec`) kardeş-spec okuma çalışmaz. `services/BaseForge.Identity/Protos/user.proto` gerçek, statik bir dosyadır; hem `IdentityGenerator`'ın kopyalama döngüsü hem `CodeGenerator`'ın `target: identity/User` özel durumu **aynı** embedded kaynağı okur (tek fiziksel kaynak, drift riski yok). Sabit alanlar (`ApplicationUser`): Id, UserName, Email, FullName.
- **Kestrel — iki port zorunlu:** ASP.NET Core Kestrel, TLS olmadan (h2c) aynı portta HTTP/1.1 ve HTTP/2'yi otomatik ayırt edemez (canlı testte doğrulandı: `EndpointDefaults: Http1AndHttp2` tek başına REST'i çalıştırır ama gRPC'yi sessizce HTTP/1.1'e düşürür). Bu yüzden her üretilen servis **iki ayrı endpoint** tanımlar: `Http` (8080, REST/Scalar) ve `Grpc` (8081, h2c). Client tarafında `AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true)` ile TLS'siz HTTP/2 istekleri etkinleştirilir.
- **Kısıtlar:** İki farklı kaynak servisten aynı adlı entity'ye dış referans çakışır (ikincisi atlanır). gRPC çağrılarında JWT propagasyonu yok (güven, docker ağı sınırına dayanıyor). decimal/datetime/date/guid proto'da `string` taşınır (native karşılık yok).
- **Cross-service host adresi:** Her üretilen servis kendi izole `docker-compose.yml`'unda (kendi Docker network'ünde) çalışır; sağlayıcı servisin bare adı (örn. `"identity"`) bu yüzden network DNS ile çözülemez. Rich gRPC client'ların `ProviderHost`'u (ve RabbitMq broker adresi, bkz. §5.2) bu yüzden sabit olarak `host.docker.internal` üretilir — Docker Desktop'ın container'dan host'a yönlendiren adresi.
- **Referans senaryo:** `samples/products.yaml` + `samples/warehouse.yaml` + `samples/orders.yaml` → `services/BaseForge.Products`, `services/BaseForge.Warehouse`, `services/BaseForge.Orders` (committed, gerçek/derlenebilir). Orders, Products'a (kardeş spec) ve Identity'ye (`identity/User`) gRPC ile bağlanır; Warehouse da Products'a bağlanır.

### 5.2. RabbitMQ — Otomatik Event Pub/Sub

`ExternalRefSpec.Via = "event"` **implemente edilmedi** ve gelecekte planlanan farklı bir özellik (read-model senkronizasyonu — üretici servis CRUD olaylarını yayınlar, tüketici yerel bir gölge tabloyla günceller) için rezerve edilmiştir; bugün no-op'tur. Asenkron pub/sub, ayrı ve daha basit iki YAML alanıyla çalışır: entity bazlı `publishes` ve servis bazlı `subscribes`.

**Kütüphane tarafı (`BaseForge.Core`/`Infrastructure`/`API`):**

- `IIntegrationEvent : INotification` (Core) — MediatR'ın bildirim sözleşmesini genişletir. Yayıncı `IEventBus.PublishAsync<TEvent>()` (Core sözleşmesi, Infrastructure'da `RabbitMqEventBus` implementasyonu) ile RabbitMQ'ya yazar; tüketici tarafında `RabbitMqConsumerHostedService` (Infrastructure, `BackgroundService`) kuyruktan gelen mesajı ilgili CLR tipine deserialize edip yerel olarak `IPublisher.Publish()` (MediatR) ile dağıtır. Geliştirici sıradan bir `INotificationHandler<TEvent>` yazar, RabbitMQ'yu hiç görmez — **yeni bir CQRS kütüphanesi eklenmemiş olur** (§3 kararıyla tutarlı).
- `builder.Services.AddBaseForge(options => options.EnableRabbitMq(mq => { ... }))` — `EnableJwt` ile birebir aynı fluent desen. Abonelik varsa (`mq.Subscribe<TEvent>(eventType, queueName)`) tüketici hosted service'i otomatik eklenir; yoksa yalnızca `IEventBus` (yayıncı) register edilir.
- Broker: tek bir topic exchange (`baseforge.events`). Routing key, olayın `EventType`'ından türetilir (`servis/EntityKind` → `servis.EntityKind`).
- Paket: `RabbitMQ.Client` (tam async API) + `Microsoft.Extensions.Hosting.Abstractions` — `BaseForge.Infrastructure`'a eklendi (ASP.NET Core'a bağımlılık getirmez, host-framework-agnostic kuralı korunur).

**CodeGen tarafı:**

- `EntitySpec.Publishes: List<string>` (`created`/`updated`/`deleted`) — ilgili Create/Update/Delete komutu `SaveChangesAsync` sonrası `{Entity}{Kind}Event`'i yayınlar (`Features/{Entity}s/{Entity}Events.cs`).
- `ServiceSpec.Subscribes: List<SubscribeSpec>` (`event: "servis/EntityKind"`, `handler: ClassName`) — hedef entity kardeş spec'te (veya **kendi spec'inde**, kendi kendine abonelik için özel durum gerekmez) bulunup `publishes` listesinde ilgili Kind varsa gerçek alanlarla ("rich"), bulunamazsa yalnızca `Id` ile ("minimal") bir gölge event/data class'ı + `INotificationHandler<T>` stub'ı üretilir (`Integration/{Handler}.cs`). Kardeş-spec bulma mantığı, gRPC dış referans çözümlemesiyle (§5.1) aynı `LoadSiblingSpec` helper'ını paylaşır.
- CLI/YAML-only v1: Designer web UI'da form karşılığı yok (`auth:` bloğunun bugünkü durumuyla aynı — CLI soru sormuyor, YAML elle yazılır). `via: event`'in aksine bu iki alan `/meta` endpoint'inde veya `EntityEditor.tsx`'te temsil edilmez; fast-follow olarak planlanabilir.
- Docker topolojisi: kökteki `docker-compose.yml`'daki (kullanılmadan duran, `.env.example`'da `RABBITMQ_*` değişkenleriyle zaten scaffold edilmiş) `rabbitmq` servisi tek paylaşılan broker olur. Üretilen servisler kendi izole compose'larında RabbitMQ container'ı açmaz; `appsettings.json`'daki `RabbitMq:Host` varsayılanı `host.docker.internal`'dır (bkz. §5.1 cross-service host notu).

**v1 sınırlamaları (bilinçli, dokümante edilen basitlik):**

- DLQ/retry politikası yok — tüketici hata alırsa mesaj `nack(requeue: false)` ile atılır, loglanır. "Fire and forget" felsefesiyle tutarlı (§5), ileride ayrı bir iyileştirme konusu.
- Kanal havuzu yok — `RabbitMqConnectionManager` tek bağlantıyı paylaşır ama her yayın/tüketim çağrısında yeni kanal açar.
- gRPC çağrılarında olduğu gibi, mesajlarda JWT/kimlik propagasyonu yok.

### 5.3. JSON/JSONB Alan Tipi

Spec tip sistemine `json` eklendi: C# tarafında `string` (serileştirilmiş JSON metni), veritabanı tarafında Postgres `jsonb` (`[Column(TypeName = "jsonb")]`, EF Core native fluent API yerine — bu üretici mevcut `MaxLength` deseninin aynısı, DataAnnotation attribute olarak entity sınıfına gömülür).

- **Karar:** `TypeMap.cs`'e `["json"] = ("string", "jsonb")` eklendi; `[Column(TypeName = "jsonb")]` **yalnızca entity sınıfında** üretilir, Create/Update komut DTO'larında değil (Column attribute'u yalnızca EF-mapped tiplerde anlamlıdır; DTO'lar mapped değildir — `MaxLength`'in DTO'larda da anlamlı olmasının [ASP.NET model validation] aksine).
- **Gerekçe:** Esnek/şemasız payload alanları (örn. audit/trace event'lerinin olay-spesifik verisi) için ayrı bir tablo/JOIN yerine tek bir sütun yeterli; Postgres'in native `jsonb` desteği sorgu/index imkânı da sağlıyor (ileride `EF.Functions.JsonContains` vb. ile).
- **Kısıtlar:** Yalnızca Postgres `jsonb`'e eşlenir (SQL Server gibi başka bir provider hedeflenirse bu tip yeniden değerlendirilmeli). `maxLength` json'da anlamsız olduğu için `SpecValidator`'ın string/text-only kontrolü sayesinde otomatik reddedilir (ek kod gerekmedi). gRPC proto tarafında `string` olarak taşınır (decimal/datetime/guid ile aynı "native karşılığı yok" kısıtı).

### 5.4. Append-Only Entity'ler

`EntitySpec.AppendOnly: bool` — `true` ise Update/Delete komutu, handler'ı ve controller action'ı **hiç üretilmez**; yalnızca Create/GetById/List kalır.

- **Karar:** Servis-geneli değil, **entity-bazlı** bir bayrak (her serviste hem mutable hem append-only entity'ler bir arada olabilir — örn. `Product` mutable, `TraceEvent` append-only, aynı serviste).
- **Gerekçe:** GMP/Annex 11 ve 21 CFR Part 11 gibi regülatif uyum gerektiren audit/trace kayıtlarının API üzerinden asla değiştirilememesi/silinememesi gerekiyor. Bunu yalnızca "istemci Update/Delete çağırmasın" (sözleşme/dokümantasyon) yerine, üretici seviyede **fiziksel olarak var olmayan bir endpoint** ile garanti altına almak daha güvenli.
- **Kısıtlar:** `AppendOnly=true` iken `publishes` listesinde `created` dışında bir değer (`updated`/`deleted`) veya `anonymousActions` içinde `update`/`delete` olamaz — `SpecValidator` bunu derleme/üretim öncesi hata olarak yakalar (sessizce yok saymak yerine "fail loud").

### 5.5. Multi-Tenancy

`ServiceSpec.MultiTenant: bool` — `true` ise servisin **tüm** entity'leri `ITenantEntity` (`Guid TenantId`, `BaseForge.Core.Entities`) implemente eder; `options.EnableMultiTenancy()` çağrılır.

- **Karar:** Servis-geneli, entity-bazlı **değil** — gerçek izolasyon her tabloyu kapsamalı, entity-bazlı seçim ayak tuzağı olurdu (bir tabloyu unutmak = tenant sızıntısı). `BaseEntity<TKey>` değiştirilmedi (mevcut tüm servisleri etkileyen breaking change olurdu) — yeni `ITenantEntity` marker interface'i, `ISoftDelete` ile aynı desende, yalnızca CodeGen tarafından `MultiTenant: true` olan servislerin entity'lerine eklenir; `TenantId` kullanıcı tarafından YAML'da tanımlanmaz, otomatik enjekte edilir.
- **Mekanizma:** `ICurrentTenant` (Core, `ICurrentUser` ile aynı şekil) + `CurrentTenant` (API, JWT `tenant_id` claim'i okur) `EnableMultiTenancy()` ile DI'a kaydedilir. `BaseForgeDbContext`:
  - `ApplyAuditAndSoftDelete`, `Added` durumundaki `ITenantEntity`'lere `TenantId`'yi damgalar; `ICurrentTenant.TenantId` null ise `InvalidOperationException` fırlatır (sessiz NULL satır yerine "fail loud").
  - `OnModelCreating`, EF Core'un her entity tipi için yalnızca **tek** query filter'a izin vermesi nedeniyle, `ISoftDelete` ve `ITenantEntity` filtrelerini `Expression.AndAlso` ile tek bir birleşik filtrede kurar (4 durum: ne biri ne diğeri / yalnız soft-delete / yalnız tenant / ikisi birden).
  - Üretilen DbContext'in constructor'ı `ICurrentUser?`/`ICurrentTenant?`'ı `BaseForgeDbContext`'e forward eder (`(options, currentUser = null, currentTenant = null) : base(...)`) — bu forward olmadan tenant damgalama hiç çalışmaz.
- **Bilinen bir reflection tuzağı (üretim sırasında yakalandı, birim testle doğrulandı):** Query filter ifadesinde o anki context'e (`this`) referans verirken `Expression.Constant(this, typeof(BaseForgeDbContext))` ile **açıkça temel sınıf olarak tiplemek gerekir** — `Expression.Constant(this)` runtime tipini (her zaman türetilmiş, CodeGen'in ürettiği DbContext sınıfı) kullanırsa, `private` `CurrentTenantId` property'si (yalnızca `BaseForgeDbContext`'te tanımlı, private üyeler `FlattenHierarchy` ile türetilmiş tipe miras alınmaz) reflection'da bulunamaz ve her sorguda `ArgumentException` fırlar.
- **Kısıtlar:** Tenant claim adı sabit: `tenant_id`. Multi-tenant bir entity'ye tenant context'siz (örn. arka plan servisinden `ICurrentTenant` olmadan) kayıt eklemek exception fırlatır — bu bilinçli bir tasarım (sessiz cross-tenant sızıntısı yerine).

## 6. Kimlik Doğrulama

- Merkezi tek bir **Identity Service** vardır (JWT / OAuth2).
- Her servis JWT token'ı kendi middleware'inde **lokal olarak** validate eder; her istekte merkezi DB'ye çağrı yapılmaz.

## 7. Containerization

- Her servis için ayrı `Dockerfile`.
- Tüm servisler tek bir `docker-compose.yml` ile ayağa kalkar. **Not:** CodeGen bugün her servis için ayrı, izole bir `docker-compose.yml` üretir (kendi Postgres'i ile); kökteki paylaşılan `docker-compose.yml` yalnızca tek-örnek altyapı için kullanılır (Postgres + RabbitMQ, bkz. §5.2) — üretilen servisler bu paylaşılan broker'a `host.docker.internal` üzerinden bağlanır.
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
| 2026-07-10 | RabbitMQ asenkron event pub/sub eklendi: `IIntegrationEvent`/`IEventBus` (Core/Infrastructure, MediatR'ı yerel dağıtım için yeniden kullanır), `EnableRabbitMq` (API, `EnableJwt` deseniyle), CodeGen `publishes`/`subscribes` (bkz. §5.2). `via: event` kalıcı olarak no-op — ayrı bir gelecek özellik için rezerve. Aynı geçişte rich gRPC client'ların `ProviderHost`'u `host.docker.internal`'a düzeltildi (izole compose ağları arasında hiç çalışmıyordu). | ✅ |
| 2026-07-13 | `json`/`jsonb` prop tipi eklendi (bkz. §5.3): `TypeMap` + `[Column(TypeName = "jsonb")]` yalnızca entity sınıfında (DTO'larda değil). | ✅ |
| 2026-07-13 | Append-only entity desteği eklendi (bkz. §5.4): `EntitySpec.AppendOnly` — Update/Delete komut/handler/controller action'ı hiç üretilmez; GMP/21 CFR Part 11 audit/trace senaryosu için. | ✅ |
| 2026-07-13 | Multi-tenancy eklendi (bkz. §5.5): `ServiceSpec.MultiTenant`, `ITenantEntity`/`ICurrentTenant`/`EnableMultiTenancy()`, `BaseForgeDbContext`'te `Expression.AndAlso` ile birleşik soft-delete+tenant query filter. Üretim sırasında iki gerçek hata bulunup düzeltildi: (1) üretilen DbContext constructor'ı `ICurrentUser`/`ICurrentTenant`'ı hiç forward etmiyordu, (2) query filter'daki `Expression.Constant(this)` runtime tipini kullandığından `private CurrentTenantId` property'si türetilmiş tipte reflection ile bulunamıyordu (`Expression.Constant(this, typeof(BaseForgeDbContext))` ile düzeltildi, birim testle doğrulandı). | ✅ |
