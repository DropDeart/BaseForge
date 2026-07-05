# BaseForge

.NET 10 tabanlı, mikroservis mimarisine uygun, yeniden kullanılabilir bir **base library**. Yeni backend projeleri için sıfırdan mimari kurmak yerine bu lib extend edilerek kullanılır. Public NuGet paketi olarak yayınlanacaktır.

## Teknoloji Stack

| Katman | Teknoloji |
| --- | --- |
| Framework | .NET 10 |
| Mimari | Clean Architecture + CQRS + Repository Pattern |
| CQRS | MediatR |
| Servisler arası (sync) | gRPC |
| Servisler arası (async) | RabbitMQ |
| Auth | Merkezi Identity Service + JWT / OAuth2 |
| Veritabanı | PostgreSQL (her mikroservis kendi DB'si) |
| ORM / Data Access | EF Core 10 (yazma + tracking) + Dapper (ham SQL okuma) |
| Container | Docker + Docker Compose |
| Paket | Public NuGet (nuget.org) |

## NuGet Paketleri

| Paket | Açıklama |
| --- | --- |
| `BaseForge.Core` | Sadece interface ve entity base'leri (dış bağımlılık yok) |
| `BaseForge.Infrastructure` | Repository implementasyonları (EF Core), Dapper sorgu yardımcıları |
| `BaseForge.API` | Controller base, middleware, DI extension'ları |
| `BaseForge.Tools` | Geliştirme araçları: EF Core model'inden DBML ER diyagramı üretimi |

## Hızlı Başlangıç

```csharp
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL(connectionString);
    options.EnableCQRS();
    options.EnableAuditLog();
});
```

## Designer — Görsel Arayüz (`baseforge new`)

YAML elle yazmak yerine tarayıcı tabanlı bir tasarımcıyla servis üret. Tek komut:

```bash
baseforge new orders
```

`http://localhost:3500` adresinde açılan arayüzde:

- **Entity tasarımı** — alanlar (tip dropdown'ları), aynı servis içi ilişkiler (one-to-many / many-to-one / one-to-one) ve başka servislere dış referanslar (grpc / event).
- **JWT bağlantısı** — üretilen servisi merkez Identity'ye bağla (authority, audience, `[Authorize]`).
- **Merkez Identity** — sosyal sağlayıcı credential'ları (Google, GitHub, Microsoft, Facebook), seed admin.
- **Üret + Derle** — "Generate" ile hem `spec.yaml` hem kod üretilir, ardından otomatik `dotnet build` çalışır ve sonuç (dosya listesi + derleme durumu) arayüzde gösterilir.

Üretilen spec `spec.yaml` olarak diske yazılır; servisi sonradan tekrar açıp düzenleyebilir, version control'e koyabilirsin (mevcut `new-service` / `er` komutlarıyla uyumlu).

> Arayüz React (Vite + TS) ile yazılır ve `dotnet tool`'a gömülür — çalıştırmak için ek kurulum gerekmez. Kaynağı: `src/BaseForge.Designer.Web/`.

## Yapı

```
src/
  BaseForge.Core/            → Entity base'leri, interface'ler, CQRS sözleşmeleri, exception'lar
  BaseForge.Infrastructure/  → GenericRepository (EF Core), Dapper sorgu yardımcıları, DI extension'ları
  BaseForge.API/             → BaseController, middleware, AddBaseForge()
  BaseForge.CodeGen/         → baseforge CLI: kod üretici + Designer web arayüzü (baseforge new)
  BaseForge.Designer.Web/    → Designer React arayüzü (Vite + TypeScript)
tests/
  BaseForge.UnitTests/
  BaseForge.IntegrationTests/
docs/
  ARCH.md                    → Detaylı mimari kararlar
  CONVENTIONS.md             → Kod standartları ve naming kuralları
```

## ER Diyagramı (BaseForge.Tools)

EF Core model'inden DBML üretir; çıktıyı [dbdiagram.io](https://dbdiagram.io)'ya yapıştırarak görselleştir:

```csharp
using BaseForge.Tools;

// Bir DbContext örneğinden (DB bağlantısı gerekmez, sadece model okunur)
string dbml = DbmlGenerator.Generate(dbContext);
File.WriteAllText("docs/er.dbml", dbml);
```

Gerçek tablo/kolon adları, kolon tipleri (provider'a göre, örn. PostgreSQL `uuid`/`timestamptz`), birincil anahtarlar ve FK ilişkileri yansıtılır.

## Geliştirme

```bash
dotnet build      # tüm solution
dotnet test       # testler
```

> Bu proje Claude Code ile birlikte geliştirilmektedir. Mimari ve kod kuralları için bkz. [`CLAUDE.md`](CLAUDE.md), [`docs/ARCH.md`](docs/ARCH.md), [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md).

## Lisans

MIT
