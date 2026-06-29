using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BaseForge.CodeGen.Spec;

/// <summary>YAML spec dosyalarını ilgili modele yükler.</summary>
internal static class SpecLoader
{
    public static ServiceSpec Load(string path) => Load<ServiceSpec>(path);

    public static T Load<T>(string path)
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

        return deserializer.Deserialize<T>(yaml)
            ?? throw new InvalidDataException("Spec dosyası boş ya da okunamadı.");
    }
}
