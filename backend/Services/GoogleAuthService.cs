using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using IA.WebAPI.Models.Auth;
using IA.WebAPI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IA.WebAPI.Services
{
    /// <summary>
    /// Interfaz para el servicio de autenticación con Google
    /// </summary>
    public interface IGoogleAuthService
    {
        /// <summary>
        /// Genera la URL de autorización de Google
        /// </summary>
        /// <param name="state">Estado OAuth para seguridad</param>
        /// <param name="redirectUri">URI de redirección después de autenticación</param>
        /// <returns>URL de autorización</returns>
        string BuildAuthorizationUrl(string state, string redirectUri);

        /// <summary>
        /// Intercambia un código de autorización por un token de acceso
        /// </summary>
        /// <param name="code">Código de autorización</param>
        /// <param name="redirectUri">URI de redirección</param>
        /// <returns>Respuesta con token de acceso</returns>
        Task<GoogleTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri);

        /// <summary>
        /// Obtiene información del usuario usando un token de acceso
        /// </summary>
        /// <param name="accessToken">Token de acceso</param>
        /// <returns>Información del usuario</returns>
        Task<GoogleUserInfoResponse> GetUserInfoAsync(string accessToken);
    }

    /// <summary>
    /// Implementación del servicio de autenticación con Google
    /// </summary>
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly GoogleAuthOptions _googleOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(
            IOptions<AuthOptions> authOptions,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleAuthService> logger)
        {
            _googleOptions = authOptions.Value.Google;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string BuildAuthorizationUrl(string state, string redirectUri)
        {
            try
            {
                // URL de autorización de Google con los parámetros requeridos
                var googleAuthUrl = $"{_googleOptions.AuthorizationEndpoint}" +
                    $"?client_id={Uri.EscapeDataString(_googleOptions.ClientId)}" +
                    "&response_type=code" +
                    $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                    $"&state={Uri.EscapeDataString(state)}" +
                    "&scope=openid%20email%20profile";

                _logger.LogDebug("URL de autorización generada: {Url}", googleAuthUrl);
                return googleAuthUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar URL de autorización");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<GoogleTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("GoogleAuth");

                var tokenRequest = new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _googleOptions.ClientId,
                    ["client_secret"] = _googleOptions.ClientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                };

                _logger.LogDebug("Solicitando token con código: {CodeStart}...", code.Substring(0, Math.Min(code.Length, 10)));

                var response = await httpClient.PostAsync(
                    _googleOptions.TokenEndpoint,
                    new FormUrlEncodedContent(tokenRequest));

                response.EnsureSuccessStatusCode();

                var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>()
                    ?? throw new InvalidOperationException("No se pudo deserializar la respuesta de token");

                _logger.LogInformation("Token obtenido exitosamente de Google");
                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intercambiar código por token");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<GoogleUserInfoResponse> GetUserInfoAsync(string accessToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("GoogleAuth");
                var userInfoUrl = $"{_googleOptions.UserInfoEndpoint}?access_token={Uri.EscapeDataString(accessToken)}";

                _logger.LogDebug("Solicitando información de usuario con token");

                var response = await httpClient.GetAsync(userInfoUrl);
                response.EnsureSuccessStatusCode();

                var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfoResponse>()
                    ?? throw new InvalidOperationException("No se pudo deserializar la información del usuario");

                _logger.LogInformation("Información de usuario obtenida: {Email}", userInfo.Email);
                return userInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información de usuario");
                throw;
            }
        }
    }
}