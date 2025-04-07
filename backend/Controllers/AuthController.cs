using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using IA.WebAPI.Models.Auth;
using IA.WebAPI.Options;
using IA.WebAPI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IA.WebAPI.Controllers
{
    /// <summary>
    /// Controlador para la autenticación y autorización
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IOAuthStateService _stateService;
        private readonly AuthOptions _authOptions;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Constructor del controlador de autenticación
        /// </summary>
        public AuthController(
            IAuthService authService,
            IGoogleAuthService googleAuthService,
            IOAuthStateService stateService,
            IOptions<AuthOptions> authOptions,
            ILogger<AuthController> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _googleAuthService = googleAuthService ?? throw new ArgumentNullException(nameof(googleAuthService));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
            _authOptions = authOptions.Value ?? throw new ArgumentNullException(nameof(authOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Inicia el proceso de autenticación con Google
        /// </summary>
        /// <remarks>
        /// Redirige al usuario a la página de autenticación de Google
        /// </remarks>
        /// <response code="302">Redirige a Google para autenticación</response>
        /// <response code="400">Error en la configuración o solicitud</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpGet("google-login")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GoogleLogin()
        {
            try
            {
                // Generar un estado OAuth para seguridad CSRF
                var state = _stateService.CreateState();

                // Construir la URL de redirección para el callback
                var callbackUrl = $"https://{Request.Host}{_authOptions.Google.CallbackPath}";

                // Construir la URL de autorización de Google
                var authUrl = _googleAuthService.BuildAuthorizationUrl(state, callbackUrl);

                _logger.LogInformation("Iniciando autenticación con Google, redirigiendo al usuario");
                return Redirect(authUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar autenticación con Google");
                return RedirectToLoginFailed("error_iniciando_autenticacion", ex.Message);
            }
        }

        /// <summary>
        /// Procesa la respuesta de Google después de la autenticación
        /// </summary>
        /// <param name="state">Estado OAuth para validación</param>
        /// <param name="code">Código de autorización</param>
        /// <param name="error">Error (si existe) de Google</param>
        /// <response code="302">Redirige al frontend con token (éxito) o error (fallo)</response>
        [HttpGet("google-callback")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public async Task<IActionResult> GoogleCallback(
            [FromQuery] string state,
            [FromQuery] string? code,
            [FromQuery] string? error)
        {
            _logger.LogInformation("Recibido callback de Google. State: {State}, Code: {CodePresent}, Error: {Error}",
                state, !string.IsNullOrEmpty(code), error);

            // Verificar si hay error explícito de Google
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Error recibido de Google: {Error}", error);
                return RedirectToLoginFailed("error_de_google", error);
            }

            try
            {
                // Verificar el estado OAuth para prevenir CSRF
                if (!_stateService.ValidateState(state))
                {
                    _logger.LogWarning("Estado OAuth inválido o expirado");
                    return RedirectToLoginFailed("estado_invalido", "El estado de autenticación es inválido o ha expirado");
                }

                // Verificar código de autorización
                if (string.IsNullOrEmpty(code))
                {
                    _logger.LogWarning("Código de autorización ausente");
                    return RedirectToLoginFailed("codigo_ausente", "No se recibió código de autorización");
                }

                // URL de redirección para intercambio de token
                var redirectUri = $"https://{Request.Host}{_authOptions.Google.CallbackPath}";

                // Intercambiar código por token de acceso
                var tokenResponse = await _googleAuthService.ExchangeCodeForTokenAsync(code, redirectUri);

                // Obtener información del usuario
                var userInfo = await _googleAuthService.GetUserInfoAsync(tokenResponse.AccessToken);

                // Crear claims para autenticación
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userInfo.Id),
                    new Claim(ClaimTypes.Email, userInfo.Email),
                    new Claim(ClaimTypes.Name, userInfo.Name),
                    new Claim(ClaimTypes.GivenName, userInfo.GivenName),
                    new Claim(ClaimTypes.Surname, userInfo.FamilyName)
                };

                var claimsIdentity = new ClaimsIdentity(claims, "Google");
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                // Iniciar sesión con cookies
                await HttpContext.SignInAsync("ApplicationCookie", claimsPrincipal, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
                });

                // Generar token JWT
                var (jwtToken, userInfoModel) = await _authService.AuthenticateUserAsync(
                    userInfo.Id,
                    userInfo.Email,
                    userInfo.Name,
                    userInfo.Picture);
                _logger.LogInformation("Usuario {Email} autenticado exitosamente", userInfo.Email);

                // Redireccionar al frontend con el token
                return RedirectToLoginCallback(jwtToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el callback de Google");
                return RedirectToLoginFailed("error_procesando_autenticacion", ex.Message);
            }
        }

        /// <summary>
        /// Obtiene información del usuario autenticado
        /// </summary>
        /// <returns>Información del usuario actual</returns>
        /// <response code="200">Información del usuario</response>
        /// <response code="401">Usuario no autenticado</response>
        [Authorize]
        [HttpGet("user-info")]
        [ProducesResponseType(typeof(UserInfoModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<UserInfoModel> GetUserInfo()
        {
            return Ok(new UserInfoModel
            {
                UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Email = User.FindFirst(ClaimTypes.Email)?.Value,
                Name = User.FindFirst(ClaimTypes.Name)?.Value
            });
        }

        /// <summary>
        /// Proporciona información de diagnóstico para depuración
        /// </summary>
        /// <returns>Información de diagnóstico</returns>
        /// <response code="200">Información de diagnóstico</response>
        [HttpGet("debug-info")]
        [ApiExplorerSettings(IgnoreApi = true)] // Ocultar del Swagger
        public IActionResult GetDebugInfo()
        {
            // Recopilar información de diagnóstico de manera segura
            var cookies = Request.Cookies.Select(c => new
            {
                Name = c.Key,
                // Solo mostrar los primeros caracteres por seguridad
                Value = c.Value.Length > 5 ? c.Value.Substring(0, 5) + "..." : "***"
            }).ToList();

            var headers = Request.Headers.Select(h => new
            {
                Name = h.Key,
                Value = h.Value.ToString()
            }).ToList();

            return Ok(new
            {
                RequestPath = Request.Path.ToString(),
                RequestScheme = Request.Scheme,
                Host = Request.Host.ToString(),
                Cookies = cookies,
                Headers = headers,
                AuthenticationScheme = User.Identity?.AuthenticationType,
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false
            });
        }

        #region Helpers

        /// <summary>
        /// Redirecciona al frontend con un token JWT
        /// </summary>
        private IActionResult RedirectToLoginCallback(string token)
        {
            // Decodificar el token para obtener información adicional si es necesario
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Obtener claims importantes
            var email = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var internalId = jwtToken.Claims.FirstOrDefault(c => c.Type == "InternalId")?.Value;

            // Construir URL de callback con información adicional
            var callbackUrl = new UriBuilder($"{_authOptions.FrontendBaseUrl}{_authOptions.LoginCallbackPath}");

            var query = HttpUtility.ParseQueryString(callbackUrl.Query);
            query["token"] = Uri.EscapeDataString(token);
            query["email"] = Uri.EscapeDataString(email ?? "");
            query["name"] = Uri.EscapeDataString(name ?? "");
            query["internalId"] = Uri.EscapeDataString(internalId ?? "");

            callbackUrl.Query = query.ToString();

            _logger.LogDebug("Redirigiendo a callback de login: {CallbackUrl}", callbackUrl.ToString());
            return Redirect(callbackUrl.ToString());
        }

        /// <summary>
        /// Redirecciona al frontend con información de error
        /// </summary>
        private IActionResult RedirectToLoginFailed(string errorCode, string? details = null)
        {
            var url = $"{_authOptions.FrontendBaseUrl}{_authOptions.LoginFailedPath}?error={Uri.EscapeDataString(errorCode)}";

            if (!string.IsNullOrEmpty(details))
            {
                url += $"&details={Uri.EscapeDataString(details)}";
            }

            _logger.LogDebug("Redirigiendo a página de error: {FailedUrl}", url);
            return Redirect(url);
        }

        /// <summary>
        /// Refresca el token JWT
        /// </summary>
        [HttpPost("refresh-token")]
        public IActionResult RefreshToken([FromBody] string token)
        {
            try
            {
                // Validate the existing token
                var userInfo = _authService.ValidateToken(token);

                if (userInfo == null)
                    return Unauthorized();

                // Generate a new token
                var newToken = _authService.GenerateJwtToken(userInfo);
                return Ok(newToken);
            }
            catch
            {
                return Unauthorized();
            }
        }
    }
}

#endregion