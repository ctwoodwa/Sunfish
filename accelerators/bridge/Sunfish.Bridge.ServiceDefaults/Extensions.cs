using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

public static class SunfishBridgeServiceDefaultsExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureSerilog();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Serilog is the logging provider for all Bridge projects. The OpenTelemetry
        // sink ships logs to whatever OTLP endpoint Aspire injects, so they show up
        // in the Aspire dashboard alongside traces and metrics.
        var serviceName = builder.Environment.ApplicationName;
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var otlpHeaders = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];

        builder.Logging.ClearProviders();

        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service.name", serviceName)
            .WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            loggerConfig.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
                options.Protocol = OtlpProtocol.Grpc;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                };
                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    foreach (var header in otlpHeaders.Split(','))
                    {
                        var parts = header.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            options.Headers[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
            });
        }

        Log.Logger = loggerConfig.CreateLogger();
        builder.Services.AddSerilog(Log.Logger, dispose: true);

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Logs are handled by Serilog's OpenTelemetry sink (see ConfigureSerilog).
        // OpenTelemetry SDK here covers traces and metrics only.
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
