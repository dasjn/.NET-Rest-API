// Actualizar Security/SecurityHeadersMiddleware.cs para permitir que Swagger funcione
namespace IA.WebAPI.Security
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public SecurityHeadersMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _next = next;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // No aplicar estas cabeceras a la ruta de Swagger en entorno de desarrollo
            bool isSwaggerRequest = context.Request.Path.StartsWithSegments("/swagger");

            if (!isSwaggerRequest || !_environment.IsDevelopment())
            {
                IHeaderDictionary headers = context.Response.Headers;

                // Agregar headers de seguridad estándar
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["X-XSS-Protection"] = "1; mode=block";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";

                // Configuración de Content-Security-Policy
                var frontendUrl = _configuration["Authentication:FrontendBaseUrl"] ?? "https://localhost:44337";
                headers["Content-Security-Policy"] =
                    $"default-src 'self'; " +
                    $"img-src 'self' data:; " +
                    $"font-src 'self'; " +
                    $"style-src 'self' 'unsafe-inline'; " +
                    $"script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // Añadido 'unsafe-inline' y 'unsafe-eval'
                    $"frame-ancestors 'none'; " +
                    $"connect-src 'self' {frontendUrl}; " +
                    $"object-src 'none'";
            }

            await _next(context);
        }
    }
}