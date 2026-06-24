using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BaseForge.CodeGen.Spec;

/// <summary>YAML servis spec dosyalarını <see cref="ServiceSpec"/> nesnesine yükler.</summary>
internal static class SpecLoader
{
    public static ServiceSpec Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Spec dosyası bulunamadı: {path}", path);
        }

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var spec = deserializer.Deserialize<ServiceSpec>(yaml)
            ?? throw new InvalidDataException("Spec dosyası boş ya da okunamadı.");

        return spec;
    }
}
