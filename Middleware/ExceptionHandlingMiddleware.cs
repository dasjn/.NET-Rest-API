// Actualización de Middleware/ExceptionHandlingMiddleware.cs con mensajes en inglés
using System.Net;
using System.Text.Json;

namespace IA.WebAPI.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = HttpStatusCode.InternalServerError;
            var errorMessage = "An unexpected error occurred.";
            var stackTrace = string.Empty;

            // Determinar el código de estado HTTP según el tipo de excepción
            switch (exception)
            {
                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Unauthorized;
                    errorMessage = "You are not authorized to access this resource.";
                    break;

                case ArgumentException or FormatException:
                    statusCode = HttpStatusCode.BadRequest;
                    errorMessage = exception.Message;
                    break;

                case InvalidOperationException:
                    statusCode = HttpStatusCode.BadRequest;
                    errorMessage = exception.Message;
                    break;

                case KeyNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    errorMessage = "The requested resource was not found.";
                    break;

                case TimeoutException:
                    statusCode = HttpStatusCode.RequestTimeout;
                    errorMessage = "The operation has timed out.";
                    break;

                default:
                    // Para excepciones no controladas explícitamente
                    _logger.LogError(exception, "Unhandled error: {Message}", exception.Message);
                    break;
            }

            // Incluir stack trace en entorno de desarrollo
            if (_environment.IsDevelopment())
            {
                stackTrace = exception.StackTrace ?? string.Empty;
            }

            // Registrar el error
            _logger.LogError(exception, "{StatusCode} Error: {Message}", (int)statusCode, errorMessage);

            // Configurar la respuesta HTTP
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            // Crear objeto de respuesta de error
            var errorResponse = new
            {
                status = (int)statusCode,
                message = errorMessage,
                stackTrace = _environment.IsDevelopment() ? stackTrace : null,
                path = context.Request.Path,
                timestamp = DateTime.UtcNow
            };

            // Serializar y devolver la respuesta
            var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            return context.Response.WriteAsync(result);
        }
    }
}