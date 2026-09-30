using System.Net.Http.Headers;
using LokiLogViewer.Client.Pages;
using LokiLogViewer.Components;
using LokiLogViewer.Endpoints;
using LokiLogViewer.Loki;
using LokiLogViewer.Search;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddOptions<LokiOptions>()
    .Bind(builder.Configuration.GetSection(LokiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<LokiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<LokiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    if (!string.IsNullOrEmpty(options.OrgId))
    {
        client.DefaultRequestHeaders.Add("X-Scope-OrgID", options.OrgId);
    }
    if (!string.IsNullOrEmpty(options.BearerToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
    }
});

builder.Services.AddScoped<LogSearchService>();
builder.Services.ConfigureHttpJsonOptions(options => LokiLogViewer.Shared.Serialization.JsonDefaults.Apply(options.SerializerOptions));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(LokiLogViewer.Client._Imports).Assembly);

app.MapLogsEndpoints();

app.Run();
