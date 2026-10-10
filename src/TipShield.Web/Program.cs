using TipShield.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("TipShieldApi", client =>
{
    var apiUrl = builder.Configuration["API_URL"] ?? Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";
    client.BaseAddress = new Uri(apiUrl);
    var apiKey = builder.Configuration["API_KEY"] ?? Environment.GetEnvironmentVariable("API_KEY") ?? "test-api-key";
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("TipShieldApi"));

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
