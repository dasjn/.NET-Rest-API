using IA.WebAPI.Logging;

namespace IA.WebAPI.Extensions
{
    public static class LoggingExtensions
    {
        public static ILoggingBuilder ConfigureLogging(this ILoggingBuilder logging, IHostEnvironment environment, IConfiguration configuration)
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();

            // Agregar proveedor de log detallado
            logging.AddDetailedLogger(configuration);

            // Configurar nivel de log y filtros según el entorno
            if (environment.IsDevelopment())
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
                logging.AddFilter("IA.WebAPI", LogLevel.Debug);
            }
            else
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                logging.AddFilter("IA.WebAPI", LogLevel.Information);
            }

            return logging;
        }

        public static ILoggingBuilder AddDetailedLogger(this ILoggingBuilder builder, IConfiguration configuration)
        {
            builder.Services.Configure<DetailedLoggerConfiguration>(configuration.GetSection("Logging:DetailedLogger"));
            builder.Services.AddSingleton<ILoggerProvider, DetailedLoggerProvider>();
            return builder;
        }
    }
}