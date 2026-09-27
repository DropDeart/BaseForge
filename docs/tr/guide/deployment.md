# Production'a Yayın

Bu rehber tipik bir BaseForge platformunu — bir **Identity** servisi ve bir veya daha fazla iş servisi — makinenden, otomatik HTTPS'li bir reverse proxy arkasında Docker çalıştıran bir Linux sunucuya taşır.

Örneklerde `example.com`, bir Identity servisi ve bir `blog` servisi kullanılıyor. Kendi değerlerinle değiştir.

## 1. Neye ihtiyacın var

| Bileşen | Zorunlu mu? | Amaç |
| --- | --- | --- |
| **Identity** | Evet | Giriş, OAuth2/OIDC, tüm servisler için JWT |
| **Servislerin** | Evet | Spec'lerinden üretilir |
| **PostgreSQL** | Evet | Tek instance, servis başına bir veritabanı |
| **RabbitMQ** | `publishes` / `subscribes` kullanıyorsan | Asenkron event'ler |
| **Reverse proxy (Caddy)** | Evet | Tek HTTPS girişi, otomatik sertifika, domain bazlı yönlendirme |
| **Gateway servisi** | Genelde hayır | Yalnızca bir frontend birçok servise tek origin'den erişmesi gerektiğinde — bkz. [Mimari §5.8](/tr/architecture#_5-8-gateway-bff-—-yarp-tabanlı-reverse-proxy) |

::: tip Muhtemelen gateway'e ihtiyacın yok
Birkaç servis için bir **reverse proxy** (TLS sonlandırma + yönlendirme, iş mantığı yok) yeterlidir. BFF/gateway servisi ancak tarayıcının birden fazla servisten birleştirilmiş veriye tek origin'den ihtiyaç duyduğunda değer.
:::

## 2. Mimari

```
                         example.com (tarayıcı)
                                  │
                                  ▼
               ┌──────────────────────────────────┐
               │   Caddy (reverse proxy + TLS)    │
               │   example.com          → frontend │
               │   identity.example.com → Identity │
               │   blog.example.com     → Blog     │
               └───────┬──────────────┬───────────┘
                       │              │
               ┌───────▼─────┐  ┌─────▼──────┐
               │  Identity   │◄─┤    Blog    │  gRPC (identity/User)
               └──────┬──────┘  └─────┬──────┘
                      │  RabbitMQ event'leri
               ┌──────▼───────────────▼──────┐
               │  PostgreSQL + RabbitMQ      │
               └─────────────────────────────┘
```

Frontend, Identity'ye authorization code + PKCE ile giriş yapar ve token'ı servislerine `Authorization: Bearer …` olarak gönderir. Servisler iç Docker ağı üzerinden gRPC/RabbitMQ ile konuşur — tarayıcı bu trafiği hiç görmez.

## 3. Sunucuyu hazırla

Docker kurulu herhangi bir Linux VPS (Ubuntu 22.04+ önerilir):

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # ardından yeniden giriş yap
```

`example.com`, `identity.example.com` ve `blog.example.com` için DNS `A` kayıtlarını sunucuya yönlendir.

## 4. Identity'yi production'a hazırla

1. **Kalıcı bir imza sertifikası.** Olmazsa Identity her açılışta geçici bir anahtar üretir — ve yeniden başlatmada verilmiş tüm token'lar geçersiz olur.

   ```bash
   openssl req -x509 -newkey rsa:2048 -keyout identity.key -out identity.crt -days 3650 -nodes -subj "/CN=identity.example.com"
   openssl pkcs12 -export -out identity-signing.pfx -inkey identity.key -in identity.crt -passout pass:DEGISTIR
   ```

   ```yaml
   # auth.yaml
   signing:
     certificatePath: /app/certs/identity-signing.pfx
   ```

   Parolayı `.env`'e yaz ve `.pfx` dosyasını container'a mount et.

2. **Redirect URI'ler** — gerçek frontend callback'ini (ör. `https://example.com/auth/callback`) `auth.yaml`'daki tarayıcı client'ına ekle.
3. **CORS** — `https://example.com`'u `auth.yaml`'daki ve tarayıcının çağırdığı her servis spec'indeki `corsOrigins`'e ekle.
4. **Secret'lar** — `.env`'de seed admin parolası, sağlayıcı secret'ları ve veritabanı parolaları için güçlü değerler kullan. Asla commit etme.

## 5. Servisleri Identity'ye yönlendir

Üretilen servisler bu ayarları ortamdan okur, kod değişikliği gerekmez:

```yaml
environment:
  Auth__Authority: http://identity:8080     # Docker ağı içindeki adres
  Auth__Issuer: https://identity.example.com/
```

`Auth__Issuer`, Identity'nin kendi issuer'ıyla aynı olmalı; Identity yeniden başlarken isteklerin çalışmaya devam etmesi için yedek olarak kabul edilir.

## 6. Tüm platform için tek compose dosyası

Üretilen her servis kendi izole `docker-compose.yml`'uyla gelir (yerel geliştirme için ideal). Sunucuda bunları tek bir PostgreSQL ile tek dosyada birleştir:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: baseforge
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U baseforge"]
      interval: 10s
      retries: 5

  rabbitmq:
    image: rabbitmq:4-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}

  identity:
    build: ./identity
    env_file: ./identity/.env
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=postgres;Database=identity_db;Username=baseforge;Password=${POSTGRES_PASSWORD}"
    volumes: [./identity/certs:/app/certs:ro]
    depends_on: { postgres: { condition: service_healthy } }

  blog:
    build: ./blog
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=postgres;Database=blog_db;Username=baseforge;Password=${POSTGRES_PASSWORD}"
      RabbitMq__Host: rabbitmq
      Grpc__Identity: http://identity:8081
      Auth__Authority: http://identity:8080
      Auth__Issuer: https://identity.example.com/
    depends_on: [postgres, rabbitmq, identity]

  caddy:
    image: caddy:2-alpine
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data

volumes:
  pgdata:
  caddy-data:
```

Servis veritabanlarını bir kez oluştur:

```bash
docker compose up -d postgres
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE identity_db;"
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE blog_db;"
```

Servisler şemalarını açılışta oluşturur.

## 7. Otomatik HTTPS'li reverse proxy

```
# Caddyfile
identity.example.com {
    reverse_proxy identity:8080
}

blog.example.com {
    reverse_proxy blog:8080
}
```

Caddy ilk istekte Let's Encrypt sertifikalarını otomatik alır. Üretilen servisler `X-Forwarded-*` header'larını zaten dikkate aldığından discovery dokümanlarındaki URL'ler `https` ve gerçek domain'ini kullanır.

## 8. Yayına al

```bash
docker compose up --build -d
curl -s https://identity.example.com/.well-known/openid-configuration | head -c 200
curl -s -o /dev/null -w "%{http_code}\n" https://blog.example.com/api/posts   # token yoksa 401
```

## Kontrol listesi

- [ ] DNS kayıtları sunucuya yönlü
- [ ] Identity'nin kalıcı bir `.pfx` imza sertifikası var
- [ ] Tarayıcı client redirect URI'leri gerçek domain'i kullanıyor
- [ ] `corsOrigins` frontend origin'ini içeriyor
- [ ] Servislerde `Auth__Authority` (iç adres) ve `Auth__Issuer` (public issuer) var
- [ ] Her servis için bir veritabanı oluşturuldu
- [ ] `.env` dosyaları güçlü secret'lar içeriyor ve commit edilmedi
- [ ] Caddy her subdomain için sertifika aldı
- [ ] Her servis için `/health` `Healthy` dönüyor
