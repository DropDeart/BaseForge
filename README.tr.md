# BaseForge

**.NET mikroservislerini spec'ten üret.**

[![NuGet](https://img.shields.io/nuget/vpre/BaseForge.API?label=NuGet&color=0f9f7a)](https://www.nuget.org/packages?q=BaseForge)
[![.NET 10](https://img.shields.io/badge/.NET-10-512bd4)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-dropdeart.github.io%2FBaseForge-0f9f7a)](https://dropdeart.github.io/BaseForge/tr/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

BaseForge, .NET 10 mikroservisleri için opinionated bir **temel kütüphane** ve **görsel kod üreticidir**. Entity'lerini YAML ile tarif et — ya da tarayıcı tabanlı Designer'da tıklayarak oluştur — CQRS, kimlik doğrulama, gRPC, event'ler, loglama ve Docker'ı hazır bağlanmış, temiz ve production'a hazır servisler al.

📖 **Dokümantasyon:** https://dropdeart.github.io/BaseForge/tr/ · 🇬🇧 [English README](README.md)

---

## Neden BaseForge?

Her mikroservis aynı altyapıya ihtiyaç duyar — katmanlama, CQRS, repository'ler, audit alanları, soft delete, hata yönetimi, auth, servisler arası çağrılar, mesajlaşma, loglama, health check, Docker dosyaları. BaseForge bu kararları bir kez verir, *nedenini* belgeler ve şu şekilde sunar:

- **Bir kütüphane** — `builder.Services.AddBaseForge(...)` hepsini tek satırda verir. Framework değil kütüphanedir: her davranış override edilebilir.
- **Bir üretici** — `baseforge` CLI ve Designer'ı bir spec'i sana ait, sade ve okunabilir C#'a (controller, handler, EF Core entity, DTO, proto, Dockerfile) çevirir.

## Özellikler

- 🎨 **Görsel Designer** — entity'ler, ilişkiler, erişim kuralları ve Identity tarayıcıda, canlı ER diyagramıyla; tek tıkla üret, derle ve çalıştır
- 🧱 MediatR üzerinde **Clean Architecture + CQRS**, repository'ler, audit alanları, soft delete
- 🗄️ **EF Core 10 + Dapper** — yazma için LINQ, ağır okumalar için ham SQL, tek paylaşılan bağlantı
- 🔐 **Merkezi Identity** (OpenIddict + ASP.NET Identity) — OAuth2/OIDC, sosyal girişler, roller, sahiplik kuralları, kullanıcı profil alanları
- 🔌 Servisler arası **gRPC** — üretilen proto'lar, istemciler ve sunucular
- 📨 Transactional outbox, inbox idempotency ve dead-letter kuyruklarıyla **RabbitMQ** event'leri
- 🔭 **İzlenebilirlik** — Serilog + Grafana Loki, HTTP → gRPC → RabbitMQ boyunca correlation id, her yerde `/health`
- 🏢 **Multi-tenancy**, **append-only entity'ler**, **enum'lar**, **JSONB**, **liste filtreleri**, **YARP gateway**

## Hızlı başlangıç

```bash
# 1. CLI'yı kur
dotnet tool install -g BaseForge.CodeGen --prerelease

# 2. Bir workspace klasöründe Designer'ı aç
mkdir my-platform && cd my-platform
baseforge new orders
```

Designer `http://localhost:3500` adresinde açılır. Entity ekle, **Üret + Derle**'ye, ardından **Çalıştır**'a bas — servisin `http://localhost:8080/scalar/v1` adresinde ayakta.

YAML mı tercih edersin? Bir spec yazıp terminalden üret:

```yaml
# orders.yaml
service: orders
database: orders_db
entities:
  Order:
    props:
      Status: { type: enum, values: [Draft, Paid], default: Draft }
      Total: decimal
      BuyerId: guid
    ownerField: BuyerId
    access:
      list: [Admin, owner]
    publishes: [created]
```

```bash
baseforge new-service --spec orders.yaml
```

### Kütüphaneyi doğrudan kullanmak

```bash
dotnet add package BaseForge.API --prerelease
```

```csharp
builder.AddBaseForgeLogging("orders");
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL<OrdersDbContext>(connectionString);
    options.EnableCQRS(typeof(Program).Assembly);
    options.EnableAuditLog();
});

var app = builder.Build();
app.UseBaseForge();
```

## Paketler

| Paket | Açıklama |
| --- | --- |
| `BaseForge.Core` | Yalnızca interface ve entity base'leri (dış bağımlılık yok) |
| `BaseForge.Infrastructure` | EF Core repository'leri, Dapper sorgu yardımcıları, DbContext base, RabbitMQ event bus, outbox/inbox |
| `BaseForge.API` | Controller base, middleware, `AddBaseForge()` / `UseBaseForge()`, JWT, loglama, health check |
| `BaseForge.Tools` | Geliştirici araçları: EF Core modelinden DBML ER diyagramı |
| `BaseForge.CodeGen` | `baseforge` .NET tool'u — kod üretici + Designer |

## Dokümantasyon

| | |
| --- | --- |
| [Başlarken](https://dropdeart.github.io/BaseForge/tr/guide/getting-started) | Kur, üret ve ilk servisini çalıştır |
| [Designer](https://dropdeart.github.io/BaseForge/tr/guide/designer) | Görsel editör turu |
| [Servis Spec'i](https://dropdeart.github.io/BaseForge/tr/guide/service-spec) | Tüm YAML seçenekleri |
| [Identity ve Yetkilendirme](https://dropdeart.github.io/BaseForge/tr/guide/identity) | Merkezi auth, roller, sahiplik, profil alanları |
| [Production'a Yayın](https://dropdeart.github.io/BaseForge/tr/guide/deployment) | Docker, reverse proxy, HTTPS, kontrol listesi |
| [Mimari Kararlar](https://dropdeart.github.io/BaseForge/tr/architecture) | Neye karar verildi ve neden |

## Geliştirme

```bash
dotnet build      # tüm solution
dotnet test       # testler
```

Global kurulu `baseforge` tool'unu yerel kaynaktan tazelemek için (bu **resmi bir sürüm değildir**):

```powershell
.\scripts\update-cli.ps1                 # tam build (Designer / Identity arayüzleri dahil)
.\scripts\update-cli.ps1 -SkipWebBuild   # yalnızca CLI / codegen
```

Script, `localpkgs/` altına yerel bir NuGet paketi üretir ve kurulu local sürümün patch numarasını otomatik artırarak (`0.5.1-local` → `0.5.2-local` gibi) global tool'u günceller.

Dokümantasyon sitesi üzerinde çalışmak için:

```bash
cd docs
npm install
npm run dev
```

Resmi sürümler, GitHub'da bir Release oluşturulduğunda `.github/workflows/publish.yml` ile nuget.org'a yayınlanır.

> Bu proje Claude Code ile birlikte geliştirilmektedir. Mimari ve kod kuralları için bkz. [`CLAUDE.md`](CLAUDE.md), [`docs/tr/ARCH.md`](docs/tr/ARCH.md), [`docs/tr/CONVENTIONS.md`](docs/tr/CONVENTIONS.md).

## Lisans

[MIT](LICENSE)
