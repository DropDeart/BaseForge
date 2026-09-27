# Başlarken

BaseForge'u iki şekilde kullanabilirsin. Çoğu kişi **üretici** ile başlar — kütüphaneyi zaten kullanan eksiksiz bir servis üretir. İstersen **kütüphaneyi** mevcut bir projeye elle de ekleyebilirsin.

## Önkoşullar

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (üretilen servisleri ve PostgreSQL / RabbitMQ / Loki'yi çalıştırmak için)

## Seçenek A — Servis üret (önerilen)

### 1. CLI'yı kur

```bash
dotnet tool install -g BaseForge.CodeGen --prerelease
```

Bu, `baseforge` komutunu kurar. Sonradan güncellemek için: `dotnet tool update -g BaseForge.CodeGen --prerelease`.

### 2. Designer'ı aç

Bir workspace klasörü oluştur (veya içine gir) — üreteceğin her servis bu klasörde yan yana alt klasörler olarak durur:

```bash
mkdir my-platform && cd my-platform
baseforge new orders
```

Tarayıcında `http://localhost:3500` adresinde **Designer** açılır. Entity ve alan ekle, ilişkileri çiz, erişim kurallarını seç — ER diyagramı canlı güncellenir. Tur için [Designer rehberine](/tr/guide/designer) bak.

### 3. Üret, derle, çalıştır

**Üret + Derle**'ye bas. BaseForge `orders/spec.yaml` dosyasını ve tüm kaynak kodu yazar, ardından `dotnet build` çalıştırıp sonucu gösterir. Tüm stack'i Docker Compose ile başlatmak için **Çalıştır**'a bas:

```bash
# ya da terminalden
cd orders
docker compose up --build -d
```

Üretilen REST API'yi incelemek için `http://localhost:8080/scalar/v1` adresini aç.

### 4. Geliştirmeye devam et

Entity eklemek veya kuralları değiştirmek için istediğin zaman geri dön — Designer mevcut spec'i yükler:

```bash
baseforge update orders
```

::: tip YAML mı tercih edersin?
Designer'ın yaptığı her şey düz bir `spec.yaml` dosyasında saklanır. Elle yazıp terminalden `baseforge new-service --spec orders.yaml` ile üretebilirsin. Bkz. [Servis Spec'i](/tr/guide/service-spec).
:::

## Seçenek B — Kütüphaneyi doğrudan kullan

Paketleri bir ASP.NET Core projesine ekle:

```bash
dotnet add package BaseForge.API --prerelease
dotnet add package BaseForge.Infrastructure --prerelease
dotnet add package BaseForge.Core --prerelease
```

`BaseForgeDbContext`'ten türeyen bir `DbContext` oluştur, ardından `Program.cs`'te BaseForge'u bağla:

```csharp
using BaseForge.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Yapılandırılmış loglama (konsol + Serilog:LokiUrl doluysa Grafana Loki)
builder.AddBaseForgeLogging("orders");

builder.Services.AddControllers();
builder.Services.AddBaseForge(options =>
{
    options.UsePostgreSQL<OrdersDbContext>(
        builder.Configuration.GetConnectionString("Default")!);
    options.EnableCQRS(typeof(Program).Assembly);
    options.EnableAuditLog();

    // Opsiyonel yapı taşları
    options.EnableJwt(jwt =>
    {
        jwt.Authority = "http://localhost:5090";
        jwt.Audience = "baseforge-api";
    });
    options.EnableRabbitMq(mq => mq.Host = "localhost");
});

var app = builder.Build();
app.UseBaseForge();   // correlation id, hata yönetimi, istek logu, /health
app.MapControllers();
app.Run();
```

Artık entity'lerini `BaseEntity`'den türetebilir, komut ve sorgularını `ICommand` / `IQuery` ile yazabilir, controller'larını `BaseController`'dan miras alabilirsin. Audit alanları, soft delete, exception → HTTP durum kodu eşlemesi ve correlation id senin yerine yönetilir.

## Sırada ne var?

- [Designer](/tr/guide/designer) — görsel editör turu
- [Servis Spec'i](/tr/guide/service-spec) — tüm YAML seçenekleri
- [Identity](/tr/guide/identity) — merkezi kimlik doğrulama, sosyal girişler, roller
- [Mimari](/tr/architecture) — BaseForge'un arkasındaki kararlar ve gerekçeleri
