using IA.FrontEnd;
using IA.FrontEnd.Auth;
using IA.FrontEnd.Components;
using IA.FrontEnd.PageModels.Layout;
using IA.FrontEnd.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Servicios de MudBlazor
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 200;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

// 🎯 CONFIGURACIÓN CONDICIONAL POR ENTORNO
string apiBaseUrl;
var configuredApiUrl = builder.Configuration["ApiBaseUrl"];

// Detectar si está en desarrollo (localhost)
var isDevelopment = builder.HostEnvironment.BaseAddress.Contains("localhost") ||
                   builder.HostEnvironment.BaseAddress.Contains("127.0.0.1");

if (isDevelopment)
{
    // En desarrollo: usar localhost
    apiBaseUrl = "https://localhost:7113";
    Console.WriteLine("Development mode: Using localhost API");
}
else
{
    // En producción: usar Azure
    apiBaseUrl = configuredApiUrl ?? "https://interventional-academy-api.azurewebsites.net";
    Console.WriteLine($"Production mode: Using Azure API: {apiBaseUrl}");
}

// Configuración
builder.Services.AddSingleton(provider => new ConfigurationSettings { ApiBaseUrl = apiBaseUrl });

// Configurar HttpClient con la URL detectada
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Servicios de autenticación
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthorizationCore();

// Servicios de la aplicación
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<VideoService>();
builder.Services.AddScoped<VideoInteractionService>();
builder.Services.AddScoped(typeof(InfiniteScrollService<>));

// ViewModels
builder.Services.AddSingleton<MainLayoutVM>();

await builder.Build().RunAsync();

public class ConfigurationSettings
{
    public string ApiBaseUrl { get; set; } = string.Empty;
}