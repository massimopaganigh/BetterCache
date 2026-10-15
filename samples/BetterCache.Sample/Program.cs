using BetterCache;
using BetterCache.Sample.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Options also bind from the "BetterCache" section of appsettings.json.
builder.Services.AddBetterCache();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();
else
    app.UseExceptionHandler("/Error");

app.UseAntiforgery();

app.UseBetterCache();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BetterCache.Sample.Client._Imports).Assembly);

app.Run();
