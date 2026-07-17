using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Debugging;
using Serilog.Sinks.Grafana.Loki;

namespace BaseForge.API.Extensions;

/// <summary>
/// BaseForge'un merkezi loglama entegrasyonunu (Serilog + Grafana Loki) tek satırla ekleyen
/// extension metodu. <c>AddBaseForge</c>'dan (services) ayrıdır çünkü Serilog, DI seviyesinde
/// değil <see cref="WebApplicationBuilder.Host"/> seviyesinde (logging provider olarak)
/// yapılandırılır.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Serilog'u yapılandırır: her zaman konsola yapılandırılmış (structured) log yazar;
    /// <c>Serilog:LokiUrl</c> appsettings/env anahtarı doluysa aynı zamanda Grafana Loki'ye push eder.
    /// Anahtar boşsa/erişilemezse servis sadece konsola loglamaya devam eder — Loki'nin ayakta olması
    /// bir ön koşul değildir (RabbitMq'nun paylaşılan broker'a bağlanma deseniyle tutarlı). Loki sink'i
    /// içeride bir yazma hatasıyla karşılaşırsa (örn. Loki'ye erişilemiyor), <see cref="SelfLog"/>
    /// sayesinde artık tamamen sessiz kalmaz — tanılama mesajı stderr'e düşer.
    /// Her log satırı <c>Service</c> alanıyla etiketlenir; <c>CorrelationId</c> ise
    /// <c>CorrelationIdMiddleware</c>/gRPC interceptor'ları/RabbitMQ consumer'ı tarafından
    /// <c>Serilog.Context.LogContext</c> ile eklenir (bkz. <c>FromLogContext()</c>).
    /// </summary>
    /// <param name="builder">Uygulamanın host builder'ı.</param>
    /// <param name="serviceName">Logların <c>Service</c> alanında görüneceği isim (genelde servis adı).</param>
    /// <returns>Zincirleme için aynı <paramref name="builder"/>.</returns>
    public static WebApplicationBuilder AddBaseForgeLogging(this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // Serilog sink'lerinin (örn. Grafana Loki, ağ erişimi olmadan) kendi içindeki hataları
        // varsayılan olarak tamamen sessizce yutar. SelfLog bunu stderr'e görünür kılar — Loki'ye
        // yazılamadığını fark etmek artık "hiç log gelmiyor, neden?" bilmecesi olmuyor.
        SelfLog.Enable(msg => Console.Error.WriteLine($"[Serilog] {msg}"));

        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

            var lokiUrl = context.Configuration["Serilog:LokiUrl"];
            if (!string.IsNullOrWhiteSpace(lokiUrl))
            {
                loggerConfiguration.WriteTo.GrafanaLoki(
                    lokiUrl,
                    labels: [new LokiLabel { Key = "service", Value = serviceName }]);
            }
        });

        return builder;
    }
}
