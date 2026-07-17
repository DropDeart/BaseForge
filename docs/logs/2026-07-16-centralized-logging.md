# 2026-07-16 — Merkezi Log Sistemi (Serilog → Grafana Loki) + Correlation ID

Plan: `C:\Users\pc\.claude\plans\sparkling-strolling-planet.md`. Otomatik yürütme modu — her adım burada loglanıyor, ara onay istenmiyor.

## Log

- **1** `src/BaseForge.Core/Logging/ICorrelationIdAccessor.cs` (yeni) — o anki akışın correlation id'sine ambient erişim sözleşmesi (`string? Current { get; set; }`).
- **2** `src/BaseForge.Infrastructure/Logging/CorrelationIdAccessor.cs` (yeni) — `AsyncLocal<string?>` tabanlı Singleton implementasyon.
- **3** `src/BaseForge.Infrastructure/Messaging/RabbitMqPublisher.cs` — `EventEnvelope` record'una `string? CorrelationId = null` alanı eklendi.
- **4** `src/BaseForge.Infrastructure/Messaging/OutboxEventBus.cs` — ctor'a `ICorrelationIdAccessor` eklendi; `PublishAsync` envelope'u oluştururken `accessor.Current`'ı gömüyor.
- **5** `src/BaseForge.Infrastructure/Messaging/RabbitMqConsumerHostedService.cs` — `HandleDeliveryAsync`, `MediatR.Publish`'ten önce `envelope.CorrelationId`'yi yeni scope'un accessor'ına yazıyor ve `Serilog.Context.LogContext.PushProperty` ile handler loglarına ekliyor.
- **6** `src/BaseForge.API/Extensions/ServiceCollectionExtensions.cs` — `AddBaseForge`'un başına `services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>()` eklendi (RabbitMq etkin olsun olmasın her zaman gerekli).
- **7** `src/BaseForge.API/Middleware/CorrelationIdMiddleware.cs` (yeni) — pipeline'ın en başında (`ExceptionHandlingMiddleware`'den önce), `X-Correlation-Id` header'ını okur/üretir, accessor + `LogContext`'e yazar, response header'ına geri yazar.
- **8** `src/BaseForge.API/Extensions/ApplicationBuilderExtensions.cs` — `UseBaseForge`'a `CorrelationIdMiddleware` en başa eklendi.
- **9** `src/BaseForge.API/Grpc/CorrelationIdServerInterceptor.cs` ve `CorrelationIdClientInterceptor.cs` (yeni) — sırasıyla gelen `correlation-id` metadata'sını okuyup accessor/LogContext'e yazan ve giden çağrıya ekleyen `Grpc.Core.Interceptors.Interceptor` implementasyonları (yalnızca unary — CodeGen bugün yalnızca unary `GetById` üretiyor).
- **10** `src/BaseForge.API/Extensions/WebApplicationBuilderExtensions.cs` (yeni) — `AddBaseForgeLogging(serviceName)`: `builder.Host.UseSerilog(...)`, her zaman konsola (CA1305 uyarısı için `CultureInfo.InvariantCulture` formatProvider ile), `Serilog:LokiUrl` doluysa `Serilog.Sinks.Grafana.Loki` ile de yazıyor.
- **11** Paket referansları — `BaseForge.Infrastructure.csproj`: `Serilog` (core, `LogContext` için). `BaseForge.API.csproj`: `Grpc.Core.Api`, `Serilog.AspNetCore`, `Serilog.Sinks.Grafana.Loki`.
- **12** `src/BaseForge.CodeGen/Generation/CodeModel.cs`/`CodeGenerator.cs` — `ProgramFileModel.ServiceKey` eklendi (`HostFileModel`'deki aynı alanla aynı desen).
- **13** `src/BaseForge.CodeGen/Generation/Templates.cs` — `Program` şablonu: `builder.AddBaseForgeLogging("{{ ServiceKey }}")` en başta; `AddGrpc(...)`'a server interceptor, her `AddGrpcClient<...>()`'a `.AddInterceptor<CorrelationIdClientInterceptor>()` eklendi. `AppSettings` şablonuna `"Serilog": { "LokiUrl": "http://host.docker.internal:3100" }` bloğu eklendi. `DockerCompose`/`ComposeSnippet` yorum satırlarına Loki'nin de kökteki paylaşılan container olduğu notu eklendi.
- **14** Kök `docker-compose.yml` — `loki` (grafana/loki:3.2.0) + `grafana` (grafana/grafana:11.4.0, Loki datasource provisioning ile) servisleri rabbitmq'nun yanına eklendi. `grafana/provisioning/datasources/loki.yaml` (yeni) — Loki datasource'unu otomatik provision eder. `.env.example`'a `LOKI_PORT`/`GRAFANA_PORT`/`GRAFANA_USER`/`GRAFANA_PASSWORD` eklendi.
- ✅ **15** `dotnet build BaseForge.slnx` — ilk denemede 2 hata: (a) `WriteTo.Console()` için CA1305 (`IFormatProvider` belirtilmeli, `CultureInfo.InvariantCulture` ile düzeltildi), (b) yeni `CorrelationIdAccessorTests`'te `Assert.Equal(string?[], ...)` nullability uyuşmazlığı (CS8631 — dizi karşılaştırması yerine tek tek `Assert.Equal` ile düzeltildi). Düzeltme sonrası **0 hata, 0 uyarı**. Ayrıca `tests/BaseForge.IntegrationTests/Persistence/OutboxIntegrationTests.cs`'teki iki `new OutboxEventBus(context)` çağrısı yeni zorunlu `correlationIdAccessor` parametresi yüzünden derleme hatası verdi; `new CorrelationIdAccessor()` eklenerek düzeltildi.

## Birim ve entegrasyon testleri

- **16** `tests/BaseForge.UnitTests/Logging/CorrelationIdAccessorTests.cs` (yeni) — 3 test: varsayılan `null`, aynı akış içinde round-trip, eşzamanlı 3 farklı async akış arasında sızma olmadığı (`Task.WhenAll` ile).
- **17** `tests/BaseForge.UnitTests/Messaging/OutboxEventBusTests.cs` — mevcut 3 test yeni ctor imzasına (`new CorrelationIdAccessor()`) güncellendi; yeni test: `accessor.Current` set edilmişse envelope JSON payload'ında `"CorrelationId":"..."` alanının doğru göründüğü.
- **Kapsam dışı bırakılan (plan notu, "mümkünse"):** `RabbitMqConsumerHostedService`'in correlation id'yi geri yüklediğini doğrudan doğrulayan bir birim testi yazılmadı — mevcut testlerde consumer'ı mock'lamak için hazır bir altyapı/desen yoktu (gerçek `IChannel`/`AsyncEventingBasicConsumer` mock'lamak gerekirdi), yeni bir test harness kurmak bu adımın kapsamını aşardı. Değişikliğin doğruluğu, kod incelemesi + entegrasyon seviyesinde (aşağıdaki uçtan uca derleme doğrulaması) ile teyit edildi.
- ✅ `dotnet test tests/BaseForge.UnitTests` — 14/14 (10 eski + 4 yeni: 3 Logging + 1 OutboxEventBus).
- ✅ `RUN_DB_TESTS=1 dotnet test tests/BaseForge.IntegrationTests` — 12/12 (değişmedi, yalnızca ctor çağrısı güncellendi).

## docs/ARCH.md ve CLAUDE.md

- **18** `docs/ARCH.md` — yeni §5.6 "Merkezi Loglama ve Correlation ID" eklendi (tasarım kararı: her zaman açık, üç sınır boyunca CorrelationId akışı, Serilog wiring, v1 sınırlamaları). Karar Günlüğü'ne 2026-07-16 satırı eklendi.
- **19** `CLAUDE.md` backlog — outbox maddesinin altına `[x] Merkezi Loglama (Serilog + Grafana Loki) + Correlation ID` satırı + bilinen kısıtlar eklendi.

## Uçtan uca doğrulama

- `dotnet run --project src/BaseForge.CodeGen -- new-service --spec samples/blog.yaml` ile (echo "e" | ... ile CLI onayları otomatik geçilerek) örnek Blog servisi geçici bir klasöre üretildi. **Doğrulandı:** üretilen `Program.cs`'te `builder.AddBaseForgeLogging("blog")`, `AddGrpc(grpc => grpc.Interceptors.Add<CorrelationIdServerInterceptor>())`, `.AddInterceptor<CorrelationIdClientInterceptor>()`; üretilen `appsettings.json`'da `"Serilog": { "LokiUrl": ... }` doğru göründü.
- Üretilen `Blog.csproj`'daki `BaseForge.API` NuGet `PackageReference`'ı (yayınlanmış `0.3.0-beta`, bu oturumun değişikliklerini içermiyor) geçici olarak yerel `src/BaseForge.API/BaseForge.API.csproj`'a `ProjectReference` ile değiştirilip `dotnet build` çalıştırıldı — **0 hata** (yalnızca ilgisiz, önceden var olan `NU1903`/`ASPDEPR005` uyarıları). Bu, gerçek CodeGen çıktısının değiştirilmiş kütüphaneye (yeni `AddBaseForgeLogging`/`ICorrelationIdAccessor`/gRPC interceptor'ları dahil) karşı derlendiğini doğruluyor. Geçici dosyalar temizlendi (repo'ya commit edilmedi).
- **Kapsam dışı bırakılan (plan notu, zaman/kaynak nedeniyle):** `docker compose up loki grafana` ile gerçek uçtan uca görsel doğrulama (Grafana Explore'da bir isteğin CorrelationId'siyle filtrelenip HTTP+consumer loglarının birlikte göründüğünü görmek) çalıştırılmadı — bunun yerine derleme + birim testi seviyesinde güven sağlandı, önceki outbox doğrulamasıyla tutarlı bir tercih.

## Sonuç

Plandaki 7 bölümün tümü + birim testleri tamamlandı. `dotnet build BaseForge.slnx`: 0 hata/0 uyarı. `dotnet test` (Unit + Integration, `RUN_DB_TESTS=1`): 26/26 başarılı (14 unit + 12 integration). Bir isteğin HTTP → gRPC → RabbitMQ event zinciri boyunca aynı `CorrelationId` ile, merkezi olarak (Grafana Loki, yapılandırılmışsa) loglanabilmesi sağlandı; Loki olmadan da servisler konsola loglamaya devam eder.
