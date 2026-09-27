# BaseForge nedir?

**BaseForge**, **.NET 10 mikroservisleri için opinionated, yeniden kullanılabilir bir temel kütüphane ve kod üreticidir**. Her yeni backend'de aynı mimariyi sıfırdan kurmak yerine BaseForge'u extend edersin — tekrar eden kısımları da onun üreticisi yazar.

Birlikte çalışan iki parçadan oluşur:

| | Nedir | Ne için kullanırsın |
| --- | --- | --- |
| **Kütüphane** | `BaseForge.Core`, `BaseForge.Infrastructure`, `BaseForge.API` NuGet paketleri | Tek bir `AddBaseForge()` çağrısıyla CQRS, repository, audit/soft delete, JWT, RabbitMQ, loglama ve health check |
| **Üretici** | `baseforge` CLI + tarayıcı tabanlı **Designer** | Entity'leri YAML ile (veya görsel olarak) tarif edip eksiksiz, derlenebilir, Docker'a hazır bir servis almak |

## Neden?

Her mikroservis aynı altyapıya ihtiyaç duyar: temiz bir katmanlama, CQRS pipeline'ı, repository'ler, audit alanları, soft delete, hata yönetimi, kimlik doğrulama, servisler arası çağrılar, mesajlaşma, loglama, health check, Docker dosyaları… Bunları elle yazmak yavaştır ve her servis bir öncekinden biraz farklılaşır.

BaseForge bu kararları **bir kez** verir, *neden* verildiğini [mimari kararlar](/tr/architecture) dokümanında açıklar ve bunları kütüphane + üretici olarak sunar:

- **Framework değil, kütüphane.** Kontrol tamamen sende — her davranış override edilebilir. Sadece yazmak zorunda kalmazsın.
- **Tek satırlık entegrasyon.** `builder.Services.AddBaseForge(...)` her şeyi bağlar.
- **Üretilen kod düz koddur.** Çalışma zamanı sihri yok: üretici okuyabileceğin, debug edebileceğin ve düzenleyebileceğin sıradan controller'lar, handler'lar, DTO'lar, EF Core entity'leri ve proto dosyaları üretir.

## Öne çıkanlar

- MediatR üzerinde **Clean Architecture + CQRS**, `ICommand` / `IQuery` / handler sözleşmeleri.
- **EF Core 10 + Dapper** — yazma için LINQ ve change tracking, ağır okumalar için ham SQL; aynı bağlantı ve transaction paylaşılır.
- **Merkezi Identity servisi** (OpenIddict + ASP.NET Identity) — OAuth2/OIDC, sosyal girişler, roller, sahiplik kuralları, kullanıcı profil alanları.
- Servisler arası **gRPC** — proto'lar, istemciler ve sunucular otomatik üretilir.
- **Transactional outbox**, inbox idempotency ve dead-letter kuyruklarıyla **RabbitMQ** pub/sub.
- **İzlenebilirlik** — Serilog + Grafana Loki, HTTP → gRPC → RabbitMQ boyunca tek correlation id, her yerde `/health`.
- **Görsel Designer** — entity'leri, ilişkileri, erişim kurallarını ve Identity'yi tarayıcıda tasarla, canlı ER diyagramını gör, tek tıkla üret, derle ve Docker ile çalıştır.
- **Multi-tenancy**, **append-only entity'ler**, **JSONB alanlar**, **enum'lar**, **liste filtreleri**, **YARP gateway** ve daha fazlası — hepsi spec'ten.

## Teknoloji yığını

| Katman | Teknoloji |
| --- | --- |
| Framework | .NET 10 |
| Mimari | Clean Architecture + CQRS + Repository Pattern |
| CQRS | MediatR |
| Servisler arası (senkron) | gRPC |
| Servisler arası (asenkron) | RabbitMQ |
| Auth | Merkezi Identity Service + JWT / OAuth2 |
| Veritabanı | PostgreSQL (her mikroservisin kendi veritabanı) |
| ORM / Veri erişimi | EF Core 10 (yazma + tracking) + Dapper (ham SQL okuma) |
| Container | Docker + Docker Compose |
| Dağıtım | Public NuGet (nuget.org) |

## Paketler

| Paket | Açıklama |
| --- | --- |
| [`BaseForge.Core`](https://www.nuget.org/packages/BaseForge.Core) | Yalnızca interface ve entity base'leri — dış bağımlılık yok |
| [`BaseForge.Infrastructure`](https://www.nuget.org/packages/BaseForge.Infrastructure) | EF Core repository'leri, Dapper sorgu yardımcıları, DbContext base, RabbitMQ event bus, outbox/inbox |
| [`BaseForge.API`](https://www.nuget.org/packages/BaseForge.API) | Controller base, middleware, `AddBaseForge()` / `UseBaseForge()`, JWT, loglama, health check |
| [`BaseForge.Tools`](https://www.nuget.org/packages/BaseForge.Tools) | Geliştirici araçları: EF Core modelinden DBML ER diyagramı |
| [`BaseForge.CodeGen`](https://www.nuget.org/packages/BaseForge.CodeGen) | `baseforge` .NET tool'u: kod üretici + Designer |

::: tip Durum
BaseForge **beta** aşamasında (güncel: `0.6.2-beta`). API oturuyor, ancak minor sürümler arasında kırıcı değişiklik olabilir — bunlar her zaman [sürüm notlarında](/tr/releases/v0.6.2-beta) listelenir.
:::

Hazır mısın? [Başlarken](/tr/guide/getting-started) sayfasına geç.
