using IA.WebAPI.Options;

namespace IA.WebAPI.Extensions
{
    public static class ConfigurationExtensions
    {
        public static AuthOptions GetAuthOptions(this IConfiguration configuration)
        {
            var authOptions = configuration.GetSection(AuthOptions.SectionName)
                .Get<AuthOptions>() ?? new AuthOptions();

            // Verificar y cargar secretos desde variables de entorno si están disponibles
            LoadSecretFromEnvironment(configuration, "JWT_KEY", value => authOptions.Jwt.Key = value);
            LoadSecretFromEnvironment(configuration, "GOOGLE_CLIENT_SECRET", value => authOptions.Google.ClientSecret = value);

            ValidateAuthOptions(authOptions);

            return authOptions;
        }

        private static void LoadSecretFromEnvironment(IConfiguration configuration, string envVarName, Action<string> setValue)
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(envValue))
            {
                setValue(envValue);
            }
        }

        private static void ValidateAuthOptions(AuthOptions options)
        {
            var errors = new List<string>();

            // Validar JWT
            if (string.IsNullOrEmpty(options.Jwt.Key))
                errors.Add("JWT Key no está configurada");
            else if (options.Jwt.Key.Length < 32)
                errors.Add("JWT Key debe tener al menos 32 caracteres");

            if (string.IsNullOrEmpty(options.Jwt.Issuer))
                errors.Add("JWT Issuer no está configurado");

            if (string.IsNullOrEmpty(options.Jwt.Audience))
                errors.Add("JWT Audience no está configurado");

            // Validar Google Auth (solo si está en uso)
            if (!string.IsNullOrEmpty(options.Google.ClientId))
            {
                if (string.IsNullOrEmpty(options.Google.ClientSecret))
                    errors.Add("Google ClientSecret no está configurado");

                if (string.IsNullOrEmpty(options.Google.CallbackPath))
                    errors.Add("Google CallbackPath no está configurado");
            }

            if (errors.Any())
            {
                throw new InvalidOperationException(
                    $"Configuración de autenticación inválida: {string.Join(", ", errors)}");
            }
        }
    }
}