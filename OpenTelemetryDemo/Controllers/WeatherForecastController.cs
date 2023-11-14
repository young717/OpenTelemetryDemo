using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace OpenTelemetryDemo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly ServiceA serviceA;
        private readonly IWebHostEnvironment webHostEnvironment;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, ServiceA serviceA, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            this.serviceA = serviceA;
            this.webHostEnvironment = webHostEnvironment;
        }
        private static readonly ActivitySource RegisteredActivity = new ActivitySource("Examples.ManualInstrumentations.Registered");

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            using var activity2 = ServiceA.ActivitySource.StartActivity("AAA");
            activity2?.AddTag("AA", "GetApi");
            var name = Guid.NewGuid();
            _logger.LogInformation($"{name}");
            // A span
            using var activity = ServiceA.ActivitySource.StartActivity("Call to Service B");
            activity?.AddTag("Path", "GetApi");

            serviceA.GetName(name.ToString());
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }

    public class ServiceA
    {
        private readonly ILogger logger;

        public ServiceA(ILogger<ServiceA> logger)
        {
            this.logger = logger;
        }
        public static readonly ActivitySource ActivitySource = new ActivitySource("AAAA");

        private static readonly TextMapPropagator Propagator = new OpenTelemetry.Extensions.Propagators.B3Propagator();
        public string GetName(string name)
        {
            // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#span-name
            var activityName = $"send";

            using var activity = Activity.Current ?? ActivitySource.StartActivity(activityName, ActivityKind.Producer);

            ActivityContext contextToInject = default;
            if (activity != null)
            {
                contextToInject = activity.Context;
            }
            else if (Activity.Current != null)
            {
                contextToInject = Activity.Current.Context;
            }
            Propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), new Dictionary<string, string>() { { "X-B3-Flags", "1" }, { "X-B3-TraceId", Guid.NewGuid().ToString() }, { "X-B3-SpanId", Guid.NewGuid().ToString() } }, InjectTraceContextIntoBasicProperties);

            var parentContext = Propagator.Extract(new PropagationContext(contextToInject, Baggage.Current), new Dictionary<string, string>() { { "X-B3-Flags", "1" }, { "X-B3-TraceId", Guid.NewGuid().ToString() }, { "X-B3-SpanId", Guid.NewGuid().ToString() } }, this.ExtractTraceContextFromBasicProperties);

            Baggage.Current = parentContext.Baggage;

            var activityName1 = $"receive";
            using var activity1 = ActivitySource.StartActivity(activityName1, ActivityKind.Consumer, parentContext.ActivityContext);
            activity1?.AddTag("Path", "GetName function");
            logger.LogWarning($"²ÎÊýÊÇ{name}");
            activity1?.Stop();
            return name;
        }

        private void InjectTraceContextIntoBasicProperties(Dictionary<string, string> headers, string key, string value)
        {
            try
            {
                if (headers == null)
                {
                    headers = new Dictionary<string, string>();
                }

                headers[key] = value;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to inject trace context.");
            }
        }

        private IEnumerable<string> ExtractTraceContextFromBasicProperties(Dictionary<string, string> headers, string key)
        {
            try
            {
                if (headers.TryGetValue(key, out var value))
                {
                    return new[] { value };
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to extract trace context.");
            }

            return Enumerable.Empty<string>();
        }
    }
}