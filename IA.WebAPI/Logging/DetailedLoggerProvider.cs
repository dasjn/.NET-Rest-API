// Actualizar Logging/DetailedLoggerProvider.cs para corregir los warnings

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IA.WebAPI.Logging
{
    [ProviderAlias("DetailedFile")]
    public class DetailedLoggerProvider : ILoggerProvider
    {
        // Marcar como nullable para resolver CS8618
        private readonly IDisposable? _onChangeToken;
        private DetailedLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, DetailedLogger> _loggers = new();
        private readonly string _path;

        public DetailedLoggerProvider(IOptionsMonitor<DetailedLoggerConfiguration> config)
        {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
            _path = _currentConfig.LogFilePath ?? "logs/webapi-.log";

            // Crear directorio si no existe
            var logDirectory = Path.GetDirectoryName(_path);

            // Corregir CS8601: Verificar explícitamente que logDirectory no es null
            if (!string.IsNullOrEmpty(logDirectory))
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
            }
            else
            {
                // Si logDirectory es null, usar directorio actual
                _path = Path.Combine(Directory.GetCurrentDirectory(), _path);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new DetailedLogger(name, _currentConfig, _path));
        }

        public void Dispose()
        {
            _loggers.Clear();
            _onChangeToken?.Dispose(); // Usar el operador ?. para manejar posibles nulos
        }
    }

    public class DetailedLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly DetailedLoggerConfiguration _config;
        private readonly string _path;

        public DetailedLogger(string categoryName, DetailedLoggerConfiguration config, string path)
        {
            _categoryName = categoryName;
            _config = config;
            _path = path;
        }

        // Implementación explícita para resolver el warning CS8633
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        // Implementación de la clase para mantener coherencia interna
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _config.MinLevel;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string formattedMessage = formatter(state, exception);
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string threadId = Environment.CurrentManagedThreadId.ToString();
            string logLevelString = GetLogLevelString(logLevel);
            string message = $"[{timestamp}] [{threadId}] [{logLevelString}] [{_categoryName}] {formattedMessage}";

            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }

            string filePath = _path.Replace(".log", $"{DateTime.UtcNow:yyyyMMdd}.log");

            // Escribir en archivo con bloqueo para prevenir problemas de concurrencia
            lock (this)
            {
                try
                {
                    File.AppendAllText(filePath, message + Environment.NewLine);
                }
                catch
                {
                    // Silenciar errores de escritura para evitar fallos en cascada
                }
            }
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRIT",
                _ => "NONE"
            };
        }
    }

    public class DetailedLoggerConfiguration
    {
        public LogLevel MinLevel { get; set; } = LogLevel.Information;
        public string LogFilePath { get; set; } = "logs/webapi-.log"; // Valor por defecto para evitar nullables
        public bool IncludeScopes { get; set; } = true;
        public bool IncludeSource { get; set; } = true;
    }
}