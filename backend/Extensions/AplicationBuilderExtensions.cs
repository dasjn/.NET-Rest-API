using IA.WebAPI.Middleware;
using Microsoft.Extensions.FileProviders;
using IA.WebAPI.Security;

namespace IA.WebAPI.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static WebApplication ConfigureApp(this WebApplication app)
        {
            // Middleware de manejo global de excepciones
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            // Headers de seguridad
            app.UseMiddleware<SecurityHeadersMiddleware>();

            // Habilitar CORS temprano en el pipeline
            app.UseCors("AllowFrontend");

            // Forzar HTTPS
            app.UseHttpsRedirection();
            app.UseHsts();

            // Asegurar uso de HTTPS
            app.Use(async (context, next) =>
            {
                if (context.Request.Scheme != "https")
                {
                    context.Request.Scheme = "https";
                }
                await next();
            });

            // Configurar política de cookies
            app.UseCookiePolicy(new CookiePolicyOptions
            {
                MinimumSameSitePolicy = SameSiteMode.None,
                Secure = CookieSecurePolicy.Always,
                HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always,
                OnAppendCookie = ctx =>
                {
                    ctx.CookieOptions.SameSite = SameSiteMode.None;
                    ctx.CookieOptions.Secure = true;
                    ctx.CookieOptions.HttpOnly = true;
                    ctx.CookieOptions.IsEssential = true;
                }
            });

            // Configurar Swagger solo en entorno de desarrollo
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IA API v1");
                    c.RoutePrefix = "swagger"; // Mantener la ruta estándar de Swagger
                });
            }

            // Configurar archivos estáticos
            app.ConfigureStaticFiles();

            // Middleware de autenticación y autorización
            app.UseAuthentication();
            app.UseAuthorization();

            // Mapear controladores
            app.MapControllers();

            return app;
        }

        public static WebApplication ConfigureStaticFiles(this WebApplication app)
        {
            // Solo configurar archivos estáticos si NO estamos usando Azure Storage
            var useAzureStorage = app.Configuration.GetValue<bool>("AzureStorage:UseAzureStorage");

            if (!useAzureStorage)
            {
                // Solo crear el proveedor de archivos físicos si usamos almacenamiento local
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

                // Crear directorio si no existe (solo para almacenamiento local)
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var frontendUrl = app.Configuration["Authentication:FrontendBaseUrl"] ?? "https://localhost:44337";

                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(uploadsPath),
                    RequestPath = "/Uploads",
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream",
                    OnPrepareResponse = ctx =>
                    {
                        var headers = ctx.Context.Response.Headers;
                        headers.AccessControlAllowOrigin = frontendUrl;
                        headers.AccessControlAllowMethods = "GET, OPTIONS";
                        headers.AccessControlAllowHeaders = "Content-Type";
                        headers["Cross-Origin-Resource-Policy"] = "cross-origin";
                    }
                });
            }
            // Si usamos Azure Storage, no configuramos archivos estáticos locales

            return app;
        }
    }
}