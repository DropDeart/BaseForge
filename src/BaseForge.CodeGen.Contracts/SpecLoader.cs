using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BaseForge.CodeGen.Contracts;

/// <summary>YAML spec dosyalarını ilgili modele yükler.</summary>
public static class SpecLoader
{
    /// <summary>Bir <see cref="ServiceSpec"/>'i verilen YAML dosyasından yükler.</summary>
    public static ServiceSpec Load(string path) => Load<ServiceSpec>(path);

    /// <summary>Verilen YAML dosyasını <typeparamref name="T"/> tipine yükler (ör. <see cref="ServiceSpec"/>, <see cref="AuthSpec"/>).</summary>
    public static T Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Spec dosyası bulunamadı: {path}", path);
        }

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new PropSpecYamlConverter())
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<T>(yaml)
            ?? throw new InvalidDataException("Spec dosyası boş ya da okunamadı.");
    }
}
