using IA.FrontEnd;
using IA.FrontEnd.Auth;
using IA.FrontEnd.PageModels.Layout;
using IA.FrontEnd.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
// Configure HttpClient to use the base URL of your API
builder.Services.AddScoped(sp => new HttpClient{BaseAddress = new Uri("https://localhost:7113/") });

// Registrar el servicio de autenticación personalizado
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<AuthService>();

// VideoService
builder.Services.AddScoped<VideoService>();

// Agregar la autenticación
builder.Services.AddAuthorizationCore();

// Mudblazor library
builder.Services.AddMudServices();

// Viewmodels register
builder.Services.AddSingleton<MainLayoutVM>();

await builder.Build().RunAsync();

