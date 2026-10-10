using TipShield.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("TipShieldApi", client =>
{
    var apiUrl = builder.Configuration["API_URL"] ?? Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";
    client.BaseAddress = new Uri(apiUrl);
    var apiKey = builder.Configuration["API_KEY"] ?? Environment.GetEnvironmentVariable("API_KEY") ?? "";
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
