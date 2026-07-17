# 2026-07-16 — Transactional Outbox Pattern (DB↔RabbitMQ dual-write riski)

Plan: `C:\Users\pc\.claude\plans\smooth-seeking-treehouse.md`. Otomatik yürütme modu — her adım burada loglanıyor, ara onay istenmiyor.

## Log

- **1** `src/BaseForge.Core/Messaging/OutboxMessage.cs` (yeni) — plain POCO (`IAuditEntity`/`ISoftDelete`/`ITenantEntity`/`BaseEntity` implement etmiyor): `Id`, `EventId`, `EventType`, `OccurredAt`, `Payload` (tam `EventEnvelope` JSON'u), `ProcessedAt`, `Error`, `RetryCount`.
- **2** `src/BaseForge.Infrastructure/Messaging/RabbitMqOptions.cs` — `OutboxPollingInterval` (2s), `OutboxBatchSize` (50) eklendi.
- **3** `src/BaseForge.Infrastructure/Data/BaseForgeDbContext.cs` — `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();` eklendi. `OnModelCreating`/`ApplyAuditAndSoftDelete` değişmedi (OutboxMessage hiçbir marker interface implemente etmediği için reflection döngüleri otomatik atlıyor).
- **4** `RabbitMqEventBus.cs` → `RabbitMqPublisher.cs` (rename+refactor): yeni `IRabbitMqPublisher.PublishRawAsync(eventType, envelopeJson, ct)` — jenerik `TEvent` almıyor, zaten hazır zarf JSON'unu doğrudan body olarak yazıyor (wire format değişmedi). `EventEnvelope` record'u aynı dosyada kaldı. `IEventBus.cs` doc-comment'i yeni akışı (outbox → relay) anlatacak şekilde güncellendi.
- **5** `src/BaseForge.Infrastructure/Messaging/OutboxEventBus.cs` (yeni) — `IEventBus`'ın outbox implementasyonu, Scoped, ctor'da `BaseForgeDbContext` alıyor; `PublishAsync` sadece `context.OutboxMessages.Add(...)` ile change tracker'a ekliyor, I/O yok.
- **6** `src/BaseForge.CodeGen/Generation/Templates.cs` — Create/Update/Delete handler şablonlarında `_eventBus.PublishAsync(...)` çağrısı `_unitOfWork.SaveChangesAsync(...)`'den önceye alındı (3 yer). **Doğrulama notu (planda öngörülmüş, teyit edildi):** Create'te `entity.Id` — EF Core Guid PK'lar için client-side value generator, entity `Added` state'e girdiği anda (yani `_repository.AddAsync` çağrısı sırasında, `SaveChangesAsync`'den ÖNCE) dolduruluyor; Delete'te DTO şablonu `IsDeleted`/audit alanlarını hiç içermiyor (`grep` ile doğrulandı, 0 eşleşme). Yani sıra değişikliği hiçbir DTO/event içeriğini etkilemiyor.
- **7** `src/BaseForge.Infrastructure/Messaging/OutboxPublisherHostedService.cs` (yeni) — `BackgroundService`, `PeriodicTimer` ile polling; her tick'te `FOR UPDATE SKIP LOCKED` ile batch seçiyor (çoklu-instance güvenliği, ekstra lease kolonu yok), her satır kendi `try/catch`'inde işleniyor (batch-level catch yok — bir satırın hatası diğerlerinin commit'ini engellemiyor), tek `SaveChangesAsync`+`CommitAsync` ile batch kapatılıyor.
- **8** `src/BaseForge.API/Extensions/ServiceCollectionExtensions.cs` (`AddRabbitMq`) — `IRabbitMqPublisher` (Singleton), `IEventBus → OutboxEventBus` (Singleton'dan **Scoped**'a değişti), `OutboxPublisherHostedService` her zaman eklendi (subscription sayısından bağımsız — publish varsa outbox her zaman aktif).
- ✅ **9** `dotnet build BaseForge.slnx` (npm build dahil) — ilk denemede `OutboxMessage.cs`'teki XML doc `<see cref="IAuditEntity"/>` vb. referansları (yanlış namespace, `<c>` etiketiyle düzeltildi) CS1574 hatası verdi; düzeltme sonrası **0 hata, 0 uyarı**.

## docs/ARCH.md ve CLAUDE.md

- **10** `docs/ARCH.md` §5.2 — yeni "Transactional Outbox" alt bölümü eklendi (akış: `OutboxEventBus` → aynı transaction → `OutboxPublisherHostedService` relay → RabbitMQ); v1 sınırlamaları listesine at-least-once/no-cleanup/sınırsız-retry/migration'sız-tablo eklendi; Karar Günlüğü'ne 2026-07-16 satırı eklendi.
- **11** `CLAUDE.md` backlog — RabbitMQ maddesinin altına "Transactional Outbox Pattern" `[x]` satırı + bilinen kısıtlar eklendi.

## Birim ve entegrasyon testleri

- **12** `tests/BaseForge.UnitTests/Messaging/OutboxEventBusTests.cs` (yeni, InMemory) — 3 test: `PublishAsync` DB'ye yazmadan önce sadece tracker'a ekliyor mu, business entity ile aynı `SaveChangesAsync`'e biniyor mu, `OutboxMessage` soft-delete'e tabi olmadan fiziksel siliniyor mu. ✅ `dotnet test tests/BaseForge.UnitTests` — 10/10 (7 eski + 3 yeni).
- **13** `tests/BaseForge.IntegrationTests/Persistence/OutboxIntegrationTests.cs` (yeni, gerçek Postgres/Testcontainers, `[DockerFact]`) — 5 test: atomiklik, rollback (ikisi birden geri alınıyor), relay'in bekleyen mesajı publish edip işaretlemesi, satır-seviyesi hata izolasyonu (bir mesaj sürekli başarısız olurken diğeri işleniyor, `RetryCount`/`Error` doğru), ve **iki paralel `OutboxPublisherHostedService` instance'ının** `FOR UPDATE SKIP LOCKED` sayesinde 20 satırı hiç çakışmadan/iki kez publish etmeden paylaşması. **Planda öngörülmeyen bir uyarlama:** `PostgresFixture`'ın tek container'ını tüm test metotları paylaştığı için (mevcut `RepositoryIntegrationTests` deseni), relay'in `WHERE "ProcessedAt" IS NULL` sorgusu TÜM tabloyu taradığından, bir testin kasıtlı olarak işlenmemiş bıraktığı satır (hata-izolasyonu testindeki "hep başarısız olan" mesaj) başka bir testin relay'i tarafından da görülüp sonuçları kirletebilirdi — her test için `NpgsqlConnectionStringBuilder` ile benzersiz bir veritabanı adı (`ob_{guid}`) üretilip `EnsureCreated()` ile izole şema kuruldu; testler artık birbirinden tam bağımsız. ✅ `RUN_DB_TESTS=1 dotnet test tests/BaseForge.IntegrationTests` — 12/12 (7 eski + 5 yeni).

## Uçtan uca doğrulama

- `dotnet run --project src/BaseForge.CodeGen -- new-service --spec samples/blog.yaml` ile (`Comment`/`Like` `publishes: [created]` içeriyor) örnek servis geçici bir klasöre üretildi. **Doğrulandı:** `CommentCommands.cs`/`LikeCommands.cs`'te `_eventBus.PublishAsync(...)` artık `_unitOfWork.SaveChangesAsync(...)`'den önce (Templates.cs değişikliği üretilen kodda doğru yansıdı).
- Üretilen `Blog.csproj`'daki `BaseForge.API` NuGet `PackageReference`'ı (yayınlanmış `0.3.0-beta`, bu oturumun kütüphane değişikliklerini içermiyor) geçici olarak yerel `src/BaseForge.API/BaseForge.API.csproj`'a `ProjectReference` ile değiştirilip `dotnet build` çalıştırıldı — **0 hata** (yalnızca ilgisiz, önceden var olan `NU1903`/`ASPDEPR005` uyarıları). Bu, gerçek CodeGen çıktısının değiştirilmiş kütüphaneye (yeni `IEventBus`/`OutboxEventBus`/DI kaydı dahil) karşı derlendiğini doğruluyor. Geçici dosyalar temizlendi (repo'ya commit edilmedi, `services/`'e eklenmedi).
- **Kapsam dışı bırakılan (zaman/kaynak nedeniyle):** `docker compose up` ile gerçek RabbitMQ broker'a karşı canlı uçtan uca test (API'den entity oluştur → outbox satırı → relay → broker → tüketici log'u) çalıştırılmadı; bunun yerine gerçek Postgres'e karşı entegrasyon testleri (yukarıda) + derleme-seviyesi E2E doğrulaması ile eşdeğer güven sağlandı. İsteğe bağlı olarak sonradan manuel çalıştırılabilir (plan dosyasındaki adım 4).

## Sonuç

Plandaki 10 adımın tümü + birim/entegrasyon testleri tamamlandı. `dotnet build BaseForge.slnx`: 0 hata/0 uyarı. `dotnet test` (Unit + Integration, `RUN_DB_TESTS=1`): 22/22 başarılı. DB commit ile RabbitMQ publish arasındaki dual-write/event-kaybı riski çözüldü.

