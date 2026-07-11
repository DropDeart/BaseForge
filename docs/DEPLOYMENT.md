# enterfea.com — Blog + Yorum + Beğeni: Servis Envanteri ve Yayına Alma Rehberi

Bu doküman, enterfea.com'a yorum/beğeni özelliği eklemek için hangi BaseForge servislerinin
gerektiğini, merkez bir API'ye ihtiyaç olup olmadığını ve kendi sunucunuzda sıfırdan yayına
almayı adım adım anlatır. Claude'un yardımı olmadan takip edilebilecek şekilde yazılmıştır —
her adımda çalıştırılacak gerçek komutlar var.

**Önkoşul bilgi:** Bu oturumda Identity (merkezi auth) ve Blog (Post/Comment/Like, JWT + gRPC +
RabbitMQ event pub/sub) zaten geliştirilip yerelde uçtan uca test edildi. Burada anlatılan,
bunları **gerçek sunucunuza taşımak**.

---

## 1. Hangi servislere ihtiyacınız var?

| Servis | Zorunlu mu? | Ne işe yarar | Bu oturumdaki durumu |
| --- | --- | --- | --- |
| **Identity** | Evet | Merkezi login/register, OAuth2 (authorization_code+PKCE ve password grant), OpenIddict, tüm servislerin JWT doğrulaması, gRPC ile `User` sorgulama | Hazır: `services/BaseForge.Identity` |
| **Blog** | Evet | Post/Comment/Like CRUD, `[Authorize]` ile JWT korumalı, `identity/User`'a gRPC, Comment/Like oluşunca RabbitMQ'ya olay yayınlar | Hazır: `samples/blog.yaml` → `baseforge new-service` ile üretilir |
| **PostgreSQL** | Evet | `identity_db` + `blog_db` — **tek instance, iki veritabanı** önerilir (aşağıda §4.5) | Kökteki `docker-compose.yml`'da tanımlı |
| **RabbitMQ** | Opsiyonel ama önerilir | "Yorum/beğeni yapıldığında yazara bildir" özelliği bunsuz çalışmaz | Kökteki `docker-compose.yml`'da tanımlı, bu oturumda canlı test edildi |
| **Merkez API / Gateway** | **Hayır** | — | Bkz. §2 |
| **Reverse proxy (Caddy)** | Evet (production) | Tek HTTPS girişi, otomatik TLS sertifikası, domain bazlı yönlendirme | Yeni eklenecek (§4.6) |
| **Frontend (enterfea.com'un kendisi)** | Zaten sizde | Login/kayıt, yorum formu, beğeni butonu — Identity+Blog API'lerini çağırır | Sizin elinizde; entegrasyon sözleşmesi §4.8 |

## 2. Merkez API'ye ihtiyacınız var mı?

**Hayır.** Şu an yalnızca iki backend servisiniz var (Identity, Blog). Ayrı bir "gateway servisi"
(iş mantığı içeren, auth/aggregation/rate-limit yapan bir ara katman) bu ölçekte gereksiz karmaşıklık
katar — bakımı gereken üçüncü bir .NET servisi daha demektir.

Gerçekte ihtiyacınız olan şey bir gateway değil, bir **reverse proxy**: tarayıcıya tek bir HTTPS
adresi (enterfea.com) sunan, arkada hangi isteği hangi servise ileteceğini bilen basit bir yönlendirici
(TLS sonlandırma + domain bazlı routing, iş mantığı yok). Bunun için **Caddy** öneriyorum (nginx'e göre
otomatik Let's Encrypt sertifikası alması ve config'inin çok daha kısa olması nedeniyle). Kurulumu §4.6'da.

Servis sayınız ileride 4-5'i geçerse (örn. Orders, Products gibi) ve tarayıcının tek bir "aggregate"
uçtan (örn. "ana sayfa verisi = 3 farklı servisten birleştirilmiş veri") beslenmesi gerekirse, o zaman
gerçek bir BFF/gateway servisi gündeme gelir. Şu an için gerekmiyor.

## 3. Mimari (özet)

```
                              enterfea.com (tarayıcı)
                                       │
                                       ▼
                    ┌──────────────────────────────────┐
                    │   Caddy (reverse proxy + TLS)     │
                    │   - enterfea.com          → frontend
                    │   - identity.enterfea.com → Identity
                    │   - blog.enterfea.com     → Blog
                    └───────┬──────────────┬───────────┘
                            │              │
                    ┌───────▼─────┐  ┌─────▼──────┐
                    │  Identity   │  │    Blog    │
                    │  (8080/8081)│◄─┤ (8080/8081)│  gRPC (identity/User)
                    └──────┬──────┘  └─────┬──────┘
                           │               │
                           │  publish/subscribe (RabbitMQ)
                           │               │
                    ┌──────▼───────────────▼──────┐
                    │  Postgres (identity_db,      │
                    │  blog_db) + RabbitMQ         │
                    └──────────────────────────────┘
```

Frontend (enterfea.com'un kendisi) tarayıcıda çalışır, Identity'e authorization_code+PKCE ile login
olur, aldığı token'ı Blog'a `Authorization: Bearer` ile gönderir. İki servis birbirine doğrudan
(gRPC/RabbitMQ) container ağı üzerinden konuşur — tarayıcı bunu hiç görmez.

## 4. Adım adım kurulum

### 4.1 Sunucu hazırlığı

Herhangi bir Linux VPS (Ubuntu 22.04+ önerilir), Docker + Docker Compose kurulu:

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # yeniden login gerekir
```

DNS: `enterfea.com` (frontend), `identity.enterfea.com`, `blog.enterfea.com` — üçünü de sunucunuzun
IP'sine A kaydı olarak yönlendirin.

### 4.2 Identity'yi production'a hazırlama

Yerelde çalışan `identity` projenizin (`auth.yaml`/`appsettings.json`) şu değişiklikleri gerekir:

1. **Kalıcı imzalama sertifikası** — şu an sertifika verilmezse Identity her yeniden başlatmada
   geçici (ephemeral) bir RSA anahtarı üretir; bu, container her restart olduğunda **önceden verilmiş
   tüm token'ların geçersiz hale gelmesi** demektir (production'da kabul edilemez). Bir `.pfx` üretin:
   ```bash
   openssl req -x509 -newkey rsa:2048 -keyout identity.key -out identity.crt -days 3650 -nodes -subj "/CN=identity.enterfea.com"
   openssl pkcs12 -export -out identity-signing.pfx -inkey identity.key -in identity.crt -passout pass:DEGISTIRIN
   ```
   `auth.yaml`'a ekleyin:
   ```yaml
   signing:
     certificatePath: /app/certs/identity-signing.pfx
     certificatePassword: DEGISTIRIN   # prod'da .env'e taşıyın
   ```
   Bu dosyayı bir Docker volume ile container'a mount edin (aşağıdaki compose örneğinde `./certs`).

2. **`blog-web` client'ının redirect URI'sini güncelleyin** (`appsettings.json` → `Auth:Clients`):
   ```json
   { "ClientId": "blog-web", "Public": true, "Grants": ["authorization_code", "refresh_token"],
     "Scopes": ["api"], "RedirectUris": ["https://enterfea.com/auth/callback"] }
   ```
   (`http://localhost:3000/callback` yerine gerçek domain'iniz — birden fazla URI ekleyebilirsiniz.)

3. **`Issuer`'ı** docker network içindeki servis adına sabitleyin (aşağıdaki compose örneğinde
   `http://identity:8080/` — dışarıdan erişilen `https://identity.enterfea.com` adresiyle KARIŞTIRMAYIN,
   ikisi farklı amaçlar için: Issuer token içindeki `iss` claim'i, dışarıdaki domain ise sadece
   tarayıcının/reverse proxy'nin eriştiği adres).

4. **Seed admin şifresini** güçlü bir değerle `.env`'e yazın (`Auth__SeedAdmin__Password`).

5. **`RequireHttpsMetadata`** — Caddy TLS'i sonlandırıp container'a düz HTTP ile ilettiği için
   container-içi ayarlarda `false` kalabilir (bu, Blog'un Identity'e gRPC/HTTP ile container ağı
   üzerinden erişimi için zaten gerekliydi).

### 4.3 Blog'u üretme

```bash
cd BaseForge
baseforge new-service --spec samples/blog.yaml --output ../blog
```

Üretilen `Program.cs`'te (veya CLI sizden auth soracak — evet deyip) şunları prod değerleriyle
güncelleyin:

```csharp
options.EnableJwt(jwt =>
{
    jwt.Authority = "http://identity:8080";     // container ağı içi adres (Caddy önü değil!)
    jwt.Audience = "baseforge-api";
    jwt.RequireHttpsMetadata = false;           // TLS Caddy'de sonlanıyor
});
```

`appsettings.json`'daki `RabbitMq:Host` zaten `host.docker.internal` — eğer Blog'u da diğer
servislerle **aynı** docker-compose ağına alıyorsanız (bu rehberde öyle, §4.5), bunu `rabbitmq`
(servis adı) olarak değiştirin — `host.docker.internal` yalnızca izole/ayrı compose'lar arasında
gereklidir.

### 4.4 CORS ekleyin (Identity + Blog — şu an ikisinde de yok)

Frontend'iniz (enterfea.com) Identity ve Blog'dan **farklı bir origin/subdomain** olduğu için
tarayıcı CORS gerektirir. Şu an BaseForge'un ürettiği hiçbir serviste CORS middleware'i yok —
her iki servisin `Program.cs`'ine elle ekleyin:

```csharp
// builder.Services.AddBaseForge(...) çağrısından ÖNCE:
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("https://enterfea.com")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// app.UseBaseForge()'dan ÖNCE (Identity'de app.UseAuthentication()'dan önce):
app.UseCors("Frontend");
```

### 4.5 Docker Compose — tek dosyada konsolide edin

Şu an her üretilen servis **kendi izole** `docker-compose.yml`'unu getirir (kendi Postgres'iyle) —
bu yerel test için uygun ama gerçek sunucuda 2 ayrı Postgres çalıştırmak gereksiz. Bunun yerine
kökteki `docker-compose.yml`'u temel alıp tek bir dosyada birleştirin:

```yaml
# /opt/enterfea/docker-compose.yml
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: baseforge
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: baseforge   # yalnızca ilk açılışta oluşur; identity_db/blog_db'yi §4.5.1'de elle açıyoruz
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U baseforge"]
      interval: 10s
      retries: 5

  rabbitmq:
    image: rabbitmq:4-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 15s
      retries: 5

  identity:
    build: ./identity
    env_file: ./identity/.env
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=postgres;Port=5432;Database=identity_db;Username=baseforge;Password=${POSTGRES_PASSWORD}"
    volumes:
      - ./identity/certs:/app/certs:ro
      - identity-avatars:/app/wwwroot/uploads
    depends_on:
      postgres: { condition: service_healthy }
    expose: ["8080", "8081"]   # Caddy'ye açık, host'a değil

  blog:
    build: ./blog
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=postgres;Port=5432;Database=blog_db;Username=baseforge;Password=${POSTGRES_PASSWORD}"
      RabbitMq__Host: rabbitmq
      Grpc__Identity: "http://identity:8081"
    depends_on:
      postgres: { condition: service_healthy }
      rabbitmq: { condition: service_healthy }
      identity: { condition: service_started }
    expose: ["8080", "8081"]

  caddy:
    image: caddy:2-alpine
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
    depends_on: [identity, blog]

volumes:
  pgdata:
  identity-avatars:
  caddy-data:
```

`.env` (bu dizinde, git'e commit etmeyin):
```
POSTGRES_PASSWORD=guclu_bir_sifre
RABBITMQ_USER=baseforge
RABBITMQ_PASSWORD=guclu_bir_sifre
```

#### 4.5.1 İki veritabanını oluşturma (tek seferlik)

Postgres image'ı yalnızca `POSTGRES_DB` env'inde yazan **tek** veritabanını ilk açılışta oluşturur.
İki ayrı DB (`identity_db`, `blog_db`) için ilk `docker compose up -d postgres` sonrası bir kere:

```bash
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE identity_db;"
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE blog_db;"
```

### 4.6 Reverse proxy (Caddy) — otomatik TLS

`/opt/enterfea/Caddyfile`:

```
identity.enterfea.com {
    reverse_proxy identity:8080
}

blog.enterfea.com {
    reverse_proxy blog:8080
}

enterfea.com {
    reverse_proxy frontend:PORT   # frontend'iniz de container'daysa; değilse bu bloğu kaldırıp
                                   # mevcut web sunucunuzu (WordPress vb.) olduğu gibi bırakın
}
```

Caddy, DNS doğru yönlendirilmişse ilk istekte otomatik Let's Encrypt sertifikası alır — elle
sertifika/renewal işi yoktur.

### 4.7 Ayağa kaldırma

```bash
cd /opt/enterfea
docker compose up --build -d
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE identity_db;"
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE blog_db;"
docker compose restart identity blog   # DB'ler artık var, EnsureCreated şemaları kursun
```

Doğrulama:
```bash
curl -s https://identity.enterfea.com/.well-known/openid-configuration | head -c 200
curl -s -o /dev/null -w "%{http_code}\n" https://blog.enterfea.com/api/posts   # 401 bekleniyor (token yok)
```

### 4.8 Frontend entegrasyonu (enterfea.com'un kendi kodu)

Bu kısmı biz yapamayız — enterfea.com'un kendi kod tabanı sizde. Uyması gereken sözleşme (bu
oturumda zaten kurulup test edildi):

1. **Login:** `https://identity.enterfea.com/connect/authorize` adresine `client_id=blog-web`,
   `response_type=code`, `scope=api offline_access`, PKCE `code_challenge` ile yönlendirin.
2. **Callback:** `/auth/callback`'te gelen `code`'u `https://identity.enterfea.com/connect/token`'a
   `grant_type=authorization_code` + `code_verifier` ile POST edip `access_token`/`refresh_token` alın.
3. **API çağrıları:** `https://blog.enterfea.com/api/comments` (vb.) uçlarına
   `Authorization: Bearer <access_token>` header'ıyla istek atın.

Tam kod örnekleri (PKCE üretimi, authorize URL, token exchange) bu konuşmanın önceki bir adımında
verildi — orayı referans alın.

### 4.9 BaseForge NuGet paketlerini yayınlama (opsiyonel)

`Blog.csproj`'daki `PackageReference Include="BaseForge.API"` sürümü, RabbitMQ desteğinin olduğu
bir NuGet sürümüne işaret etmeli. İki seçenek:

- **(A) Gerçek nuget.org release'i** (önerilen, kalıcı çözüm): `Directory.Build.props`'taki
  `<Version>` değerini artırın (örn. `0.3.0-beta`), GitHub'da bir Release oluşturun
  (`gh release create v0.3.0-beta --generate-notes`) — `.github/workflows/publish.yml` otomatik
  tetiklenip build+test+pack+nuget.org push yapar (Trusted Publishing/OIDC zaten kurulu).
- **(B) Yerel/private feed** (hızlı iterasyon için): `dotnet pack src/BaseForge.{Core,Infrastructure,API}
  -o ./localfeed -p:Version=0.3.0-local`, sonra üretilen servisin klasörüne bir `NuGet.Config` ekleyip
  bu klasörü kaynak gösterin, `PackageReference` sürümünü `0.3.0-local` yapın. (Bu oturumda tam bunu
  yaparak doğrulama yaptık.)

Gerçek yayına almadan önce (A)'yı seçip nuget.org'a normal bir sürüm çıkarmanızı öneririm — (B)
yalnızca geliştirme/test için.

## 5. Yayına alma kontrol listesi

- [ ] DNS: `enterfea.com`, `identity.enterfea.com`, `blog.enterfea.com` sunucu IP'sine yönlü
- [ ] Identity: kalıcı `.pfx` imzalama sertifikası (§4.2.1)
- [ ] Identity: `blog-web` redirect URI gerçek domain'e güncel (§4.2.2)
- [ ] Identity + Blog: CORS `https://enterfea.com`'a izinli (§4.4)
- [ ] Blog: `jwt.Authority` container-içi `http://identity:8080` (dışarıdaki domain DEĞİL)
- [ ] Postgres: `identity_db` + `blog_db` ikisi de oluşturuldu (§4.5.1)
- [ ] `.env`: güçlü, gerçek şifreler (Postgres, RabbitMQ, seed admin) — repo'ya commit edilmedi
- [ ] Caddy ayakta, üç subdomain için sertifika alındı
- [ ] `curl https://blog.enterfea.com/api/posts` → token'sız 401, geçerli token'la 200/beklenen veri
- [ ] BaseForge NuGet paketleri (RabbitMQ destekli sürüm) yayınlandı veya yerel feed ile çözüldü (§4.9)

## 6. Bilinen sınırlamalar (bu oturumda bilinçli ertelendi)

- CORS, gRPC JWT propagasyonu, RabbitMQ DLQ/retry — `docs/ARCH.md` §5.1/§5.2'de dokümante edilen
  v1 sınırlamaları; production'a göre ihtiyaç oldukça genişletin.
- Reverse proxy config'i (Caddyfile) burada minimal tutuldu; rate limiting, gerçek health check
  entegrasyonu, log toplama (örn. Loki/ELK) kapsam dışı bırakıldı — ölçek büyüdükçe eklenir.
