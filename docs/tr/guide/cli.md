# CLI Referansı

`baseforge` komutu `BaseForge.CodeGen` .NET tool'u ile kurulur:

```bash
dotnet tool install -g BaseForge.CodeGen --prerelease
dotnet tool update  -g BaseForge.CodeGen --prerelease
```

## Komutlar

| Komut | Açıklama |
| --- | --- |
| `baseforge new <servis>` | [Designer](/tr/guide/designer)'ı boş bir spec ile açar |
| `baseforge update <servis>` | Designer'ı `<servis>/spec.yaml` (ve varsa `identity/auth.yaml`) yüklenmiş olarak açar |
| `baseforge new-service --spec <dosya.yaml>` | ER diyagramını üretir, onay ister, sonra servisi üretir |
| `baseforge new-identity --spec <auth.yaml>` | Config-driven merkezi Identity servisini üretir |
| `baseforge er --spec <dosya.yaml>` | Spec'ten yalnızca draw.io ER diyagramı üretir |

## Seçenekler

| Seçenek | Geçerli olduğu | Açıklama |
| --- | --- | --- |
| `--spec <dosya>` | `new-service`, `new-identity`, `er` | YAML spec dosyası (zorunlu) |
| `--output <klasör>` | `new-service`, `new-identity`, `er` | Çıktı klasörü (`er`: `.`, `new-service`: `./<servis>`) |
| `--yes` | `new-service` | Onay sormadan devam et |
| `--port <n>` | `new`, `update` | Designer portu (varsayılan `3500`) |
| `--no-browser` | `new`, `update` | Tarayıcıyı otomatik açma (uzak/headless makineler) |

## Workspace yapısı

CLI'yı bir **workspace klasöründen** çalıştır. Her servis kendi alt klasörüne üretilir; kökteki paylaşılan `services.json` kaydı portları ve Identity adresini tutar, böylece servisler birbirini bulabilir:

```
my-platform/
├── services.json        ← paylaşılan kayıt (portlar, authority)
├── observability/       ← Loki + Grafana, ilk üretimde oluşur
├── identity/
│   ├── auth.yaml
│   └── …
├── products/
│   ├── spec.yaml
│   └── …
└── orders/
    ├── spec.yaml
    └── …
```

Birbirine referans veren spec'ler (`externalRefs`, `subscribes`) aynı workspace'teki kardeş spec'ler aranarak çözülür.

## EF Core modelinden ER diyagramı

`baseforge er` (spec → draw.io) dışında, `BaseForge.Tools` paketi herhangi bir EF Core modelini [dbdiagram.io](https://dbdiagram.io)'ya yapıştırabileceğin [DBML](https://dbml.dbdiagram.io/)'e çevirir:

```csharp
using BaseForge.Tools;

// Yalnızca model okunur — veritabanı bağlantısı gerekmez.
string dbml = DbmlGenerator.Generate(dbContext);
File.WriteAllText("docs/er.dbml", dbml);
```

Gerçek tablo/kolon adları, provider'a özgü kolon tipleri (ör. PostgreSQL `uuid` / `timestamptz`), birincil anahtarlar ve foreign key ilişkileri yansıtılır.
