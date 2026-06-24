namespace BaseForge.CodeGen.Generation;

/// <summary>Scriban kod üretim şablonları (gömülü).</summary>
internal static class Templates
{
    public const string Entity =
        """
        using BaseForge.Core.Entities;

        namespace {{ Namespace }}.Entities;

        /// <summary>{{ Name }} entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
        public sealed class {{ Name }} : BaseEntity
        {
        {{~ for p in Scalars ~}}
            /// <summary>{{ p.Name }}.</summary>
            public {{ p.Type }} {{ p.Name }} { get; set; }{{ p.Init }}
        {{~ end ~}}
        {{~ for n in Navigations ~}}
        {{~ if n.IsCollection ~}}
            /// <summary>{{ n.Name }} (servis içi ilişki).</summary>
            public ICollection<{{ n.Type }}> {{ n.Name }} { get; } = [];
        {{~ else ~}}
            /// <summary>{{ n.Name }} (servis içi ilişki).</summary>
            public {{ n.Type }}? {{ n.Name }} { get; set; }
        {{~ end ~}}
        {{~ end ~}}
        }

        """;

    public const string DbContext =
        """
        using BaseForge.Infrastructure.Data;
        using Microsoft.EntityFrameworkCore;
        using {{ Namespace }}.Entities;

        namespace {{ Namespace }}.Data;

        /// <summary>{{ ServiceName }} servisinin EF Core context'i.</summary>
        public sealed class {{ ContextName }} : BaseForgeDbContext
        {
            /// <summary>Yeni bir {{ ContextName }} oluşturur.</summary>
            public {{ ContextName }}(DbContextOptions<{{ ContextName }}> options)
                : base(options)
            {
            }

        {{~ for e in Entities ~}}
            /// <summary>{{ e.Name }} tablosu.</summary>
            public DbSet<{{ e.Name }}> {{ e.Plural }} => Set<{{ e.Name }}>();
        {{~ end ~}}
        }

        """;

    public const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{{ Namespace }}</RootNamespace>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="BaseForge.Infrastructure" Version="{{ BaseForgeVersion }}" />
          </ItemGroup>

        </Project>

        """;
}
