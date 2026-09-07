using HackerNewsApi.Options;
using HackerNewsApi.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<HackerNewsOptions>()
    .Bind(builder.Configuration.GetSection(HackerNewsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.RequestTimeout;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HackerNewsApi/1.0");
});

// Typed clients are transient. Keep the service scoped so it does not capture
// a single HttpClient for the process lifetime. The refresh lock stays singleton
// so concurrent requests still share one upstream refresh.
builder.Services.AddSingleton<BestStoriesRefreshLock>();
builder.Services.AddScoped<IBestStoriesService, BestStoriesService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.Run();

public partial class Program;
