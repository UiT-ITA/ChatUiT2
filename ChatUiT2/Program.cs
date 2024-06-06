using ChatUiT2.Components;
using ChatUiT2.Services;
using ChatUiT2.Interfaces;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Mudblazor
builder.Services.AddMudServices();
builder.Services.AddMudMarkdownServices();

// Add authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));


// Singleton services
builder.Services.AddSingleton<IConfigService, ConfigService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();

// Scoped services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthUserService, AuthUserService>();

// Transient services
builder.Services.AddTransient<IEncryptionService, EncryptionService>();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
