using System.Text.Json;

namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>Yetkilendirme modeli (access / ownerField / defaultAccess / superRoles) — bkz. docs/ARCH.md §6.1.</summary>
public class AccessSpecTests
{
    private const string AccessYaml =
        """
        service: shop
        database: shop_db
        auth:
          authority: http://localhost:8081
          defaultAccess: authenticated
          superRoles: [SuperAdmin]
        entities:
          Order:
            props:
              BuyerId: guid
              Total: decimal
            ownerField: BuyerId
            access:
              list: [Admin, owner]
              getById: [Admin, owner]
              create: authenticated
              update: [Admin, owner]
              delete: [Admin]
          Product:
            props:
              Name: string
            access:
              list: anonymous
              getById: anonymous
        """;

    [Fact]
    public void Load_AccessBlock_ParsesKeywordsAndRoleLists()
    {
        var spec = LoadFromString(AccessYaml);

        Assert.True(spec.Auth!.DefaultAccess!.IsAuthenticated);
        Assert.Equal(["SuperAdmin"], spec.Auth.SuperRoles);

        var order = spec.Entities["Order"];
        Assert.Equal("BuyerId", order.OwnerField);
        Assert.True(order.Access["create"].IsAuthenticated);
        Assert.True(order.Access["update"].IncludesOwner);
        Assert.Equal(["Admin"], order.Access["update"].Roles);
        Assert.False(order.Access["delete"].IncludesOwner);
        Assert.True(spec.Entities["Product"].Access["list"].IsAnonymous);

        Assert.Empty(SpecValidator.Validate(spec));
    }

    [Fact]
    public void AccessRule_JsonRoundTrip_KeepsStringOrArrayShape()
    {
        var keyword = JsonSerializer.Serialize(AccessRule.Keyword("anonymous"));
        var list = JsonSerializer.Serialize(new AccessRule { Values = ["Admin", "owner"] });

        Assert.Equal("\"anonymous\"", keyword);
        Assert.Equal("[\"Admin\",\"owner\"]", list);
        Assert.True(JsonSerializer.Deserialize<AccessRule>(keyword)!.IsAnonymous);
        Assert.True(JsonSerializer.Deserialize<AccessRule>(list)!.IncludesOwner);
    }

    [Fact]
    public void Validate_OwnerWithoutOwnerField_ReturnsError()
    {
        var spec = Spec(new EntitySpec
        {
            Props = { ["Title"] = new PropSpec { Type = "string" } },
            Access = { ["update"] = Roles("Admin", "owner") },
        });

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("'ownerField' tanımlanmalı", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OwnerOnCreate_ReturnsError()
    {
        var spec = Spec(OwnedEntity(("create", Roles("owner"))));

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("create'te henüz bir sahip yoktur", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("string", false)]
    [InlineData("guid", true)]
    public void Validate_OwnerFieldMustBeNonNullableGuid(string type, bool nullable)
    {
        var entity = new EntitySpec
        {
            Props = { ["AuthorId"] = new PropSpec { Type = type, Nullable = nullable } },
            OwnerField = "AuthorId",
        };

        Assert.Contains(SpecValidator.Validate(Spec(entity)), e => e.Contains("nullable olmayan 'guid'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AccessAndAnonymousActionsTogether_ReturnsError()
    {
        var entity = OwnedEntity(("list", AccessRule.Keyword("anonymous")));
        entity.AnonymousActions.Add("getById");

        Assert.Contains(SpecValidator.Validate(Spec(entity)), e => e.Contains("birlikte kullanılamaz", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_KeywordInsideRoleList_ReturnsError()
    {
        var spec = Spec(OwnedEntity(("list", Roles("Admin", "anonymous"))));

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("rol listesi içinde kullanılamaz", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_UnknownActionAndInvalidRoleName_ReturnErrors()
    {
        var spec = Spec(OwnedEntity(("publish", Roles("Admin")), ("list", Roles("Site Admin"))));

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("geçersiz action: 'publish'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("geçersiz rol adı: 'Site Admin'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AccessOnAppendOnlyUpdate_ReturnsError()
    {
        var entity = OwnedEntity(("update", Roles("Admin")));
        entity.AppendOnly = true;

        Assert.Contains(SpecValidator.Validate(Spec(entity)), e => e.Contains("appendOnly=true iken 'access'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AccessWithoutAuthBlock_ReturnsError()
    {
        var spec = Spec(OwnedEntity(("list", Roles("Admin"))));
        spec.Auth = null;

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("'auth' bloğu gerekir", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DefaultAccessWithOwnerOrProtectFalse_ReturnsErrors()
    {
        var spec = Spec(OwnedEntity());
        spec.Auth!.Protect = false;
        spec.Auth.DefaultAccess = Roles("Admin", "owner");

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("'auth.protect: false' birlikte", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'auth.defaultAccess' — 'owner' burada kullanılamaz", StringComparison.Ordinal));
    }

    private static AccessRule Roles(params string[] values) => new() { Values = [.. values] };

    private static EntitySpec OwnedEntity(params (string Action, AccessRule Rule)[] access)
    {
        var entity = new EntitySpec
        {
            Props = { ["AuthorId"] = new PropSpec { Type = "guid" } },
            OwnerField = "AuthorId",
        };
        foreach (var (action, rule) in access)
        {
            entity.Access[action] = rule;
        }

        return entity;
    }

    private static ServiceSpec Spec(EntitySpec entity) => new()
    {
        Service = "s",
        Database = "s_db",
        Auth = new ServiceAuthSpec { Authority = "http://localhost:8081" },
        Entities = { ["Post"] = entity },
    };

    private static ServiceSpec LoadFromString(string yaml)
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, yaml);
            return SpecLoader.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
