using BaseForge.CodeGen.Contracts;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BaseForge.CodeGen.Designer;

/// <summary>
/// <see cref="SpecLoader"/>'ın tersi: spec nesnesini YAML metnine çevirir ve diske yazar.
/// Loader ile aynı camelCase convention'ı kullanır (round-trip garantisi).
/// </summary>
internal static class YamlSpecWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new PropSpecYamlConverter())
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public static string ToYaml<T>(T spec) => Serializer.Serialize(spec!);

    /// <summary>Spec'i <paramref name="outputDir"/>/<c>spec.yaml</c> olarak yazar; tam yolu döndürür.</summary>
    public static string Write<T>(T spec, string outputDir, string fileName = "spec.yaml")
    {
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, fileName);
        File.WriteAllText(path, ToYaml(spec));
        return path;
    }
}
