// Program.cs actualizado
using IA.WebAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configuración de logging con los parámetros correctos
builder.Logging.ConfigureLogging(builder.Environment, builder.Configuration);

// Registrar servicios para la inyección de dependencias
builder.Services.AddAppServices(builder.Configuration);

// Construir la aplicación
var app = builder.Build();

// Configurar el pipeline de solicitudes HTTP
app.ConfigureApp();

app.UseDeveloperExceptionPage(); // Muestra errores detallados

// Añade esto después de app.UseRouting() pero antes de app.UseEndpoints()
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
        throw; // Vuelve a lanzar la excepción para que la vea el usuario
    }
});

app.Run();