# 2026-07-13 — 3 yeni jenerik kapasite: JSON tipi, Append-Only entity, Multi-Tenancy

Plan: `C:\Users\pc\.claude\plans\declarative-splashing-fox.md`. Otomatik yürütme modu — her adım burada loglanıyor, ara onay istenmiyor.

## Log

- **A.1** `src/BaseForge.CodeGen/Spec/TypeMap.cs` — `Map`'e `["json"] = ("string", "jsonb")`, `KnownTypes`'a `"json"` eklendi.
- **A.2** `src/BaseForge.CodeGen/Spec/ProtoTypeMap.cs` — `Map`'e `["json"] = "string"` eklendi. Planda öngörülmeyen bir düzeltme: nullable `ToProtoExpr`/`FromProtoExpr`'daki `"string" or "text"` pattern'lerine `"json"` de eklendi — eklenmeseydi nullable bir json alanı gRPC'de default fallback'e (`GetValueOrDefault()`, string'de yok) düşüp derleme hatası verirdi.
- **A.3** `src/BaseForge.CodeGen/Generation/CodeModel.cs` — `ScalarModel.IsJson` (bool) eklendi.
- **A.4** `src/BaseForge.CodeGen/Generation/CodeGenerator.cs` — `BuildScalars`'ta `IsJson = prop.Type.Equals("json", OrdinalIgnoreCase)` dolduruldu.
- **A.5** `src/BaseForge.CodeGen/Generation/Templates.cs` — `Templates.Entity`'ye `{{~ if p.IsJson ~}} [Column(TypeName = "jsonb")] {{~ end ~}}` + `using System.ComponentModel.DataAnnotations.Schema;` eklendi (yalnızca entity sınıfında, Create/Update DTO'larda yok — plandaki karar).
- ✅ `dotnet build src/BaseForge.CodeGen` — 0 hata, 0 uyarı. **Capability A tamamlandı.**

- **B.1** `src/BaseForge.CodeGen/Spec/ServiceSpec.cs` — `EntitySpec.AppendOnly` (bool) eklendi.
- **B.2** `src/BaseForge.CodeGen/Spec/SpecValidator.cs` — `AppendOnly=true` iken `anonymousActions`'da update/delete ve `publishes`'ta created dışında bir şey olursa hata (fail loud, sessiz yok sayma yok).
- **B.3** `CodeModel.cs` — `FeatureFileModel`/`ControllerFileModel`'e `IncludeUpdate`/`IncludeDelete` (`= true` varsayılan — mevcut davranışı bozmamak için kritik).
- **B.4** `CodeGenerator.cs` — her iki model construction'ına `IncludeUpdate = !entity.AppendOnly`, `IncludeDelete = !entity.AppendOnly` eklendi.
- **B.5** `Templates.cs` — Commands (Update/Delete komut+handler blokları) ve Controller ([HttpPut]/[HttpDelete]) `{{~ if IncludeUpdate/IncludeDelete ~}}` ile sarıldı.
- **B.6** Designer.Web `types.ts` — `EntitySpec.appendOnly?: boolean`, `ServiceSpec.multiTenant?: boolean` (Capability C için de burada eklendi).
- **B.7** Designer.Web `EntityEditor.tsx` — `paginated/sortable/searchable` toggle satırının yanına "Append-only" toggle'ı + açıklayıcı hint eklendi.
- ✅ `dotnet build src/BaseForge.CodeGen` (npm build dahil) — 0 hata, 0 uyarı. **Capability B tamamlandı.**

- **C.1** `src/BaseForge.Core/Entities/ITenantEntity.cs` (yeni) — `ISoftDelete` ile aynı şekilde marker interface, `Guid TenantId`.
- **C.2** `src/BaseForge.Core/Interfaces/ICurrentTenant.cs` (yeni) — `ICurrentUser` ile aynı şekilde, `Guid? TenantId`.
- **C.3** `src/BaseForge.API/Authentication/CurrentTenant.cs` (yeni) — `CurrentUser`'ı mirror'lıyor, `"tenant_id"` JWT claim'ini okuyor.
- **C.4** `src/BaseForge.Infrastructure/Data/BaseForgeDbContext.cs` — constructor'a opsiyonel `ICurrentTenant? currentTenant` eklendi; `OnModelCreating` artık `ISoftDelete`+`ITenantEntity`'yi `Expression.AndAlso` ile birleştiren tek bir query filter kuruyor (4 durum: ne biri ne diğeri/yalnız soft-delete/yalnız tenant/ikisi birden); `ApplyAuditAndSoftDelete` artık `Added` durumundaki `ITenantEntity`'lere `TenantId` damgalıyor, `ICurrentTenant.TenantId` null ise `InvalidOperationException` fırlatıyor (fail loud).
- **C.5** `src/BaseForge.API/Extensions/BaseForgeOptions.cs` — `EnableMultiTenancy()` eklendi (`EnableAuditLog()` ile aynı şekil).
- **C.6** `src/BaseForge.API/Extensions/ServiceCollectionExtensions.cs` — `MultiTenancyEnabled` iken `ICurrentTenant`/`CurrentTenant` DI'a kaydediliyor.
- ✅ `dotnet build BaseForge.slnx` — 0 hata, 0 uyarı (Core/Infrastructure/API/Identity/Tools/UnitTests/IntegrationTests/CodeGen dahil tüm solution).

- **C.7** `src/BaseForge.CodeGen/Spec/ServiceSpec.cs` — `MultiTenant` (bool, servis-geneli) eklendi.
- **C.8** `src/BaseForge.CodeGen/Generation/CodeModel.cs` — `EntityFileModel.IsMultiTenant`, `ProgramFileModel.HasMultiTenancy` eklendi.
- **C.9** `src/BaseForge.CodeGen/Generation/CodeGenerator.cs` — `BuildEntityModel` artık `multiTenant` parametresi alıyor, true iken sentetik `TenantId` scalar'ını **yalnızca entity modeline** ekliyor (Create/Update DTO'larına değil — `FeatureFileModel.Fields` ayrı bir `BuildScalars` çağrısından geliyor, etkilenmiyor); `ProgramFileModel.HasMultiTenancy = spec.MultiTenant`.
- **C.10** `src/BaseForge.CodeGen/Generation/Templates.cs` — entity class bildirimine koşullu `, ITenantEntity`; Program.cs şablonuna `{{~ if HasMultiTenancy ~}} options.EnableMultiTenancy(); {{~ end ~}}`.
- **C.11 — planda öngörülmeyen kritik düzeltme:** Üretilen DbContext'in constructor'ı (`Templates.DbContext`) `ICurrentUser`/`ICurrentTenant`'ı `BaseForgeDbContext`'e hiç forward etmiyordu (`: base(options)` — sadece options). Bu haliyle `_currentTenant` her zaman null kalır ve **her Create çağrısı** yeni eklenen `InvalidOperationException`'a çarpardı (multi-tenant hiç çalışmazdı). Constructor imzası `(DbContextOptions<T> options, ICurrentUser? currentUser = null, ICurrentTenant? currentTenant = null) : base(options, currentUser, currentTenant)` olarak düzeltildi. Yan etki: bu, önceden de var olan (audit `CreatedBy` hiç dolmuyordu) ama kapsam dışı bir hatayı da düzeltmiş oldu.
- **C.12** Designer.Web `App.tsx` — "Servis ayarları"nda auth toggle'ının hemen altına `multiTenant` toggle'ı eklendi.
- ✅ `dotnet build BaseForge.slnx` (tam çözüm, npm build dahil) — 0 hata, 0 uyarı. **Capability C tamamlandı.**

## Doğrulama

- **Regresyon:** `samples/blog.yaml` ve `samples/orders.yaml` yeniden üretildi. Committed bir eski çıktı olmadığı için (services/ altında yalnızca Identity var, örnek servisler daha önce kaldırılmış) diff yerine gerçek `dotnet build` ile doğrulandı: Core/Infrastructure/API'yi local feed'e (`localfeed/`, `-p:Version=0.2.1-alpha` — CodeGen'in referans aldığı sabit sürümle eşleşecek şekilde) pack'leyip, üretilen projeleri oradan restore ettirdim. **İkisi de 0 hata ile derlendi** — yeni alanların varsayılanları (`IncludeUpdate/IncludeDelete=true`, `IsJson=false`, `HasMultiTenancy=false`) mevcut davranışı bozmuyor.
- **`samples/capabilities-test.yaml`** (yeni, kalıcı örnek): `multiTenant: true`, `AuditLog` (`appendOnly: true` + `payload: json`) ve `Note` (`body: json, nullable: true`) entity'leriyle üretim yapıldı, local feed'den restore edilip **0 hata ile derlendi**. Doğrulananlar:
  - `AuditLog.cs`: `: BaseEntity, ITenantEntity`, `TenantId` scalar'ı, `Payload` alanında `[Column(TypeName = "jsonb")]` ✓
  - `AuditLogCommands.cs`/`AuditLogsController.cs`: Update/Delete komutu/action'ı **yok**, yalnızca Create/GetById/List ✓
  - `Program.cs`: `options.EnableMultiTenancy();` ✓
  - `CapabilitiestestDbContext.cs`: constructor `ICurrentUser?`/`ICurrentTenant?`'ı `base()`'e forward ediyor ✓
- **Planda öngörülmeyen kozmetik düzeltme:** İlk üretimde `public sealed class AuditLog : BaseEntity, ITenantEntity{` (satır sonu `{` ile birleşmiş) çıktı — Scriban'ın `{{~ end ~}}` trim'i sonraki satırdaki `{`'yi yuttu. Templates.cs'teki bu inline koşulu (`RequireHttpsMetadata` ternary'sindeki mevcut konvansiyona uyacak şekilde) trim marker'sız (`{{ if }}...{{ end }}`) yazıp düzelttim; yeniden üretimde doğrulandı.

## Birim testler

- `tests/BaseForge.UnitTests/BaseForge.UnitTests.csproj` — `Microsoft.EntityFrameworkCore.InMemory` (10.0.9, Infrastructure'daki EF Core sürümüyle eşleşiyor) test bağımlılığı eklendi.
- `tests/BaseForge.UnitTests/Data/BaseForgeDbContextQueryFilterTests.cs` (yeni) — 4 fixture entity (ne biri ne diğeri / yalnız soft-delete / yalnız tenant / ikisi birden) + tenant olmadan ekleme denemesinin `InvalidOperationException` fırlattığını doğrulayan 5 test.
- **🔴 Testin yakaladığı GERÇEK production bug'ı (plan aşamasında öngörülmemiş):** `OnModelCreating`'deki `Expression.Constant(this)`, `this`'i **runtime tipiyle** (örn. `FilterTestDbContext`, gerçek dünyada her zaman CodeGen'in ürettiği türetilmiş DbContext sınıfı) tipliyordu. `CurrentTenantId` `private` ve yalnızca `BaseForgeDbContext`'te tanımlı olduğu için, private üyeler `FlattenHierarchy` ile türetilmiş tipe miras alınmadığından reflection bunu bulamıyor, `ArgumentException` fırlatıyordu. **Bu, herhangi bir gerçek üretilmiş multi-tenant serviste ilk sorguda çökme anlamına gelirdi** — `capabilities-test.yaml`'ın derleme testi bunu yakalamamıştı çünkü o yalnızca compile-time'ı doğruluyordu, hiç sorgu çalıştırmıyordu. Düzeltme: `Expression.Constant(this, typeof(BaseForgeDbContext))` — constant'ı açıkça temel sınıf olarak tipleyerek private property'yi doğru yerde arattırdı.
- ✅ `dotnet test tests/BaseForge.UnitTests` — 7/7 başarılı (düzeltmeden önce 5/7 başarısızdı, tam olarak bu bug yüzünden).

## docs/ARCH.md

- §5.3 (JSON/JSONB), §5.4 (Append-Only), §5.5 (Multi-Tenancy) eklendi — §5.1/§5.2 ile aynı formatta (Karar/Gerekçe/Kısıtlar), iki gerçek bug bulgusu da §5.5'e not düşüldü.
- Karar Günlüğü'ne 3 satır eklendi (2026-07-13).

## Kapanış

- Global `baseforge` aracı `0.3.0-local`'e güncellendi — Designer UI'da `appendOnly`/`multiTenant` toggle'ları ve `json` tip seçeneği artık aktif.
- `samples/capabilities-test.yaml` kalıcı bir referans örneği olarak bırakıldı (blog.yaml/orders.yaml gibi).
- Geçici doğrulama klasörleri (`/tmp/regress-*`, `/tmp/capabilities-test`) temizlendi; `localfeed/` zaten `.gitignore`'da.
- **Sonuç:** Plandaki 3 kapasitenin tamamı uygulandı, tüm solution + yeni birim testler yeşil. Yol boyunca plan aşamasında öngörülmeyen 3 gerçek hata bulunup düzeltildi (ProtoTypeMap nullable json round-trip, üretilen DbContext'in ICurrentUser/ICurrentTenant'ı forward etmemesi, Expression.Constant'ın private property lookup'ını kırması) — hepsi ya derleme ya da birim testleriyle yakalandı, hiçbiri elle keşfedilmedi.
- **İncelemen gereken tek konu:** `services/BaseForge.Identity/Program.cs`, `AuthSpecValidator.cs`, `CliRunner.cs`, `DesignerEndpoints.cs`, `IdentityPanel.tsx` bu görevden ÖNCEKİ bir işten (seed admin parola politikası doğrulaması) kalan, hâlâ commit edilmemiş değişiklikler — bu logun kapsamı dışında, ayrı bir iş.

