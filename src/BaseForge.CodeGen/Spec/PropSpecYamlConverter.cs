using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace BaseForge.CodeGen.Spec;

/// <summary>
/// <see cref="PropSpec"/> için özel YAML dönüştürücü. Geriye dönük uyumluluk amacıyla iki formu da
/// destekler: düz scalar (<c>total: decimal</c>) ve zengin obje (<c>total: { type: decimal, nullable: true }</c>).
/// Yazarken de aynı kurala uyar: nullable/maxLength/default hepsi varsayılandaysa düz scalar üretir.
/// </summary>
internal sealed class PropSpecYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(PropSpec);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return new PropSpec { Type = scalar.Value };
        }

        parser.Consume<MappingStart>();
        var spec = new PropSpec();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "type":
                    spec.Type = parser.Consume<Scalar>().Value;
                    break;
                case "nullable":
                    spec.Nullable = bool.Parse(parser.Consume<Scalar>().Value);
                    break;
                case "maxLength":
                    spec.MaxLength = int.Parse(parser.Consume<Scalar>().Value, CultureInfo.InvariantCulture);
                    break;
                case "default":
                    spec.Default = parser.Consume<Scalar>().Value;
                    break;
                default:
                    // Bilinmeyen anahtar — değerini (skaler veya iç içe yapı olabilir) atla.
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }

        return spec;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var spec = (PropSpec)value!;

        if (!spec.Nullable && spec.MaxLength is null && spec.Default is null)
        {
            emitter.Emit(new Scalar(spec.Type));
            return;
        }

        emitter.Emit(new MappingStart());

        emitter.Emit(new Scalar("type"));
        emitter.Emit(new Scalar(spec.Type));

        if (spec.Nullable)
        {
            emitter.Emit(new Scalar("nullable"));
            emitter.Emit(new Scalar("true"));
        }

        if (spec.MaxLength is not null)
        {
            emitter.Emit(new Scalar("maxLength"));
            emitter.Emit(new Scalar(spec.MaxLength.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (spec.Default is not null)
        {
            emitter.Emit(new Scalar("default"));
            emitter.Emit(new Scalar(spec.Default));
        }

        emitter.Emit(new MappingEnd());
    }
}
