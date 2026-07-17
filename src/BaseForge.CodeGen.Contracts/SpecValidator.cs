using System.Globalization;

namespace BaseForge.CodeGen.Contracts;

/// <summary>Bir <see cref="ServiceSpec"/>'i kod/diyagram üretiminden önce doğrular.</summary>
public static class SpecValidator
{
    private static readonly string[] AllowedKinds = ["one-to-many", "many-to-one", "one-to-one"];

    private static readonly string[] AllowedPublishKinds = ["created", "updated", "deleted"];

    private static readonly string[] AllowedActions = ["list", "getById", "create", "update", "delete"];

    /// <summary>Spec'i doğrular ve bulunan hataların listesini döndürür (boşsa geçerli).</summary>
    public static IReadOnlyList<string> Validate(ServiceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(spec.Service))
        {
            errors.Add("'service' alanı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(spec.Database))
        {
            errors.Add("'database' alanı zorunludur.");
        }

        if (spec.Entities.Count == 0)
        {
            errors.Add("En az bir entity tanımlanmalıdır ('entities').");
        }

        foreach (var (entityName, entity) in spec.Entities)
        {
            if (!IsValidIdentifier(entityName))
            {
                errors.Add($"Geçersiz entity adı: '{entityName}' (geçerli bir C# tanımlayıcısı olmalı).");
            }

            foreach (var (propName, prop) in entity.Props)
            {
                if (!IsValidIdentifier(propName))
                {
                    errors.Add($"'{entityName}.{propName}' geçersiz alan adı.");
                    continue;
                }

                if (!TypeMap.IsKnown(prop.Type))
                {
                    errors.Add($"'{entityName}.{propName}' bilinmeyen tip: '{prop.Type}'.");
                    continue;
                }

                var isStringLike = string.Equals(prop.Type, "string", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prop.Type, "text", StringComparison.OrdinalIgnoreCase);

                if (prop.MaxLength is not null)
                {
                    if (!isStringLike)
                    {
                        errors.Add($"'{entityName}.{propName}' — 'maxLength' yalnızca string/text tipinde kullanılabilir.");
                    }
                    else if (prop.MaxLength <= 0)
                    {
                        errors.Add($"'{entityName}.{propName}' — 'maxLength' pozitif bir sayı olmalı.");
                    }
                }

                if (prop.Default is not null && !IsValidDefault(prop.Type, prop.Default))
                {
                    errors.Add($"'{entityName}.{propName}' — 'default' değeri ('{prop.Default}') '{prop.Type}' tipi için geçersiz " +
                               "(datetime/date/guid tiplerinde default desteklenmez).");
                }
            }

            foreach (var (relName, relation) in entity.Relations)
            {
                if (!AllowedKinds.Contains(relation.Kind, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"'{entityName}.{relName}' geçersiz ilişki türü: '{relation.Kind}' (izinli: {string.Join(", ", AllowedKinds)}).");
                }

                if (!spec.Entities.ContainsKey(relation.Target))
                {
                    errors.Add($"'{entityName}.{relName}' ilişkisinin hedefi '{relation.Target}' aynı serviste tanımlı değil. " +
                               "Başka bir servise referans veriyorsanız 'externalRefs' kullanın (FK üretilmez).");
                }
            }

            foreach (var (refName, externalRef) in entity.ExternalRefs)
            {
                if (string.IsNullOrWhiteSpace(externalRef.Store) || !IsValidIdentifier(externalRef.Store))
                {
                    errors.Add($"'{entityName}' externalRef '{refName}' için geçerli bir 'store' (ID alan adı) gereklidir.");
                }
            }

            var seenPublishKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kind in entity.Publishes)
            {
                if (!AllowedPublishKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"'{entityName}.publishes' geçersiz değer: '{kind}' (izinli: {string.Join(", ", AllowedPublishKinds)}).");
                }
                else if (!seenPublishKinds.Add(kind))
                {
                    errors.Add($"'{entityName}.publishes' içinde '{kind}' birden fazla kez geçiyor.");
                }
                else if (entity.AppendOnly && !string.Equals(kind, "created", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"'{entityName}' appendOnly=true iken 'publishes' içinde yalnızca 'created' olabilir (bulunan: '{kind}') — update/delete komutu üretilmediği için bu event hiç yayınlanamaz.");
                }
            }

            var seenAnonymousActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in entity.AnonymousActions)
            {
                if (!AllowedActions.Contains(action, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"'{entityName}.anonymousActions' geçersiz değer: '{action}' (izinli: {string.Join(", ", AllowedActions)}).");
                }
                else if (!seenAnonymousActions.Add(action))
                {
                    errors.Add($"'{entityName}.anonymousActions' içinde '{action}' birden fazla kez geçiyor.");
                }
                else if (entity.AppendOnly && action is "update" or "delete")
                {
                    errors.Add($"'{entityName}' appendOnly=true iken 'anonymousActions' içinde '{action}' olamaz — bu action hiç üretilmiyor.");
                }
            }

            var seenCounters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var counter in entity.Counters)
            {
                var prop = entity.Props.FirstOrDefault(p => string.Equals(p.Key, counter, StringComparison.OrdinalIgnoreCase));
                if (prop.Key is null)
                {
                    errors.Add($"'{entityName}.counters' geçersiz değer: '{counter}' — bu adda bir prop bulunamadı.");
                }
                else if (!string.Equals(prop.Value.Type, "int", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"'{entityName}.counters' içindeki '{counter}' alanı 'int' tipinde olmalı (bulunan: '{prop.Value.Type}').");
                }
                else if (!seenCounters.Add(counter))
                {
                    errors.Add($"'{entityName}.counters' içinde '{counter}' birden fazla kez geçiyor.");
                }
            }
        }

        if (spec.Subscribes is not null)
        {
            var seenHandlers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var subscribe in spec.Subscribes)
            {
                var slashIndex = subscribe.Event.IndexOf('/', StringComparison.Ordinal);
                var hasValidKindSuffix = slashIndex >= 0
                    && AllowedPublishKinds.Any(k => subscribe.Event.EndsWith(
                        char.ToUpperInvariant(k[0]) + k[1..], StringComparison.Ordinal));

                if (slashIndex <= 0 || !hasValidKindSuffix)
                {
                    errors.Add($"'{subscribe.Event}' geçersiz 'event' referansı (beklenen biçim: 'servis/EntityCreated|Updated|Deleted').");
                }

                if (!IsValidIdentifier(subscribe.Handler))
                {
                    errors.Add($"'subscribes' için geçersiz 'handler' adı: '{subscribe.Handler}' (geçerli bir C# tanımlayıcısı olmalı).");
                    continue;
                }

                if (!seenHandlers.Add(subscribe.Handler))
                {
                    errors.Add($"'subscribes' içinde '{subscribe.Handler}' handler adı birden fazla kez geçiyor (aynı serviste sınıf adı çakışır).");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Bir 'default' değerinin verilen spec tipi için geçerli olup olmadığını kontrol eder.
    /// datetime/date/guid/uuid'de literal default desteklenmez (Parse ifadesi gerektirir, kapsam dışı).
    /// </summary>
    private static bool IsValidDefault(string specType, string defaultValue) => specType.Trim().ToLowerInvariant() switch
    {
        "string" or "text" => true,
        "int" or "long" or "short" => long.TryParse(defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        "decimal" or "double" or "float" => double.TryParse(defaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        "bool" => bool.TryParse(defaultValue, out _),
        "datetime" or "date" or "guid" or "uuid" => false,
        _ => false,
    };

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || (!char.IsLetter(value[0]) && value[0] != '_'))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
