using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IA.WebAPI.Models.Auth;
using IA.WebAPI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IA.WebAPI.Services
{
    /// <summary>
    /// Interfaz para el servicio de autenticación
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Genera un token JWT para el usuario
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="email">Email del usuario</param>
        /// <param name="name">Nombre del usuario</param>
        /// <returns>Token JWT</returns>
        string GenerateJwtToken(string userId, string email, string name);

        /// <summary>
        /// Genera un token JWT a partir de la información del usuario
        /// </summary>
        /// <param name="user">Modelo con información del usuario</param>
        /// <returns>Token JWT</returns>
        string GenerateJwtToken(UserInfoModel user);

        /// <summary>
        /// Valida un token JWT
        /// </summary>
        /// <param name="token">Token JWT a validar</param>
        /// <returns>Información de usuario si el token es válido, null si no</returns>
        UserInfoModel? ValidateToken(string token);
    }

    /// <summary>
    /// Implementación del servicio de autenticación
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IOptions<AuthOptions> authOptions, ILogger<AuthService> logger)
        {
            _jwtOptions = authOptions.Value.Jwt;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string GenerateJwtToken(string userId, string email, string name)
        {
            var user = new UserInfoModel
            {
                UserId = userId,
                Email = email,
                Name = name
            };

            return GenerateJwtToken(user);
        }

        /// <inheritdoc/>
        public string GenerateJwtToken(UserInfoModel user)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId ?? string.Empty),
                    new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                    new Claim(ClaimTypes.Name, user.Name ?? string.Empty)
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryInMinutes);

                var token = new JwtSecurityToken(
                    issuer: _jwtOptions.Issuer,
                    audience: _jwtOptions.Audience,
                    claims: claims,
                    expires: expires,
                    signingCredentials: creds
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar token JWT");
                throw;
            }
        }

        /// <inheritdoc/>
        public UserInfoModel? ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtOptions.Key);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _jwtOptions.Issuer,
                    ValidAudience = _jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;

                var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
                var email = jwtToken.Claims.First(x => x.Type == ClaimTypes.Email).Value;
                var name = jwtToken.Claims.First(x => x.Type == ClaimTypes.Name).Value;

                return new UserInfoModel
                {
                    UserId = userId,
                    Email = email,
                    Name = name
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al validar token JWT");
                return null;
            }
        }
    }
}