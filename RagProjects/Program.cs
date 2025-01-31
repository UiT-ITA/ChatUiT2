using RagProjects.Components;
using MudBlazor.Services;
using UiT.RagProjects.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using MudBlazor;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Extensions.DependencyInjection;
using UiT.RestClientTopdesk.Services;
using UiT.RestClientTopdesk.Extension;
using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using UiT.CommonToolsLib.Services;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();

// Authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddMemoryCache();

builder.Services.AddMudServices();

builder.Services.AddScoped<MyUIManager>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor()
    .AddMicrosoftIdentityConsentHandler();
builder.Services.AddApplicationInsightsTelemetry();

// Topdesk client
builder.Services.AddUitTopdeskClient(new()
{
    ApiKey = builder.Configuration["TopdeskApi:ApiKey"] ?? string.Empty,
    ApiKeyHeaderName = builder.Configuration["TopdeskApi:ApiKeyHeaderName"] ?? string.Empty,
    BaseUrl = builder.Configuration["TopdeskApi:BaseUrl"] ?? string.Empty,
    Timeout = builder.Configuration["TopdeskApi:Timeout"] ?? string.Empty
}
);

builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddSingleton<IRagTopdeskDatabaseService, RagTopdeskDatabaseService>();

builder.Services.AddTransient<IKnowledgeItemService, KnowledgeItemService>();
builder.Services.AddTransient<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddTransient<IConfigService, ConfigService>();
builder.Services.AddTransient<IKnowledgeItemService, KnowledgeItemService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
