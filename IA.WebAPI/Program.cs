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

app.Run();

public partial class Program
{
    // Clase pública para permitir acceso desde integration tests
}