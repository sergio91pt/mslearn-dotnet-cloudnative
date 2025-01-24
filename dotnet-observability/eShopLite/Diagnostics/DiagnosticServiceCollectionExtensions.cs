using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

public static class DiagnosticServiceCollectionExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services,
        string serviceName,
        IConfiguration configuration,
        string[]? meeterNames = null)
    {
        // create the resource that references the service name passed in
        var resource = ResourceBuilder.CreateDefault().AddService(serviceName: serviceName, serviceVersion: "1.0");

        // add the OpenTelemetry services
        var otelBuilder = services.AddOpenTelemetry();

        Action<OtlpExporterOptions> configureOtelExporter = otlp =>
        {
            otlp.Endpoint = new Uri(configuration["OTLP_ENDPOINT"] ?? "http://aspire:18889");
            otlp.Protocol = OtlpExportProtocol.Grpc;
        };

        otelBuilder
            // add the metrics providers
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resource)
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEventCountersInstrumentation(c =>
                    {
                        c.AddEventSources(
                            "Microsoft.AspNetCore.Hosting",
                            "Microsoft-AspNetCore-Server-Kestrel",
                            "System.Net.Http",
                            "System.Net.Sockets");
                    })
                    .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel")
                    .AddPrometheusExporter()
                    .AddOtlpExporter(configureOtelExporter);
                
                meeterNames?.ToList().ForEach(name =>
                {
                    metrics.AddMeter(name);
                });
            })
            // add the tracing providers
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resource)
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddZipkinExporter(zipkin =>
                    {
                        var zipkinUrl = configuration["ZIPKIN_URL"] ?? "http://zipkin:9411";
                        zipkin.Endpoint = new Uri($"{zipkinUrl}/api/v2/spans");
                    })
                    .AddOtlpExporter(configureOtelExporter);
            })
            .WithLogging(logging =>
            {
                logging.SetResourceBuilder(resource)
                    .AddOtlpExporter(configureOtelExporter);
            });

        return services;
    }
    
    public static void MapObservability(this IEndpointRouteBuilder routes)
    {
        routes.MapPrometheusScrapingEndpoint();
    }
}