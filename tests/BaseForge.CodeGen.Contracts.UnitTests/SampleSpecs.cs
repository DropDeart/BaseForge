namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>tests/.../samples/ altına kopyalanan örnek YAML dosyalarının yollarını çözer.</summary>
internal static class SampleSpecs
{
    public static string Path(string fileName) => System.IO.Path.Combine(AppContext.BaseDirectory, "samples", fileName);

    public static string Blog => Path("blog.yaml");

    public static string Orders => Path("orders.yaml");

    public static string Auth => Path("auth.yaml");
}
