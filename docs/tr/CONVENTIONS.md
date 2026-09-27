# BaseForge — Kod Standartları ve Naming Kuralları (CONVENTIONS)

Bu doküman, BaseForge ve onu kullanan mikroservislerde uyulması gereken kod yazım standartlarını tanımlar.

## Naming Conventions

| Eleman | Kural | Örnek |
| --- | --- | --- |
| Interface | `I` prefix'i ile başlar | `IRepository<T>`, `ICommand` |
| Base sınıf | `Base` prefix'i ile başlar | `BaseEntity`, `BaseController` |
| Handler | `Handler` ile biter | `CreateUserCommandHandler` |
| Query | `Query` suffix'i ile biter | `GetUserByIdQuery` |
| Command | `Command` suffix'i ile biter | `CreateUserCommand` |

## Katman Kuralları

- `Core` katmanı hiçbir dış bağımlılık almaz (yalnızca MediatR interface'leri).
- `Infrastructure` katmanı `Core`'a bağımlıdır; `API`'ye bağımlı **olamaz**.
- `API` katmanı her iki katmana da bağımlı olabilir.

## Dil ve Stil

- Dil: **C# / .NET 10**, `LangVersion=latest`.
- `Nullable` ve `ImplicitUsings` tüm projelerde **açık**.
- `TreatWarningsAsErrors=true` — uyarılar hata sayılır. Analiz seviyesi: `latest-recommended`.
- Public üyeler XML doc içerir (`GenerateDocumentationFile=true`).
- Bilinçli istisnalar `NoWarn` ile yönetilir (örn. test projelerinde `CA1707`).

## Veri Erişimi

- **EF Core 10** birincil ORM'dir: yazma, change tracking ve migration. CRUD'un çoğu LINQ ile yazılır.
- **Dapper** ağır okuma / karmaşık join sorgularında ham SQL için kullanılır (sonuç → DTO mapping). EF `DbContext`'inin bağlantısı üzerinden çalışır.
- SQL elle yazıldığında (Dapper veya EF `FromSql`) **parametreli** sorgu zorunludur (SQL injection'a karşı).
- Dapper ile yazılan sorgularda soft delete koşulu (`is_deleted = false`) elle eklenir; EF global query filter Dapper'ı kapsamaz.
- Tüm entity'ler `BaseEntity`'den türer; audit alanları (`CreatedAt`, `UpdatedAt`, `CreatedBy`) ve soft delete EF `SaveChanges`/query filter ile otomatik yönetilir.

## CQRS

- Komut/sorgu ayrımı uygulanır.
- Her command/query için tek bir handler bulunur.
- MediatR dışında başka bir CQRS kütüphanesi eklenmez.

## Klasör Yerleşimi

- Entity base'leri → `Core/Entities`
- Sözleşmeler → `Core/Interfaces`, `Core/CQRS`
- Exception tipleri → `Core/Exceptions`
- Repository implementasyonları → `Infrastructure/Repositories`
- Veri erişimi / query builder → `Infrastructure/Data`
- DI extension'ları → `Infrastructure/Extensions`, `API/Extensions`
- Controller base + middleware → `API/Controllers`, `API/Middleware`

## Test

- Birim testler `BaseForge.UnitTests`, entegrasyon testleri `BaseForge.IntegrationTests` altında.
- Test metot adlarında `MetotAdı_Durum_BeklenenSonuç` biçimi kullanılır (alt çizgi serbesttir).
- Framework: xUnit.

## Commit & Branch

- Anlamlı, küçük commit'ler tercih edilir.
- Yeni özellik eklenmeden önce `docs/ARCH.md` güncellenir.
