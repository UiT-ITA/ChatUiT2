using ChatUiT2.Components;
using ChatUiT2.Services;
using ChatUiT2.Interfaces;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Identity.Web.UI;
using ChatUiT2.Services.Template;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddMicrosoftIdentityConsentHandler();

// Add logging
//builder.Services.AddLogging();
builder.Services.AddSingleton<ITelemetryInitializer, MyTelemetryInitializer>();
builder.Services.AddApplicationInsightsTelemetry();

//builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["ApplicationInsights:ConnectionString"]);


// Add authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

//builder.Services.AddServerSideBlazor()
//    .AddMicrosoftIdentityConsentHandler();

// Handle signalR buffer problems
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
        options.HandshakeTimeout = TimeSpan.FromSeconds(120);
    });


// Mudblazor
builder.Services.AddMudServices();
builder.Services.AddMudMarkdownServices();

// Singleton services
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<AdminService>();
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    string connectionString = configuration.GetConnectionString("RagProjectDef") ?? string.Empty;
    if(string.IsNullOrEmpty(connectionString))
    {
        ILogger logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogError("CosmosDB connection string is not set. Failed to register Cosmos db client");
        return null!;
    } else
    {
        return new CosmosClient(connectionString);
    }
});
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddSingleton<IRagDatabaseService, RagDatabaseServiceCosmosDbNoSql>();

// Scoped services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IChatToolsService, ChatToolsService>();
builder.Services.AddScoped<IUsernameService, UsernameService>();
builder.Services.AddScoped<IAuthUserService, AuthUserService>();
builder.Services.AddScoped<IUpdateService, UpdateService>();
builder.Services.AddScoped<SpeechService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<IRagSearchService, RagSearchService>();
builder.Services.AddScoped<IRagGeneratorService, RagGeneratorService>();
//builder.Services.AddScoped<IOpenAIService, OpenAIService>();
builder.Services.AddTransient<IDateTimeProvider, DateTimeProvider>();

// Mediatr for communication between services on for instance updateStream
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ChatService).Assembly));

// Transient services

builder.Services.AddTransient<TestService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
