using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaseForge.Identity.Configuration;

/// <summary>auth.yaml ile açılıp kapanan özellikler.</summary>
public enum AuthFeature
{
    /// <summary>Kiracı (şirket) yönetimi.</summary>
    MultiTenancy,

    /// <summary>Tek kullanımlık kod (SMS/e-posta).</summary>
    Otp,
}

/// <summary>Özellik kapalıysa uç yokmuş gibi 404 döner.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireFeatureAttribute(AuthFeature feature) : Attribute, IResourceFilter
{
    /// <inheritdoc />
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.HttpContext.RequestServices.GetRequiredService<AuthOptions>();
        var enabled = feature switch
        {
            AuthFeature.MultiTenancy => options.MultiTenancy.Enabled,
            AuthFeature.Otp => options.Otp.Enabled,
            _ => false,
        };
        if (!enabled)
        {
            context.Result = new NotFoundResult();
        }
    }

    /// <inheritdoc />
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
