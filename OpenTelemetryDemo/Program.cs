using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryDemo.Controllers;
using System.Diagnostics.Metrics;

namespace OpenTelemetryDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            // Custom metrics for the application
            var greeterMeter = new Meter("OtPrGrYa.Example", "1.0.0");
            var countGreetings = greeterMeter.CreateCounter<int>("greetings.count", description: "Counts the number of greetings");
            // Custom ActivitySource for the application
            var greeterActivitySource = ServiceA.ActivitySource;

            var tracingOtlpEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];

            builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                tracing.AddEntityFrameworkCoreInstrumentation();
                tracing.AddSqlClientInstrumentation();
                tracing.AddQuartzInstrumentation();
                tracing.AddSource(builder.Environment.ApplicationName);

                tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics => metrics
                // Metrics provider from OpenTelemetry
                .AddAspNetCoreInstrumentation()
                .AddMeter(greeterMeter.Name)
                // Metrics provides by ASP.NET Core in .NET 8
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddPrometheusExporter()
            );
            // seq
            builder.Services.AddLogging(logging => logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
            {
                openTelemetryLoggerOptions.SetResourceBuilder(
                    ResourceBuilder.CreateEmpty()
                        .AddService(builder.Environment.ApplicationName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = "development"
                        }));
                openTelemetryLoggerOptions.IncludeScopes = true;
                openTelemetryLoggerOptions.IncludeFormattedMessage = true;

                openTelemetryLoggerOptions.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri("http://10.108.17.25:72/ingest/otlp/v1/logs");
                    exporter.Headers = "X-Seq-ApiKey=gx5N7vWdGTTAICN2vkiA";
                    exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }));

            builder.Services.AddSingleton<ServiceA, ServiceA>();

            var app = builder.Build();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // metric
            app.MapPrometheusScrapingEndpoint();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}