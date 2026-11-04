namespace BaseForge.Identity.Configuration;

/// <summary>Kimlik doğrulama şemaları.</summary>
public static class AuthSchemes
{
    /// <summary>
    /// Ortak Giriş SPA'sının çerezi ve diğer uygulamaların Bearer token'ı birlikte kabul edilir.
    /// Öznitelik argümanı derleme zamanı sabiti olmalı; OpenIddict sabiti const olmadığından literal yazılır.
    /// </summary>
    public const string CookieOrBearer = "Identity.Application,OpenIddict.Validation.AspNetCore";
}
