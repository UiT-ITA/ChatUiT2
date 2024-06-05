using ChatUiT2.Components;
using ChatUiT2.Services;
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
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<KeyVaultService>();

// Scoped services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthUserService>();

// Transient services
builder.Services.AddTransient<EncryptionService>();

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
