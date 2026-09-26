using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace BaseForge.CodeGen.Contracts;

/// <summary>
/// Bir entity action'ına kimin erişebileceği (bkz. <see cref="EntitySpec.Access"/>, docs/ARCH.md §6.1).
/// YAML/JSON'da iki biçimde yazılır: tek anahtar kelime (<c>anonymous</c>, <c>authenticated</c>) veya
/// rol listesi (<c>[Admin, Editor]</c>); listede özel <c>owner</c> değeri kaydın sahibini de ekler.
/// </summary>
[JsonConverter(typeof(AccessRuleJsonConverter))]
public sealed class AccessRule
{
    /// <summary>Herkese açık erişim anahtar kelimesi.</summary>
    public const string Anonymous = "anonymous";

    /// <summary>Giriş yapmış herkese açık erişim anahtar kelimesi.</summary>
    public const string Authenticated = "authenticated";

    /// <summary>Rol listesi içinde "kaydın sahibi" anlamına gelen özel değer.</summary>
    public const string Owner = "owner";

    /// <summary>Yazıldığı haliyle değerler (anahtar kelime tek başına ya da rol/owner listesi).</summary>
    public List<string> Values { get; set; } = [];

    /// <summary>Kural <c>anonymous</c> mı?</summary>
    public bool IsAnonymous => IsKeyword(Anonymous);

    /// <summary>Kural <c>authenticated</c> mı?</summary>
    public bool IsAuthenticated => IsKeyword(Authenticated);

    /// <summary>Kural bir rol/owner listesi mi (anahtar kelime değil)?</summary>
    public bool IsRoleList => !IsAnonymous && !IsAuthenticated;

    /// <summary>Listede <c>owner</c> var mı?</summary>
    public bool IncludesOwner => IsRoleList && Values.Contains(Owner, StringComparer.OrdinalIgnoreCase);

    /// <summary>Listedeki rol adları (<c>owner</c> hariç).</summary>
    public IReadOnlyList<string> Roles => IsRoleList
        ? Values.Where(v => !string.Equals(v, Owner, StringComparison.OrdinalIgnoreCase)).ToList()
        : [];

    /// <summary>Tek bir anahtar kelimeden kural oluşturur.</summary>
    public static AccessRule Keyword(string keyword) => new() { Values = [keyword] };

    private bool IsKeyword(string keyword) =>
        Values.Count == 1 && string.Equals(Values[0], keyword, StringComparison.OrdinalIgnoreCase);
}

/// <summary><see cref="AccessRule"/> için YAML dönüştürücü: scalar (<c>anonymous</c>) veya sequence (<c>[Admin, owner]</c>).</summary>
public sealed class AccessRuleYamlConverter : IYamlTypeConverter
{
    /// <summary>Bu dönüştürücünün yalnızca <see cref="AccessRule"/> tipini işlediğini belirtir.</summary>
    public bool Accepts(Type type) => type == typeof(AccessRule);

    /// <summary>Bir <see cref="AccessRule"/>'u YAML'dan okur.</summary>
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return AccessRule.Keyword(scalar.Value);
        }

        parser.Consume<SequenceStart>();
        var rule = new AccessRule();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            rule.Values.Add(parser.Consume<Scalar>().Value);
        }

        return rule;
    }

    /// <summary>Bir <see cref="AccessRule"/>'u YAML'a yazar (anahtar kelime ise scalar, değilse akış listesi).</summary>
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var rule = (AccessRule)value!;
        if (!rule.IsRoleList)
        {
            emitter.Emit(new Scalar(rule.Values[0]));
            return;
        }

        emitter.Emit(new SequenceStart(null, null, isImplicit: true, SequenceStyle.Flow));
        foreach (var v in rule.Values)
        {
            emitter.Emit(new Scalar(v));
        }

        emitter.Emit(new SequenceEnd());
    }
}

/// <summary><see cref="AccessRule"/> için JSON dönüştürücü (Designer API'si): string veya string dizisi.</summary>
public sealed class AccessRuleJsonConverter : JsonConverter<AccessRule>
{
    /// <inheritdoc />
    public override AccessRule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return AccessRule.Keyword(reader.GetString()!);
        }

        var values = JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? [];
        return new AccessRule { Values = values };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AccessRule value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (!value.IsRoleList)
        {
            writer.WriteStringValue(value.Values[0]);
            return;
        }

        JsonSerializer.Serialize(writer, value.Values, options);
    }
}
