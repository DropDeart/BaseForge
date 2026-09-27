# Servis Spec'i (`spec.yaml`)

Bir servis tek bir YAML dosyasıyla tarif edilir. Designer bu dosyayı okur ve yazar; `baseforge new-service --spec <dosya>` bu dosyadan servis üretir.

## Eksiksiz bir örnek

```yaml
service: blog
database: blog_db

auth:
  authority: http://host.docker.internal:8081
  audience: baseforge-api
  protect: true
  defaultAccess: authenticated
  superRoles: [SuperAdmin]

corsOrigins: [http://localhost:5173]

entities:
  Post:
    props:
      Title: { type: string, maxLength: 200 }
      Body: text
      Status: { type: enum, values: [Draft, Published], default: Draft }
      AuthorId: guid
      ViewCount: int
    counters: [ViewCount]
    ownerField: AuthorId
    access:
      list: anonymous
      getById: anonymous
      update: [Admin, owner]
      delete: [Admin]
    filterable: [Status, AuthorId]
    readFilter:
      where: { Status: Published }
      bypassRoles: [Admin]
      bypassOwner: true
    externalRefs:
      author: { target: identity/User, store: AuthorId, via: grpc }

  Comment:
    props:
      Body: text
    relations:
      post: { kind: many-to-one, target: Post }
    publishes: [created]

subscribes:
  - event: blog/CommentCreated
    handler: NotifyPostAuthorOnComment
```

## Servis seviyesi anahtarlar

| Anahtar | Tip | Açıklama |
| --- | --- | --- |
| `service` | string | Servis adı — proje, namespace ve klasör buradan türetilir |
| `database` | string | PostgreSQL veritabanı adı |
| `entities` | map | Entity adı → entity tanımı (aşağıda) |
| `auth` | object | Merkezi Identity'ye JWT bağlantısı — herkese açık servis için yazma |
| `auth.authority` / `auth.audience` | string | Identity adresi ve API audience'ı (varsayılan `baseforge-api`) |
| `auth.protect` | bool | Controller'lara `[Authorize]` koy (varsayılan `true`) |
| `auth.defaultAccess` | kural | Entity'nin `access`'inde belirtilmeyen action'lar için erişim (varsayılan `authenticated`) |
| `auth.superRoles` | liste | Tüm rol ve sahiplik kurallarını geçen roller |
| `multiTenant` | bool | Her entity'ye `TenantId` + izolasyon ekle ([detay](/tr/architecture#_5-5-multi-tenancy)) |
| `corsOrigins` | liste | İzinli tarayıcı origin'leri (`Cors:AllowedOrigins`) |
| `dockerPorts` | object | `rest`, `grpc`, `postgres` host portları (boş = varsayılan) |
| `rabbitMqTuning` | object | `outboxMaxRetries`, `outboxRetentionDays` |
| `subscribes` | liste | Servisin dinlediği event'ler: `event: servis/EntityKind`, `handler: SınıfAdı` |
| `gateway` | object | `proxiedServices: [a, b]` — diğer servisleri YARP ile `/api/gateway/{servis}` altında sun |

## Entity anahtarları

| Anahtar | Tip | Açıklama |
| --- | --- | --- |
| `props` | map | Alan adı → tip (kısa form) veya alan objesi (zengin form) |
| `relations` | map | Aynı servisteki entity'lerle ilişkiler |
| `externalRefs` | map | Diğer servislerdeki entity'lere referanslar |
| `publishes` | liste | `created` / `updated` / `deleted` — outbox üzerinden integration event yayınla |
| `access` | map | Action başına kural: `list`, `getById`, `create`, `update`, `delete` |
| `ownerField` | string | Create'te çağıranın id'siyle damgalanan bir `guid` alan |
| `anonymousActions` | liste | `access: { action: anonymous }` için eski kısa yazım |
| `filterable` | liste | Liste ucunda eşitlik filtresi olarak açılan alanlar |
| `readFilter` | object | Satır görünürlüğü: `where`, `bypassRoles`, `bypassOwner` |
| `counters` | liste | Herkese açık `POST /{id}/increment-{alan}` ucu alan `int` alanlar |
| `appendOnly` | bool | Update/Delete hiç üretilmez (audit/trace kayıtları) |
| `paginated` / `sortable` / `searchable` | bool | Liste davranışı (hepsi varsayılan `true`) |

## Alan tipleri

| Spec tipi | C# tipi | PostgreSQL tipi |
| --- | --- | --- |
| `string`, `text` | `string` | `text` |
| `int` / `long` / `short` | `int` / `long` / `short` | `integer` / `bigint` / `smallint` |
| `decimal` | `decimal` | `numeric` |
| `double` / `float` | `double` / `float` | `double precision` / `real` |
| `bool` | `bool` | `boolean` |
| `datetime` | `DateTimeOffset` | `timestamptz` |
| `date` | `DateOnly` | `date` |
| `guid`, `uuid` | `Guid` | `uuid` |
| `json` | `string` | `jsonb` |
| `enum` | üretilen C# enum'u | `text` (adıyla saklanır) |

Kısa ve zengin form birlikte kullanılabilir:

```yaml
props:
  Title: string                                     # kısa form
  Sku: { type: string, maxLength: 32 }              # zengin form
  Discount: { type: decimal, nullable: true }
  Status: { type: enum, values: [Draft, Active], default: Draft }
```

Zengin form anahtarları: `type`, `nullable`, `maxLength` (yalnızca string/text), `default`, `values` (yalnızca enum).

## İlişkiler

```yaml
relations:
  category: { kind: many-to-one, target: Category, nullable: true }
  lines:    { kind: one-to-many, target: OrderLine }
```

`kind` değeri `one-to-many`, `many-to-one` veya `one-to-one` olur. `many-to-one`/`one-to-one` bu entity'ye bir foreign key (`{İlişki}Id`) ekler; `nullable: true` onu opsiyonel yapar.

## Dış referanslar

```yaml
externalRefs:
  product: { target: products/Product, store: ProductId, via: grpc }
```

Foreign key oluşturulmaz — yalnızca bir ID kolonu (`store`). `via: grpc` ile BaseForge tipli bir gRPC istemcisi üretir. Bu dosyanın yanında kardeş bir spec (`products.yaml`) bulunursa istemci hedefin gerçek alanlarını taşır; bulunamazsa yalnızca ID içeren bir stub'a düşer. `identity/User` her zaman kullanılabilir.

## Erişim kuralları

Bir kural şunlardan biridir:

- `anonymous` — giriş gerekmez
- `authenticated` — giriş yapmış herhangi bir kullanıcı
- bir rol listesi, ör. `[Admin, Editor]` — istenirse `owner` da içerebilir

`owner`, "id'si `ownerField`'da olan kullanıcı" demektir. Sahip olmayanlar update/delete'te **403**, başkasının kaydına `getById`'de **404** alır, `list`'te ise yalnızca kendi satırlarını görür. Modelin tamamı için bkz. [Mimari §6.1](/tr/architecture#_6-1-yetkilendirme-modeli-roller-sahiplik).

## Doğrulama

Spec'ler herhangi bir kod yazılmadan önce doğrulanır — geçersiz kombinasyonlar (ör. `int` üzerinde `maxLength`, append-only bir entity'nin `publishes`'ında `updated`, `ownerField` olmadan `owner` kuralı) bozuk kod üretmek yerine açık bir mesajla hata verir.
