# Identity ve Yetkilendirme

BaseForge tüm platform için **tek bir merkezi Identity servisi** kullanır. Token'ları o verir; diğer tüm servisler bu token'ları **yerel olarak** doğrular (her istekte Identity'ye çağrı yapılmaz).

Identity, **OpenIddict + ASP.NET Identity** üzerine kurulu hazır bir referans implementasyondur. Onu sen yazmazsın — bir `auth.yaml` ile yapılandırıp üretirsin.

## Identity servisi üret

Designer'ın **I** görünümünden ya da terminalden:

```bash
baseforge new-identity --spec auth.yaml --output ./identity
```

Elde ettiklerin:

- OAuth2 / OpenID Connect uçları (`/connect/token`, `/connect/authorize`, discovery, JWKS)
- Ortak bir giriş SPA'sı (giriş, kayıt, profil sayfası, admin paneli)
- Sosyal giriş: Google, GitHub, Microsoft, Facebook, Apple
- Diğer servislerin `identity/User` referanslarını çözebilmesi için bir gRPC `User` servisi
- Workspace'teki tüm servisleri **canlı sağlık durumu** ile listeleyen bir panel

## `auth.yaml`

```yaml
service: identity
database: identity_db
issuer: http://localhost:5090/

scopes:
  - name: api
    resource: baseforge-api

clients:
  - clientId: web                  # tarayıcı uygulaması: authorization_code + PKCE
    public: true
    grants: [authorization_code, refresh_token]
    scopes: [api]
    redirectUris: [http://localhost:3000/callback]
  - clientId: service-worker       # servisten servise
    public: false
    grants: [client_credentials]
    scopes: [api]

seedAdmin:
  email: admin@example.com

roles: [Editor, SuperAdmin]        # Admin ve User her zaman var
registration:
  enabled: false                   # varsayılan kapalı
  defaultRole: User

providers:
  google: { clientId: "..." }      # secret'lar .env'e
  github: { clientId: "..." }

corsOrigins: [http://localhost:3000]
```

::: warning Secret'lar asla YAML'a yazılmaz
Client secret'ları, seed admin parolası ve imza sertifikası parolası git'e girmeyen bir `.env` dosyasına yazılır (ör. `Auth__Providers__Google__ClientSecret`). `appsettings.json` ve kopyalanan `auth.yaml` bunları boş tutar.
:::

## Bir servisi bağlamak

Servis spec'ine bir `auth` bloğu ekle (veya Designer'da **Merkez Identity'ye JWT ile bağla** düğmesini aç):

```yaml
auth:
  authority: http://host.docker.internal:8081
  audience: baseforge-api
  protect: true
```

Üretilen `Program.cs`, `options.EnableJwt(...)` çağırır. Production'da adresi `Auth__Authority` ile ez ve `Auth__Issuer` değerini Identity'nin issuer'ına ayarla.

## Yetkilendirme

Kimlik doğrulama *"kimsin?"* sorusunu, yetkilendirme *"bunu yapabilir misin?"* sorusunu yanıtlar. BaseForge ikisini de spec'ten üretir.

```yaml
auth:
  defaultAccess: authenticated     # aşağıda belirtilmeyen action'lar için
  superRoles: [SuperAdmin]         # her kuralı otomatik geçer
entities:
  Order:
    ownerField: BuyerId            # create'te token'dan damgalanır
    access:
      list: [Admin, owner]         # admin hepsini, diğerleri yalnızca kendininkini görür
      create: authenticated
      update: [owner]
      delete: [Admin]
```

| Kural | Üretilen |
| --- | --- |
| `anonymous` | `[AllowAnonymous]` |
| `authenticated` | `[Authorize]` |
| `[Admin, Editor]` | `[Authorize(Roles = "Admin,Editor")]` |
| `[Admin, owner]` | `[Authorize]` + sahiplik kontrolü |

- Sahip alanı create'te sahte gönderilemez, update'te değiştirilemez.
- Sahip olmayanlar: update/delete'te **403**, başkasının kaydına `getById`'de **404**, `list`'te yalnızca kendi satırları.
- Kararı controller verir, handler uygular — böylece servisler arası güvenilir gRPC çağrıları etkilenmez.

Satırları (ör. taslakları) admin ve yazar dışında herkesten gizlemek için bir **görünürlük filtresiyle** birleştir:

```yaml
readFilter:
  where: { IsPublished: true }
  bypassRoles: [Admin]
  bypassOwner: true
```

## Kullanıcı profil alanları

Kullanıcıya yan tablo olmadan domain'e özgü alanlar ekle:

```yaml
userProfile:
  props:
    Specialty: string
    DiplomaNo: { type: string, nullable: true, maxLength: 32 }
    VerificationStatus:
      type: enum
      values: [Pending, Approved, Rejected]
      default: Pending
      editableBy: admin      # yalnızca admin değiştirebilir
      inToken: true          # JWT claim'i olur
```

Giriş SPA'sı profil ve admin formlarını otomatik çizer, `/api/account/me` bunları döner ve `identity/User`'a referans veren servisler bunları gRPC üzerinden alır.

Bu tasarımların gerekçelerinin tamamı için bkz. [Mimari §6](/tr/architecture#_6-kimlik-dogrulama).

## Şirketler, giriş yöntemleri ve e-posta (0.6.4)

```yaml
registration:
  enabled: true
  selfAssignableRoles: [Driver]      # uygulama kullanıcıyı Driver olarak kaydedebilir
multiTenancy:
  enabled: true
  adminRole: TenantAdmin             # şirketi açan/yöneten
  memberRoles: [Operator]            # şirket içindeki diğer roller
  requireApproval: true              # yeni şirket platform onayı bekler
  approverRoles: [SuperAdmin]
otp:
  enabled: true                      # telefonla giriş + telefon/e-posta doğrulama
  defaultCountryCode: "+90"
messaging:
  emailProvider: Smtp                # Log (geliştirme) | Smtp
  smtp: { host: smtp.resend.com, port: 465, username: resend, fromAddress: no-reply@ornek.com, fromName: Örnek }
passwordReset:
  enabled: true
  resetUrl: https://app.ornek.com/sifre-sifirla
accessTokenLifetimeMinutes: 60
```

| Uç | Amaç |
| --- | --- |
| `POST /api/tenants` · `GET /api/tenants/mine` | Şirket aç · şirketlerim, davetler, istekler |
| `POST /api/tenants/join` · `POST /api/tenants/memberships/{id}/accept\|decline` | Şirket koduyla katıl · davete yanıt |
| `GET/POST /api/tenants/{id}/members…` | Şirket yöneticisi: liste, davet, onay/ret, rol, çıkarma |
| `POST /api/admin/tenants/{id}/approve\|reject\|suspend\|activate` | Platform onayı |
| `tenant_id` ile `POST /connect/token` | Aktif şirketi seç/değiştir (`refresh_token` grant'ı) |
| `POST /api/account/otp/send` + `urn:baseforge:grant-type:otp` grant'ı | Telefonla giriş/kayıt (`phone`, `code`, isteğe bağlı `role`) |
| `POST /api/account/phone/send\|verify` · `email/send\|verify` | Hesaba telefon / e-posta doğrula |
| `POST /api/account/forgot-password` · `reset-password` | E-postayla şifre sıfırlama |

SMTP parolası `.env`'e `Auth__Messaging__Smtp__Password` olarak yazılır. Ayrıntı: [Mimari §6.6](/tr/architecture).
