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

## Sürüm güncellemesi (alpha → beta)

Kullanıcı isteğiyle: `Directory.Build.props`'ta `<Version>` ve `src/BaseForge.CodeGen/Generation/CodeGenerator.cs`'teki `BaseForgeVersion` sabiti `0.2.1-alpha` → **`0.3.0-beta`** olarak güncellendi (ikisi senkron olmak zorunda — biri üretilen servislerin restore ettiği `PackageReference` sürümü, diğeri local pack/dev varsayılanı). `dotnet build BaseForge.slnx` ile doğrulandı, local feed + global `baseforge` aracı da `0.3.0-beta`'ya taşındı (`dotnet tool uninstall` + `install` gerekti — semver'de `-local` etiketi `-beta`'dan "büyük" sayıldığından `update` reddetti).

**Kullanıcının yapacağı (nuget.org push):** GitHub Release oluşturulurken tag **`v0.3.0-beta`** olmalı — `.github/workflows/publish.yml` sürümü tag'den alıp `v` prefix'ini düşürüyor; tag'in `CodeGenerator.cs`'e gömülü sabit sürümle (`0.3.0-beta`) birebir eşleşmesi zorunlu, aksi halde üretilen servisler nuget.org'da olmayan bir sürüm ister.

## CI/CD düzeltmesi — kullanıcı gerçek bir Actions hatasıyla geri döndü

Kullanıcı `v0.3.0-beta` tag'iyle push denedi, `publish.yml`'in Build adımı "1 Error(s)" ile patladı. `gh` CLI mevcut değildi (yüklü değil), log parçaları hatanın kendisini göstermeden kesiliyordu. Kullanıcı doğru sezgiyle "Identity web ile alakalı sanırım" dedi — doğruydu.

**Kök neden 1:** `services/BaseForge.Identity.Web/dist` `.gitignore`'lu ve `BaseForge.CodeGen.csproj`'un `BuildIdentityWeb` target'ı bunu **otomatik build etmiyor** (bilinçli tasarım — `dotnet pack`'in çoklu geçişleri arasındaki race condition'ı önlemek için, bkz. csproj yorumu). Ne `ci.yml` ne `publish.yml`'de bunu build eden bir adım vardı — taze bir checkout'ta `dist` hiç yok, `<Error Condition="!Exists(...)">` her zaman patlar. Bu, bu oturumun en başında yerelde çözdüğüm sorunun CI'daki karşılığı — CI'ya hiç taşınmamıştı.
- Düzeltme: `.github/workflows/ci.yml` ve `publish.yml`'e `actions/setup-node@v4` (node 22) + `cd services/BaseForge.Identity.Web && npm ci && npm run build` adımı eklendi, `dotnet restore`'dan önce.

**Kök neden 2 (düzeltmeyi doğrularken bulundu):** `package-lock.json`, `package.json` ile senkron değildi — `npm ci` (CI'nın kullanacağı, `npm install`'dan farklı olarak sıkı/lockfile-birebir kurulum) `@emnapi/core@1.11.1`/`@emnapi/runtime@1.11.1` eksik diyerek reddediyordu. Muhtemelen bu oturumun en başında (dist'i düzeltirken) çalıştırdığım `npm install`'ın lockfile'ı tutarsız bir şekilde güncellemesinden kaynaklanıyor — `npm install` "gevşek" kurulum yaptığı için o zaman fark edilmemişti, ilk kez şimdi (`npm ci` ile temiz bir state'ten test ederken) ortaya çıktı.
- Düzeltme: `npm install` yeniden çalıştırılıp lockfile senkronize edildi; `rm -rf node_modules dist && npm ci && npm run build` ile temiz state'ten doğrulandı.

**Uçtan uca doğrulama:** `publish.yml`'in tam sırası (`dotnet restore` → `build -c Release --no-restore -p:Version=0.3.0-beta` → `test -c Release --no-build` → `pack -c Release --no-build -o artifacts -p:Version=0.3.0-beta`) yerelde adım adım tekrarlandı — **hepsi başarılı**, 5 nupkg üretildi (Core/Infrastructure/API/Tools/CodeGen, hepsi 0.3.0-beta). Artifacts klasörü temizlendi (zaten gitignore'lu).

## İkinci Actions denemesi — kullanıcı ekran görüntüsüyle geri döndü

`npm install` ile "düzelttiğim" lockfile push edilip tekrar denendiğinde CI **yine** `npm ci` adımında patladı — ama bu sefer `@emnapi/core@1.11.2`/`@emnapi/runtime@1.11.2` eksik diyordu (öncekinde `1.11.1`). Bu, benim Windows'ta ürettiğim lockfile'ın Linux CI runner'ında farklı optional/platform-specific alt paketler resolve etmesinden kaynaklanan bir **cross-platform lockfile kararsızlığıydı** — tahmin etmek yerine Docker ile gerçek bir `node:22` Linux container'ında doğrudan reprodüklendi.

- Container'da `npm ci` aynı hatayla patladı (kanıtlandı) → container içinde `npm install` ile lockfile Linux'a göre yeniden üretildi → temiz state'ten `npm ci` + `npm run build` **başarılı** oldu.
- Ardından Windows'ta `npm install` çalıştırılınca lockfile **tekrar** değişti (46 satır fark) — yani bu paket, hangi platformda `npm install` çalıştırılırsa lockfile o platforma kayıyor, sürekli kırılan bir denge. Kalıcı çözüm lockfile'ı "doğru" tutmaya çalışmak değil, **CI adımını `npm ci`'den `npm install`'a çevirmek** (`ci.yml` + `publish.yml`, ikisi de güncellendi) — `npm install` sıkı lockfile-senkron istemiyor, CI kendi platformunda taze çözüyor, bu sınıf soruna tamamen bağışık.
- Hem Linux (Docker `node:22`) hem Windows'ta `npm install` + `npm run build` ile doğrulandı, ikisi de başarılı. `dotnet build BaseForge.slnx` tekrar 0 hata ile geçti.

**Ders:** Bu paketin lockfile'ını bir daha Windows'ta `npm ci` ile test etmeye çalışmayın (zaten hiçbir platformda "kalıcı doğru" bir hali yok) — CI artık `npm install` kullandığı için bu önemsiz.

## Designer.Web — counters desteği eklendi (gerçek bir eksiklik)

Kullanıcı Designer'da `SerialPool.consumedCount`'u counter yapmak isterken UI'da hiçbir kontrol olmadığını fark etti. Daha önceki explore ajanı bunu zaten doğrulamıştı (`counters`/`anonymousActions` C#'ta var, React'ta hiç yok) ama bu ana kadar `counters` için elle bir ekleme yapılmamıştı. Küçük, `appendOnly` ile aynı desende bir ekleme:

- `types.ts` — `EntitySpec.counters?: string[]` eklendi.
- `EntityEditor.tsx` — prop'un ⚙ gelişmiş panelinde, **yalnızca `int` tipli alanlarda** görünen bir "counter" checkbox'ı eklendi. Prop yeniden adlandırılırsa/silinirse `counters` dizisi de senkron tutuluyor (dangling reference olmasın diye, App.tsx'in relation rename'de yaptığı disipline benzer); tip `int`'ten başka bir şeye değiştirilirse counter işareti otomatik kaldırılıyor (`SpecValidator`'ın "counter yalnızca int'te olur" hatasına düşmemek için).
- `AnonymousActions` (entity-bazlı action kısıtlama) için hâlâ UI yok — bugüne kadar ihtiyaç doğmadı, gerekirse aynı desenle eklenir.
- ✅ `dotnet build src/BaseForge.CodeGen` (npm build dahil) — 0 hata, 0 uyarı.

## Kullanıcının Pharma/core üretimi — yetim "Entity" dosyaları

Kullanıcı `core`'u ilk kez gerçekten üretti (`C:\Users\pc\Desktop\Pharma\core`), build 3 hata ile patladı: `Grpc/EntityGrpcService.cs` içinde `EntityService`/`EntityMessage`/`EntityByIdRequest` bulunamadı. Kök neden: Designer'da bir noktada "+ ekle" ile varsayılan isimli ("Entity") boş bir entity oluşmuş, sonra silinmiş — ama **CodeGen önceki üretimden kalan dosyaları hiç temizlemiyor**. `Core.csproj`'daki `<Protobuf Include="Protos/entity.proto">` doğru şekilde kaybolmuştu ama 7 yetim dosya (`Controllers/EntitysController.cs`, `Entities/Entity.cs`, `Features/Entitys/Entity{Commands,Dto,Queries}.cs`, `Grpc/EntityGrpcService.cs`, `Protos/entity.proto`) diskte kalmıştı ve .NET SDK'nın varsayılan `**/*.cs` glob'u onları hâlâ derliyordu. Silindi, `obj`/`bin` temizlendi, `dotnet restore` + `build` **0 hata** ile geçti.

## Docker-doğru port/Authority + otomatik artan port önerisi

Kullanıcı 4 Pharma servisinin (core/its/netsis/api) hiç port ayarlanmadığı için hepsinin aynı
varsayılan portları (8080/8081/5432) kullandığını fark etti. İncelerken daha derin bir sorun
bulundu: JWT `Authority` Docker container içinden asla erişilemeyecek `localhost:5090` literal'i
olarak gömülüyordu (gRPC client'ların doğru kullandığı `host.docker.internal` deseni JWT
tarafına hiç yayılmamış). Plan: `C:\Users\pc\.claude\plans\declarative-splashing-fox.md`.

- **Bölüm 2'de araştırma sırasında bulunan 5. gerçek hata:** `Templates.cs:875`'teki
  `Grpc:{Provider}` appsettings satırı gRPC portunu da **hardcoded `8081`** yazıyordu —
  sağlayıcının gerçek portundan bağımsız. Bugüne kadar fark edilmemiş çünkü herkes varsayılan
  portları kullanıyordu; artık portlar rutin farklılaşacağı için bu da kırılacaktı.
- **Bölüm 1** — `ServiceRegistry.cs`: `ServiceRegistryEntry`'ye `PostgresPort` eklendi;
  `UpsertService`/`UpsertIdentity` bunu dolduruyor; yeni public `LoadForWorkspace(workspaceRoot)`.
- **Bölüm 2** — `CodeModel.cs`: `GrpcClientResolution.ProviderGrpcPort` (varsayılan 8081) eklendi.
  `CodeGenerator.cs`: `TryResolveSibling` artık `siblingSpec.DockerPorts?.Grpc ?? 8081` kullanıyor;
  `TryResolveIdentityUser` artık `outputDir`'den hesaplanan workspace kökünden
  `ServiceRegistry.LoadForWorkspace` ile identity'nin gerçek portunu (`?? 8082`) okuyor;
  `ResolveExternalRefs` imzasına `outputDir` eklendi. `Templates.cs:875` artık
  `{{ c.ProviderGrpcPort }}` kullanıyor (hardcoded 8081 değil).
- **Bölüm 3** — `DesignerEndpoints.cs`: yeni `GET /api/workspace` (ServiceRegistry'yi döner);
  `/api/meta`'ya `ServiceIsNew`/`IdentityIsNew` eklendi (dosya varlığına bakarak, `ctx.LoadExisting`'den
  bağımsız — daha güvenilir).
- ✅ `dotnet build src/BaseForge.CodeGen` — 0 hata, 0 uyarı (backend tarafı tamamlandı).
- **Bölüm 4** — `types.ts`: `Meta.serviceIsNew/identityIsNew`, yeni `WorkspaceEntry` interface'i.
  `api/client.ts`: `workspace()` çağrısı. `App.tsx`: `suggestPorts`/`suggestAuthority`/`portsEqual`
  yardımcıları; mount effect'te workspace çekilip yeni servis/identity için portlar+authority
  gerçek (placeholder değil, düzenlenebilir) değer olarak ön-dolduruluyor; auth toggle varsayılanı
  artık `suggested.authority`; `generate()` başarı sonrası workspace'i yeniden çekip yalnızca
  **hâlâ önceki önerilen değere eşit kalan** alanları güncelliyor (elle girileni ezmiyor) —
  Identity + servis aynı oturumdan art arda üretilme senaryosunu kapsıyor. Port alanlarının
  altına, workspace'teki diğer servisleri gösteren bir ipucu eklendi (TypeScript'in
  "workspace state hiç okunmuyor" uyarısını da gerçek bir faydaya çevirdi).
- ✅ `dotnet build BaseForge.slnx` + `dotnet test tests/BaseForge.UnitTests` — 0 hata, 7/7 test başarılı.

## Doğrulama (uçtan uca)

1. **Regresyon:** `samples/blog.yaml` yeniden üretildi (workspace'te `services.json` yok — izole
   `/tmp` klasörü) — `Grpc:Identity` doğru şekilde identity'nin kendi varsayılanına (`8082`, eski
   hardcoded `8081` değil) düştü, `dotnet build` 0 hata.
2. **Gerçek senaryo:** Identity özel portlarla (`9000`/`9001`/`5433`) `/tmp/multiservice-test`'e
   üretildi → `services.json`'da doğru kaydedildiği doğrulandı → `identity/User`'a referans veren
   yeni bir `testsvc` üretildi → **`appsettings.json`'da `"Identity": "http://host.docker.internal:9001"`**
   (hardcoded `8081` değil, gerçek kayıtlı port) — `dotnet build` 0 hata. Gerçek bug tam olarak
   hedeflendiği gibi düzeltildi.
3. Geçici test klasörleri temizlendi.

## docs/ARCH.md

- §7.1 (Servis Kaydı / ServiceRegistry — port doğruluğu) ve §7.2 (Designer otomatik port/Authority
  önerisi) eklendi. Karar Günlüğü'ne 1 satır.

## Kapanış

- `dotnet build BaseForge.slnx` + `dotnet test` — 0 hata, 7/7 test başarılı.
- Global `baseforge` aracı `0.3.4-local`'e güncellendi (çalışan bir Designer oturumu yoktu, sormaya
  gerek kalmadı).
- **Sonuç:** Kullanıcının port çakışması gözlemi, gerçekte 2 ayrı kök nedene ("hiç port ayarlanmamış"
  + "Authority Docker'da hiç çalışmayan bir adres") ve araştırma sırasında bulunan 3. bir gerçek
  hataya (gRPC cross-service portu da hardcoded'dı) çıktı. Üçü de BaseForge'a kalıcı olarak
  eklendi/düzeltildi, iki ayrı senaryoyla (tek servis regresyon + gerçek çok-servisli özel port
  testi) uçtan uca doğrulandı. Kullanıcının 4 mevcut Pharma servisindeki port/Authority değerlerini
  Designer'ı yeniden açarak (artık otomatik doğru öneri gelecek) kendisinin güncellemesi gerekiyor —
  mevcut spec.yaml'lar otomatik değiştirilmedi.

**Bilinen kısıt (CodeGen'e kasıtlı eklenmedi):** Bir entity Designer'da silinip yeniden üretilirse, eski dosyaları elle silmek gerekiyor — otomatik temizlemek, kullanıcının elle eklediği iş mantığı dosyalarını (örn. TraceEvent "verify" handler'ı) da silme riski taşıdığı için bilinçli olarak yapılmadı.

## ER önizlemesi (DBML + canlı diyagram) — MultiTenant'ın TenantId'yi hiç göstermediği bulundu

Kullanıcı `core` için ürettiği DBML'i yapıştırdı, önceki 3 bulgunun (AggregateId ismi, ilişki kardinalitesi, ConsumedCount nullable) hepsi doğru düzeltilmişti. Ama **hiçbir tabloda `TenantId` yoktu** — Multi-tenancy açıkken bunun görünmesi gerekirdi. Kontrol edilince: bu, ER önizlemesinin **3 ayrı yerde** (ikisi Designer.Web'de canlı önizleme için, biri CodeGen'de gerçek `.drawio` dosyası için) `ServiceSpec.MultiTenant`'tan tamamen habersiz, kendi başına `entity.Props`'u dolaşan kopya bir implementasyon olmasından kaynaklanıyor — Capability C'yi eklerken yalnızca `CodeGenerator.BuildEntityModel`'e (gerçek `.cs` üretimi) `TenantId` enjeksiyonu eklenmişti, ER önizleme kod yollarına hiç yayılmamıştı.

- `src/BaseForge.Designer.Web/src/dbml.ts` (`toDbml`, "DBML kopyala" butonu) — `spec.multiTenant` true iken her tabloya `TenantId uuid [not null]` satırı eklendi.
- `src/BaseForge.Designer.Web/src/components/ErDiagram.tsx` (canlı görsel ER kutucukları) — aynı koşulla `TenantId · uuid` satırı eklendi.
- `src/BaseForge.CodeGen/Generation/DrawioErGenerator.cs` (`baseforge new-service`'in diske yazdığı gerçek `.drawio` dosyası) — `BuildEntityLines`'a `multiTenant` parametresi eklendi, aynı satır eklendi.
- ✅ `dotnet build BaseForge.slnx` (tam çözüm, npm build dahil) — 0 hata, 0 uyarı.

## Designer.Web — dış referans `store` alanı için input yoktu (gerçek bir eksiklik)

Kullanıcı ER diyagramında `TraceEvent.operator` dış referansının `store: CustomerId` gösterdiğini fark edip sordu. Kontrol edince: `EntityEditor.tsx`'teki "+ referans" butonu `store: "CustomerId"` (muhtemelen `orders.yaml` örneğinden kalma) hardcoded varsayılanla ekliyordu, ama render edilen `ext-row`'da **`store` için hiç input alanı yoktu** — kullanıcı bunu asla değiştiremezdi, sadece isim (`xName`) ve hedef (`target`) ve `via` dropdown'ı vardı. `counters` ile aynı sınıf bir eksiklik.

- "+ referans" varsayılanları `target: "", store: ""` olarak boşaltıldı (yanıltıcı hardcoded örnek değer kalmasın diye — `target` zaten `placeholder="servis/Entity"` gösteriyor).
- `ext-row`'a `store` için yeni bir `<input placeholder="ör. OperatorId">` eklendi (`.ext-row` flexbox olduğu için CSS'e dokunmaya gerek kalmadı).
- ✅ `dotnet build src/BaseForge.CodeGen` (npm build dahil) — 0 hata, 0 uyarı.

