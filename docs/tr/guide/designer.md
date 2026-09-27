# Designer

Designer, BaseForge spec'leri için tarayıcı tabanlı bir editördür. `baseforge` .NET tool'unun **içinde** gelir — ek bir kurulum gerekmez.

```bash
baseforge new orders           # yeni bir servis başlat
baseforge update orders        # mevcut orders/spec.yaml'ı (ve varsa identity/auth.yaml'ı) aç
baseforge new orders --port 4000 --no-browser
```

`http://localhost:3500` adresinde açılır (port doluysa sıradaki boş port kullanılır). Üret dediğinde her şey diske düz YAML olarak kaydedilir — Designer bir kolaylıktır, bağımlılık değil.

## Yerleşim

Soldaki ikon rayı üç görünüm arasında geçiş yapar; ayrıca dil seçici ve kapatma düğmesi de oradadır:

| Ray | Görünüm | Orada ne yaparsın |
| --- | --- | --- |
| **S** | Servis | Servis ayarları, entity'ler, alanlar, ilişkiler, dış referanslar, erişim kuralları, liste filtreleri |
| **I** | Identity | Merkezi auth servisi: sağlayıcılar, seed admin, roller, kayıt, kullanıcı profil alanları |
| **E** | ER diyagramı | Güncel spec'in canlı diyagramı; DBML kopyala veya dbdiagram.io'da aç |
| **EN / TR** | Dil | Arayüz dilini değiştir (tarayıcında hatırlanır) |
| **⏻** | Kapat | Designer'ı, portunu ve başlattığı container'ları durdurur |

## Servis görünümü

### Servis ayarları

- **Servis adı / Veritabanı** — proje adı, namespace ve PostgreSQL veritabanı için kullanılır.
- **Merkez Identity'ye JWT ile bağla** — JWT doğrulamasını (`authority`, `audience`) ve `[Authorize]` korumasını açar. Açıkken **varsayılan erişim** ve **süper roller** de gelir (bkz. [Identity → Yetkilendirme](/tr/guide/identity#yetkilendirme)).
- **Multi-tenancy** — her entity'ye `TenantId` ve kiracı izolasyonu ekler.
- **REST / gRPC / Postgres portları** — Docker host portları. Designer workspace kaydını okuyup daha önce ürettiğin servislerle çakışmayan portları önceden doldurur.
- **Outbox ayarları** — event yayınlayan servisler için max retry ve saklama süresi.

### Entity'ler ve alanlar

Entity'leri soldan ekle. Her biri için:

- **Alan** ekleyip açılır listeden tip seç. ⚙ düğmesi `nullable`, `maxLength`, `default`, enum değerleri ve `counter` işaretini gösterir.
- **Sayfalama**, **Sıralama**, **Arama** ve **Append-only** aç/kapat.
- Bir **sahip alanı** ve **action başına erişim kuralı** seç (`list`, `getById`, `create`, `update`, `delete`).
- **Liste filtreleri** (eşitlik sorgu parametreleri) ve bir **görünürlük filtresi** seç (ör. taslakları admin ve yazar dışında herkesten gizle).
- Aynı servisteki entity'lere **ilişki** çiz (`one-to-many`, `many-to-one`, `one-to-one`, opsiyonel olarak nullable).
- Diğer servislerdeki entity'lere **dış referans** ekle (`servis/Entity`, ID olarak saklanır, gRPC ile çözülür).

Audit alanları — `Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, soft delete — `BaseEntity`'den gelir; bunları asla elle eklemezsin.

## Identity görünümü

Diğer tüm servislerin güvendiği merkezi kimlik doğrulama servisini yapılandır:

- **Merkez ayarlar** — servis adı, veritabanı, issuer ve portlar.
- **Sağlayıcılar** — Google, GitHub, Microsoft, Facebook (Apple YAML ile). Birini açıp client id/secret'ını yapıştır. Secret'lar commit edilen dosyalara değil `.env`'e yazılır.
- **Seed admin** — ilk admin hesabı.
- **Roller** — `Admin` ve `User` her zaman vardır; kendi rollerini ekle (ör. `Editor`, `SuperAdmin`).
- **Kendi kendine kayıt** — varsayılan kapalı; açıkken yeni kullanıcılara verilecek rolü seç.
- **Profil alanları** — kullanıcıya eklenen, kullanıcının veya yalnızca adminin düzenleyebildiği, istenirse token'a eklenen domain alanları.

## Üret, derle, çalıştır

**Üret + Derle** spec'i ve tüm kaynak kodu yazar, `dotnet build` çalıştırır ve dosya listesini ve derleme sonucunu gösterir. Yakında bir `.slnx`/`.sln` bulunursa yeni projeyi ona ekleyebilirsin.

Başarılı bir derlemeden sonra:

- **Çalıştır** servisi `docker compose up --build -d --wait` ile başlatır ve seni API referansına (Scalar) veya Identity için giriş sayfasına yönlendirir.
- **Durdur** `docker compose down` çalıştırır.

## ER diyagramı görünümü

Diyagram spec'ten canlı çizilir: düz bağlantılar servis içi foreign key'lerdir, kesikli kutular dış servis referanslarıdır (FK yok, sadece ID). **DBML kopyala** panona bir [DBML](https://dbml.dbdiagram.io/) sürümü koyar; **dbdiagram.io'da aç** aynısını yapıp siteyi açar.

::: tip Kapatma
Designer'ı **⏻** veya **Kapat** düğmesiyle kapat. Sadece sekmeyi kapatırsan Designer nabzın kesildiğini fark eder ve birkaç dakika sonra kendini kapatır.
:::
