using NBomber.CSharp;
using NBomber.Http.CSharp;

// Smoke load test against the running PmDemo web service.
// Usage: dotnet run --project Marilo.PmDemo.Tests.Performance -- https://localhost:7101
var baseUrl = args.Length > 0 ? args[0] : "https://localhost:7101";

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

var scenario = Scenario.Create("health_endpoint", async _ =>
    {
        var request = Http.CreateRequest("GET", "/health");
        return await Http.Send(http, request);
    })
    .WithoutWarmUp()
    .WithLoadSimulations(Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)));

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();
