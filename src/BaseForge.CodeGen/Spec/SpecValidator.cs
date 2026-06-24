namespace BaseForge.CodeGen.Spec;

/// <summary>Bir <see cref="ServiceSpec"/>'i kod/diyagram üretiminden önce doğrular.</summary>
internal static class SpecValidator
{
    private static readonly string[] AllowedKinds = ["one-to-many", "many-to-one", "one-to-one"];

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

            foreach (var (propName, propType) in entity.Props)
            {
                if (!IsValidIdentifier(propName))
                {
                    errors.Add($"'{entityName}.{propName}' geçersiz alan adı.");
                }

                if (!TypeMap.IsKnown(propType))
                {
                    errors.Add($"'{entityName}.{propName}' bilinmeyen tip: '{propType}'.");
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
        }

        return errors;
    }

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
