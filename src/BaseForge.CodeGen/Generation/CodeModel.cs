namespace BaseForge.CodeGen.Generation;

/// <summary>Bir entity'nin tek bir skaler kolonu (prop / FK id / dış referans id).</summary>
internal sealed class ScalarModel
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    /// <summary>Alan başlatıcısı (örn. string için <c> = string.Empty;</c>), yoksa boş.</summary>
    public string Init { get; set; } = string.Empty;
}

/// <summary>Servis içi bir ilişki için navigation property.</summary>
internal sealed class NavModel
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsCollection { get; set; }
}

/// <summary>Entity dosyası şablonu için model.</summary>
internal sealed class EntityFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<ScalarModel> Scalars { get; set; } = [];

    public List<NavModel> Navigations { get; set; } = [];
}

/// <summary>DbContext içinde bir entity referansı (DbSet üretimi için).</summary>
internal sealed class EntityRef
{
    public string Name { get; set; } = string.Empty;

    public string Plural { get; set; } = string.Empty;
}

/// <summary>DbContext dosyası şablonu için model.</summary>
internal sealed class ContextFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string ContextName { get; set; } = string.Empty;

    public List<EntityRef> Entities { get; set; } = [];
}

/// <summary>.csproj şablonu için model.</summary>
internal sealed class ProjectFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string BaseForgeVersion { get; set; } = string.Empty;
}

/// <summary>CQRS feature dosyaları (Dto/Commands/Queries) için model.</summary>
internal sealed class FeatureFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Yazılabilir alanlar (props + FK id + dış ref id). Id ve audit hariç.</summary>
    public List<ScalarModel> Fields { get; set; } = [];
}
