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

- `IIntegrationEvent : INotification` (Core) — MediatR'ın bildirim sözleşmesini genişletir. Yayıncı `IEventBus.PublishAsync<TEvent>()` çağırır; tüketici tarafında `RabbitMqConsumerHostedService` (Infrastructure, `BackgroundService`) kuyruktan gelen mesajı ilgili CLR tipine deserialize edip yerel olarak `IPublisher.Publish()` (MediatR) ile dağıtır. Geliştirici sıradan bir `INotificationHandler<TEvent>` yazar, RabbitMQ'yu hiç görmez — **yeni bir CQRS kütüphanesi eklenmemiş olur** (§3 kararıyla tutarlı).
- `builder.Services.AddBaseForge(options => options.EnableRabbitMq(mq => { ... }))` — `EnableJwt` ile birebir aynı fluent desen. Abonelik varsa (`mq.Subscribe<TEvent>(eventType, queueName)`) tüketici hosted service'i otomatik eklenir; publish varsa (her zaman) outbox relay'i (aşağıda) otomatik eklenir.
- Broker: tek bir topic exchange (`baseforge.events`). Routing key, olayın `EventType`'ından türetilir (`servis/EntityKind` → `servis.EntityKind`).
- Paket: `RabbitMQ.Client` (tam async API) + `Microsoft.Extensions.Hosting.Abstractions` — `BaseForge.Infrastructure`'a eklendi (ASP.NET Core'a bağımlılık getirmez, host-framework-agnostic kuralı korunur).

**Transactional Outbox (2026-07-16 — dual-write riskini çözer):**

`IEventBus.PublishAsync<TEvent>()` artık RabbitMQ'ya **doğrudan yazmıyor**. Önceki tasarımda handler önce `_unitOfWork.SaveChangesAsync()` ile DB'ye commit ediyor, sonra `_eventBus.PublishAsync()` ile broker'a yazıyordu — bu ikisi arasında atomiklik yoktu: DB commit başarılı olur ama publish (network/broker hatası) başarısız olursa, değişiklik kalıcı olur ama event hiç yayınlanmazdı.

- `OutboxEventBus : IEventBus` (Infrastructure, **Scoped**) — `PublishAsync`, olayı bir `OutboxMessage` (Core, plain POCO — `IAuditEntity`/`ISoftDelete`/`ITenantEntity` implemente etmez, global query filter'lara girmez) satırı olarak çağıranın o anki `BaseForgeDbContext`'inin change tracker'ına ekler. DB'ye yazmaz, I/O yapmaz.
- CodeGen şablonlarında (`Templates.cs`) `_eventBus.PublishAsync(...)` çağrısı artık `_unitOfWork.SaveChangesAsync(...)`'den **önce** yapılır — böylece outbox satırı, tetikleyen business entity değişikliğiyle **tek bir `SaveChangesAsync` çağrısında, aynı transaction'da** atomik yazılır.
- `OutboxPublisherHostedService` (Infrastructure, `BackgroundService`) — `BaseForgeDbContext.OutboxMessages`'ı periyodik tarar (`RabbitMqOptions.OutboxPollingInterval`, varsayılan 2s), işlenmemiş (`ProcessedAt IS NULL`) satırları `IRabbitMqPublisher.PublishRawAsync()` (eski `RabbitMqEventBus`'ın yeniden adlandırılmış hâli — artık jenerik değil, zaten hazır zarf JSON'unu olduğu gibi yazar) ile gerçek broker'a gönderir, başarılıysa `ProcessedAt` işaretler.
- Çoklu-instance güvenliği: satır seçimi `SELECT ... FOR UPDATE SKIP LOCKED` ile yapılır — iki instance aynı satırı asla aynı anda işlemez, ekstra lease/claim kolonu gerekmez, process çökerse Postgres kilidi otomatik serbest kalır.
- `IEventBus` kaydı Singleton'dan **Scoped**'a değişti (çağıranla aynı scope'un `BaseForgeDbContext`'ine yazması gerektiği için) — `OutboxPublisherHostedService` her zaman kayıtlıdır (abonelik sayısından bağımsız, publish varsa aktif).
- **Max-retry + dead marking (2026-07-16):** `RabbitMqOptions.OutboxMaxRetries` (varsayılan 10) aşılınca satır `OutboxMessage.IsDead = true` olarak işaretlenir, relay'in `WHERE` koşulundan çıkar (`AND "IsDead" = false`) — sonsuza kadar denenmez, ama silinmez (manuel inceleme için tabloda kalır).
- **Cleanup/retention job (2026-07-16):** Her tarama tick'inin sonunda (yeni mesaj olsun olmasın), işlenmiş (`ProcessedAt` dolu) ve `RabbitMqOptions.OutboxRetention` (varsayılan 7 gün) süresinden eski satırlar `ExecuteDeleteAsync` ile toplu silinir. `IsDead` satırlar bu temizlikten muaftır.

**CodeGen tarafı:**

- `EntitySpec.Publishes: List<string>` (`created`/`updated`/`deleted`) — ilgili Create/Update/Delete komutu (artık `SaveChangesAsync`'den **önce**, outbox'a) `{Entity}{Kind}Event`'i yayınlar (`Features/{Entity}s/{Entity}Events.cs`).
- `ServiceSpec.Subscribes: List<SubscribeSpec>` (`event: "servis/EntityKind"`, `handler: ClassName`) — hedef entity kardeş spec'te (veya **kendi spec'inde**, kendi kendine abonelik için özel durum gerekmez) bulunup `publishes` listesinde ilgili Kind varsa gerçek alanlarla ("rich"), bulunamazsa yalnızca `Id` ile ("minimal") bir gölge event/data class'ı + `INotificationHandler<T>` stub'ı üretilir (`Integration/{Handler}.cs`). Kardeş-spec bulma mantığı, gRPC dış referans çözümlemesiyle (§5.1) aynı `LoadSiblingSpec` helper'ını paylaşır.
- CLI/YAML-only v1: `publishes`/`subscribes`'ın kendisi (hangi entity hangi olayı yayınlar/dinler) hâlâ Designer web UI'da form karşılığı yok (`auth:` bloğunun bugünkü durumuyla aynı — CLI soru sormuyor, YAML elle yazılır). `via: event`'in aksine bu iki alan `/meta` endpoint'inde veya `EntityEditor.tsx`'te temsil edilmez; fast-follow olarak planlanabilir.
- **RabbitMQ ince ayarları için Designer formu eklendi (2026-07-16):** `ServiceSpec.RabbitMqTuning` (`OutboxMaxRetries`/`OutboxRetentionDays`, opsiyonel) — `DockerPortsSpec` ile birebir aynı desen (nullable nested object, doğrudan controlled input). Designer'da servis formunun altında (DockerPorts'un hemen altında), `publishes`/`subscribes` durumundan bağımsız olarak **her zaman** görünür — `DockerPorts` da aynı şekilde koşulsuz gösteriliyor, ve Designer'ın `ServiceSpec`/`EntitySpec` TS modelinde `publishes`/`subscribes` hiç temsil edilmediği için istemci tarafında "bu servis RabbitMQ kullanıyor mu" güvenilir şekilde hesaplanamıyordu (plan bunu koşullu göstermeyi öngörmüştü, uygulama sırasında bu kısıt fark edilip düzeltildi). Doldurulursa üretilen `Program.cs`'teki `options.EnableRabbitMq(mq => ...)` bloğuna `mq.OutboxMaxRetries`/`mq.OutboxRetention` override satırları eklenir.
- Docker topolojisi: kökteki `docker-compose.yml`'daki (kullanılmadan duran, `.env.example`'da `RABBITMQ_*` değişkenleriyle zaten scaffold edilmiş) `rabbitmq` servisi tek paylaşılan broker olur. Üretilen servisler kendi izole compose'larında RabbitMQ container'ı açmaz; `appsettings.json`'daki `RabbitMq:Host` varsayılanı `host.docker.internal`'dır (bkz. §5.1 cross-service host notu).

**v1 sınırlamaları (bilinçli, dokümante edilen basitlik):**

- ~~Tüketici tarafı: DLQ/retry politikası yok~~ **DLQ çözüldü (2026-07-16):** Daha önce `nack(requeue: false)` ile reddedilen bir mesaj, kuyrukta hiçbir dead-letter-exchange tanımlı olmadığı için RabbitMQ tarafından **sessizce ve kalıcı olarak siliniyordu** — gerçek bir veri kaybıydı. Artık her abonelik için paylaşılan bir `{ExchangeName}.dlx` (fanout) exchange'e bağlı bir `{queue}.dead` kuyruğu declare ediliyor (asıl kuyruk `x-dead-letter-exchange` argümanıyla açılıyor); reddedilen mesaj artık kaybolmuyor, `{queue}.dead`'de (RabbitMQ management UI'dan) görülüp incelenebiliyor/elle replay edilebiliyor. **Bilinçli kapsam dışı:** Otomatik N-kere-yeniden-dene-sonra-DLQ (retry-with-delay) eklenmedi — TTL+DLX zincirleme (delay queue pattern) gerektirir, canlı bir broker'a karşı doğrulanmadan doğru kurulduğuna güvenmek riskli; ayrı bir gelecek iş.
- Outbox relay tarafı: **at-least-once teslim, exactly-once değil** — publish RabbitMQ'ya gittikten sonra `ProcessedAt` commit edilmeden process çökerse mesaj bir sonraki taramada tekrar gönderilebilir. Asıl çözülen sorun (commit sonrası publish başarısızlığında event'in tamamen kaybolması) tamamen giderildi. ~~Tüketici tarafı `EventId` bazlı idempotency yok~~ **Çözüldü (2026-07-16) — Inbox pattern:** `InboxMessage` (Core, `OutboxMessage` ile aynı gerekçeyle marker interface implemente etmez) + `BaseForgeDbContext.InboxMessages`. `RabbitMqConsumerHostedService.HandleDeliveryAsync`, MediatR'a dağıtmadan önce `InboxMessages`'ta aynı `EventId`'yi arar — bulursa handler'ı tekrar çalıştırmadan `ack`'ler. İşaretleme **handler'dan SONRA** yapılır (mark-after, mark-before değil): handler çökerse Inbox satırı henüz commit edilmediği için yeniden teslimat hâlâ "işlenmemiş" görünüp tekrar denenir — mark-before olsaydı event yanlışlıkla "zaten işlendi" sayılıp kaybolurdu. Kalan kısıt: bu iki adım (handler'ın kendi DB etkileri + Inbox satırı) TEK bir transaction'da değil — handler başarılı ama Inbox commit/ack arasında çökme olursa nadir bir gerçek duplicate işlem olabilir (bugünkünden çok daha iyi, mükemmel değil).
- ~~Outbox'ta cleanup/retention job yok~~ **Çözüldü (2026-07-16)** — yukarıya bkz.
- ~~Outbox'ta sınırsız retry var~~ **Çözüldü (2026-07-16)** — max-retry sonrası dead marking, yukarıya bkz. (Outbox'ın kendi "dead" satırları için ayrı bir DLQ/broker yolu yoktur, tabloda kalır — tüketici tarafındaki broker DLQ'su [aşağıdaki madde] farklı bir mekanizma.)
- `OutboxMessages` tablosu da diğer tüm entity tabloları gibi migration'sız, yalnızca `Database.EnsureCreated()` (Development ortamı) ile oluşur.
- ~~Kanal havuzu yok~~ **Çözüldü (2026-07-16):** `RabbitMqConnectionManager`'a sınırlı (kapasite 10) bir `RentChannelAsync`/`ReturnChannelAsync` havuzu eklendi; `RabbitMqPublisher.PublishRawAsync` artık her publish'te aç/kapat yerine bunu kullanır. Tüketici hosted service'i zaten uygulama ömrü boyunca tek bir kanal tuttuğu için değişmedi (havuza ihtiyacı yok).
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

### 5.6. Merkezi Loglama ve Correlation ID

Her mikroservis kendi konsol çıktısına hapsolmuş durumdaydı — bir isteği HTTP → gRPC → RabbitMQ event zinciri boyunca servisler arasında takip etmenin yolu yoktu. Serilog + Grafana Loki ile merkezi, yapılandırılmış (structured) loglama ve üç sınırı (HTTP/gRPC/RabbitMQ) aşan bir `CorrelationId` eklendi.

**Tasarım kararı — her zaman açık:** `ExceptionHandlingMiddleware`/`RequestLoggingMiddleware` gibi bu da `spec.yaml`'da opt-in bir toggle **değildir** — `ServiceSpec`/`CodeModel`/Designer UI'a dokunulmadı, yalnızca sabit CodeGen template'lerine eklendi. Loki URL'i boşsa/erişilemezse servis konsola loglamaya devam eder (RabbitMq'nun `host.docker.internal` ile paylaşılan broker'a bağlanma deseniyle tutarlı — Loki'nin ayakta olması bir ön koşul değildir); Loki sink'i içeride bir yazma hatasıyla karşılaşırsa (2026-07-16) artık `Serilog.Debugging.SelfLog` ile stderr'e bir tanılama satırı düşer — tamamen sessiz değildir.

- `ICorrelationIdAccessor` (Core, `BaseForge.Core.Logging`) — o anki akışın correlation id'sine ambient erişim sözleşmesi. `CorrelationIdAccessor` (Infrastructure) `AsyncLocal<string?>` ile implemente eder, Singleton kaydedilir; async çağrı zinciri boyunca (HTTP → handler → outbox yazımı, gRPC çağrısı, consumer'ın MediatR dispatch'i) doğru akar, eşzamanlı farklı akışlar arasında sızmaz.
- **HTTP sınırı:** `CorrelationIdMiddleware` (API) — pipeline'ın en başına eklenir (`ExceptionHandlingMiddleware`'den bile önce). Gelen `X-Correlation-Id` header'ını kullanır (yoksa üretir), accessor'a yazar, Serilog `LogContext`'e ekler, response'a da aynı header'ı geri yazar.
- **gRPC sınırı:** `CorrelationIdClientInterceptor`/`CorrelationIdServerInterceptor` (API, `BaseForge.API.Grpc`) — giden çağrının metadata'sına `correlation-id` eklenir, sunucu tarafında okunup accessor/LogContext'e yazılır. CodeGen Program.cs şablonu her `AddGrpcClient<...>()` çağrısına `.AddInterceptor<CorrelationIdClientInterceptor>()`, `AddGrpc()` çağrısına da server interceptor'ı otomatik ekler.
- **RabbitMQ sınırı:** `EventEnvelope`'a (Infrastructure) `CorrelationId` alanı eklendi — DB şema/migration değişikliği **yok** (`OutboxMessage.Payload` zaten tam JSON blob'u, yeni alan sadece o JSON'un bir üyesi). `OutboxEventBus.PublishAsync`, envelope'u oluştururken accessor'ın o anki değerini gömer; `RabbitMqConsumerHostedService.HandleDeliveryAsync`, mesajı MediatR'a dağıtmadan önce bu id'yi yeni scope'un accessor'ına ve `LogContext`'e geri yükler — event'i işleyen handler'ın logları, olayı tetikleyen orijinal istekle aynı id'yi taşır.
- **Serilog wiring:** yeni `WebApplicationBuilder.AddBaseForgeLogging(serviceName)` (API) — `AddBaseForge` (services, DI) çağrısından **ayrı**, `builder.Host.UseSerilog(...)` seviyesinde çağrılır (Serilog logging provider'ı Host üzerinden değiştirir). Her log satırı `Service` alanıyla etiketlenir; `Serilog:LokiUrl` appsettings anahtarı doluysa `Serilog.Sinks.Grafana.Loki` ile push edilir, boşsa yalnızca konsola yazılır.
- Kök `docker-compose.yml`'a paylaşılan `loki` + `grafana` container'ları eklendi (Postgres/RabbitMQ ile aynı desen); Grafana, `grafana/provisioning/datasources/loki.yaml` ile Loki datasource'unu otomatik provision eder (elle "Add datasource" gerekmez).

**v1 sınırlamaları (bilinçli, dokümante edilen basitlik):**

- ~~Grafana'da hazır bir dashboard yok~~ **Çözüldü (2026-07-16):** `grafana/dashboards/baseforge-logs.json` (provisioning: `grafana/provisioning/dashboards/dashboards.yaml`) — `Service`/`CorrelationId` template değişkenleriyle filtrelenebilen bir log paneli + servise göre log hacmi zaman serisi paneli. Daha ileri düzey sorgular hâlâ LogQL ile Explore üzerinden yapılır.
- Log retention/rotation: `grafana/loki-config.yaml` (2026-07-16) ile 7 gün (`retention_period: 168h`, `compactor.retention_enabled: true`) olarak yapılandırıldı — değiştirmek için bu dosyayı düzenleyip `docker compose up -d loki` ile yeniden başlatmak yeterli. **Not:** bu config dosyası canlı bir Loki container'ına karşı çalıştırılıp doğrulanmadı (önceki turlardaki "docker compose up ile canlı doğrulama kapsam dışı" kararıyla tutarlı).
- `Serilog:LokiUrl` boşsa/erişilemezse konsola düşülür; erişilemezse artık `SelfLog` ile stderr'e tanılama mesajı yazılır (2026-07-16) — ama bu tam bir healthcheck/retry değil, yalnızca görünürlük.
- gRPC client interceptor'ı yalnızca unary çağrıları destekler (CodeGen bugün yalnızca unary `GetById` üretiyor — streaming RPC yok).

### 5.7. Health Check ve Servis Durumu İzleme

Docker-compose'daki `healthcheck:` blokları önceden yalnızca altyapı container'ları (Postgres `pg_isready`,
RabbitMQ `rabbitmq-diagnostics ping`) içindi — üretilen servisin kendi uygulama container'ının canlı olup
olmadığını gösteren bir app-level probe yoktu, dolayısıyla Identity dashboard'unun "Servisler" bölümü de
sadece codegen anında donmuş bir `services.json` anlık görüntüsü gösteriyordu (isim/port/entity sayısı,
canlılık bilgisi yok).

**Tasarım kararı — her zaman açık:** §5.6'daki loglama gibi, `/health` de `spec.yaml`'da opt-in bir toggle
**değildir** — amacı tam olarak Identity'nin her servisi güvenilir şekilde yoklayabilmesi; opt-in olsaydı
bazı servisler dashboard'da görünmezdi.

- **`/health` endpoint'i (BaseForge.API):** `AddBaseForge` her zaman `AddHealthChecks()` çağırır; `UsePostgreSQL`
  ile bir bağlantı dizesi verildiyse (her zaman verilir) `PostgresHealthCheck` (Infrastructure, ham
  `NpgsqlConnection` + `SELECT 1` — ayrı bir `AspNetCore.HealthChecks.NpgSql` bağımlılığı eklemeden) bir
  `"postgresql"` check'i olarak eklenir. `UseBaseForge` (artık `WebApplication` alıyor — endpoint eşleme
  gerektiği için `IApplicationBuilder`'dan genişletildi) `/health`'i JWT/`[Authorize]`'dan bağımsız
  (`Protect` per-controller uygulanıyor, global filtre yok) küçük özel bir JSON response writer'la eşler:
  `{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy","durationMs":12}]}`.
- **Docker healthcheck:** CodeGen'in `docker-compose.yml`/`Dockerfile` şablonlarına (`Templates.cs`,
  identity için `IdentityGenerator.BuildCompose`/`BuildDockerfile`) `curl -f http://localhost:8080/health`
  tabanlı bir `healthcheck:` bloğu eklendi; `mcr.microsoft.com/dotnet/aspnet:10.0` curl içermediği için final
  Docker stage'e `apt-get install curl` eklendi.
- **Identity'nin canlı yoklaması (`ServicesApiController.Status`, `GET /api/services/status`):** Identity
  kendisi hariç kayıtlı her servisi `host.docker.internal:{restPort}/health` üzerinden yoklar — her üretilen
  servis kendi bağımsız docker-compose ağında çalıştığı için (container DNS'i paylaşılmıyor), mevcut
  cross-service gRPC deseniyle (§7.1, `CrossServiceHost = "host.docker.internal"`) aynı host-mapped port
  yaklaşımı kullanılır. Bu, kod tabanındaki **ilk server-to-server `HttpClient`** kullanımı (`"ServiceHealthClient"`,
  2 saniye timeout, `AddHttpClient` ile adlandırılmış) — bugüne kadar servisler arası iletişim yalnızca
  gRPC/RabbitMQ idi.
- **Dashboard (React):** `Home.tsx` mevcut statik `services.json` listesini (isim/port/entity sayısı)
  `/api/services/status`'un döndürdüğü canlı `{name, healthy, checkedAt}` listesiyle isim eşleştirerek
  birleştirir; 10 saniyede bir `setInterval` ile yeniler, her kartta yeşil/kırmızı/gri nokta + "Ayakta"/
  "Kapalı"/"Kontrol ediliyor…" rozeti gösterir.

**v1 sınırlamaları (bilinçli, dokümante edilen basitlik):**

- Geçmişe dönük uptime/downtime kaydı veya grafik yok — sadece anlık durum (pull/polling, push değil).
- `/health` ve `/api/services/status` kimlik doğrulamasız — statik `services.json`'ın zaten paylaştığı
  trust seviyesiyle tutarlı (internal/ops amaçlı, dashboard zaten aynı trust boundary'de).
- Otomatik alarm/bildirim (servis düşünce e-posta/Slack) yok — ayrı bir gelecek özellik.
- Yerel (`dotnet run`, container dışı) çalıştırmada `host.docker.internal` çözümlemesi garanti değil —
  mevcut gRPC cross-service deseninin de paylaştığı bilinen bir kısıt, yeni bir risk değil.

## 6. Kimlik Doğrulama

- Merkezi tek bir **Identity Service** vardır (JWT / OAuth2).
- Her servis JWT token'ı kendi middleware'inde **lokal olarak** validate eder; her istekte merkezi DB'ye çağrı yapılmaz.

## 7. Containerization

- Her servis için ayrı `Dockerfile`.
- Tüm servisler tek bir `docker-compose.yml` ile ayağa kalkar. **Not:** CodeGen bugün her servis için ayrı, izole bir `docker-compose.yml` üretir (kendi Postgres'i ile); kökteki paylaşılan `docker-compose.yml` yalnızca tek-örnek altyapı için kullanılır (Postgres + RabbitMQ, bkz. §5.2) — üretilen servisler bu paylaşılan broker'a `host.docker.internal` üzerinden bağlanır.
- Konfigürasyon `.env` dosyasından okunur; production'da `.env.production` kullanılır.

### 7.1. Servis Kaydı (`ServiceRegistry`) — Port/Authority Doğruluğu

`ServiceRegistry.cs` her üretimde workspace kökünde (üretilen servis klasörünün bir üstünde)
paylaşılan bir `services.json` tutar: `Name`, `RestPort`, `GrpcPort`, `PostgresPort`, `IsIdentity`,
`Authority`, `Audience`, `Protected`. Bunun iki tüketicisi var:

- **Identity dashboard'u** — üretim sırasında bu kaydın güncel hali identity'nin kendi `wwwroot`'una
  kopyalanıp imaja gömülür (`SnapshotForIdentity`), "Servisler" bölümü bunu okur.
- **CodeGen'in kendisi** — bir servis `identity/User`'a (`via: grpc`) referans verdiğinde, Identity'nin
  **gerçek** gRPC portu bu kayıttan okunur (`ServiceRegistry.LoadForWorkspace`); kardeş bir servise
  referans veriliyorsa (identity dışı) port doğrudan kardeşin kendi `spec.yaml`'inden (`DockerPorts.Grpc`)
  okunur. **Karar değişikliği (2026-07-13):** Önceden appsettings.json'daki `Grpc:{Provider}` adresi
  portu hardcoded `8081` yazıyordu — sağlayıcının gerçekte hangi portu kullandığından bağımsız. Bu,
  herkes varsayılan portları kullandığı sürece fark edilmiyordu; portlar artık rutin olarak farklılaşacağı
  (bkz. §7.2) için gerçek bir bağlantı hatasına dönüşürdü. Kayıt/kardeş-spec'te port bulunamazsa
  eski varsayılana (`8081`, identity için `8082`) sessizce düşülür — hata fırlatılmaz.

### 7.2. Designer — Otomatik Artan Port/Authority Önerisi

Designer, `/api/workspace` ile bu kaydı okuyup **yeni** bir servis/identity açıldığında (spec.yaml/
auth.yaml diskte henüz yoksa) REST/gRPC/Postgres portlarını, workspace'teki en yüksek kullanılan
değerin bir fazlası olacak şekilde **gerçek, düzenlenebilir varsayılan değer** olarak önceden doldurur
(salt placeholder metni değil) — kullanıcı hâlâ elle değiştirebilir. Authority alanı da aynı şekilde,
workspace'te bir Identity kaydı varsa `http://host.docker.internal:{identity'nin gerçek REST portu}`
olarak önerilir (önceden hardcoded `http://localhost:5090` idi — bu, Docker container içinden asla
erişilemeyen bir adres, çünkü `5090` yalnızca Identity'nin yerel `dotnet run` portu). Bir servis/identity
üretildikten sonra (aynı Designer oturumunda ikisi art arda üretilebildiği için) workspace yeniden
okunur; kullanıcının elle değiştirmediği (hâlâ önceki önerilen değere eşit) alanlar canlı güncellenir,
elle girilmiş bir değer asla ezilmez.

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
| 2026-07-13 | Docker port/Authority doğruluğu eklendi (bkz. §7.1/7.2): `ServiceRegistry`'ye Postgres portu + public `LoadForWorkspace`; gRPC cross-service appsettings adresi artık gerçek (sağlayıcının kendi spec'inden veya identity kaydından okunan) portu kullanıyor — önceden hardcoded `8081`'di, gerçek bir bağlantı hatasıydı. Designer artık yeni bir servis/identity açıldığında portları/Authority'yi workspace kaydından otomatik, çakışmayacak şekilde önceden dolduruyor (elle değiştirilebilir); Identity ve normal bir servis aynı oturumdan art arda üretildiğinde öneriler canlı güncelleniyor. | ✅ |
| 2026-07-16 | Transactional Outbox Pattern eklendi (bkz. §5.2): `IEventBus` artık RabbitMQ'ya doğrudan yazmıyor, `OutboxEventBus` olayı aynı `SaveChangesAsync` transaction'ında bir `OutboxMessage` satırı olarak yazıyor; `OutboxPublisherHostedService` (`FOR UPDATE SKIP LOCKED` ile çoklu-instance güvenli) bunu ayrı, güvenilir bir relay ile gerçek broker'a gönderiyor. DB commit ile RabbitMQ publish arasındaki dual-write/event-kaybı riski çözüldü (at-least-once garantisiyle). `RabbitMqEventBus` → `RabbitMqPublisher`/`IRabbitMqPublisher` olarak refactor edildi (wire format değişmedi). | ✅ |
| 2026-07-16 | Merkezi loglama (Serilog + Grafana Loki) + Correlation ID eklendi (bkz. §5.6): `ICorrelationIdAccessor` (AsyncLocal) HTTP middleware, gRPC client/server interceptor'ları ve RabbitMQ outbox/consumer arasında paylaşılıyor — bir isteğin üç sınır boyunca (HTTP/gRPC/RabbitMQ) aynı id ile loglanmasını sağlıyor. `AddBaseForgeLogging` (Host seviyesi, `AddBaseForge`'dan ayrı) her zaman konsola, `Serilog:LokiUrl` doluysa Loki'ye de yazıyor. Kök `docker-compose.yml`'a paylaşılan `loki`/`grafana` container'ları eklendi. Her zaman açık (RabbitMQ/JWT gibi opt-in bir spec toggle değil) — `ServiceSpec`/Designer UI'a dokunulmadı. | ✅ |
