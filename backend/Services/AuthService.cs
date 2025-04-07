using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using IA.WebAPI.Models;
using IA.WebAPI.Models.Auth;
using IA.WebAPI.Options;
using Microsoft.EntityFrameworkCore;
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
        /// Genera un token JWT para el usuario y actualiza su registro en la base de datos
        /// </summary>
        /// <param name="externalUserId">ID externo del usuario</param>
        /// <param name="email">Email del usuario</param>
        /// <param name="name">Nombre del usuario</param>
        /// <param name="profilePictureUrl">URL de imagen de perfil (opcional)</param>
        /// <returns>Token JWT y modelo con datos del usuario</returns>
        Task<(string token, UserInfoModel userInfo)> AuthenticateUserAsync(
            string externalUserId,
            string email,
            string name,
            string? profilePictureUrl = null);

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

        /// <summary>
        /// Obtiene información del usuario por su ID
        /// </summary>
        /// <param name="userId">ID interno del usuario</param>
        /// <returns>Modelo con información del usuario o null si no existe</returns>
        Task<UserInfoModel?> GetUserByIdAsync(long userId);
    }

    /// <summary>
    /// Implementación del servicio de autenticación
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<AuthService> _logger;
        private readonly IAContext _context;

        public AuthService(
            IOptions<AuthOptions> authOptions,
            ILogger<AuthService> logger,
            IAContext context)
        {
            _jwtOptions = authOptions.Value.Jwt;
            _logger = logger;
            _context = context;
        }

        /// <inheritdoc/>
        public async Task<(string token, UserInfoModel userInfo)> AuthenticateUserAsync(
            string externalUserId,
            string email,
            string name,
            string? profilePictureUrl = null)
        {
            try
            {
                // Buscar si el usuario ya existe
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.ExternalId == externalUserId);

                if (existingUser == null)
                {
                    // Crear nuevo usuario si no existe
                    existingUser = new User
                    {
                        ExternalId = externalUserId,
                        Email = email,
                        Name = name,
                        ProfilePictureUrl = profilePictureUrl,
                        RegisteredDate = DateTime.UtcNow,
                        LastLoginDate = DateTime.UtcNow
                    };

                    _context.Users.Add(existingUser);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Nuevo usuario registrado: {Email} (ID: {Id})", email, existingUser.Id);
                }
                else
                {
                    // Actualizar información del usuario existente
                    existingUser.Name = name;
                    existingUser.Email = email;
                    existingUser.LastLoginDate = DateTime.UtcNow;

                    // Actualizar imagen de perfil solo si se proporciona una nueva
                    if (!string.IsNullOrEmpty(profilePictureUrl))
                    {
                        existingUser.ProfilePictureUrl = profilePictureUrl;
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Usuario existente autenticado: {Email} (ID: {Id})", email, existingUser.Id);
                }

                // Crear modelo de información de usuario
                var userInfo = new UserInfoModel
                {
                    UserId = externalUserId,
                    Email = email,
                    Name = name,
                    ProfilePictureUrl = profilePictureUrl,
                    InternalId = existingUser.Id.ToString()
                };

                // Generar token JWT
                var token = GenerateJwtToken(userInfo);

                return (token, userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al autenticar usuario: {Message}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public string GenerateJwtToken(UserInfoModel user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.Name ?? string.Empty),
                new Claim("InternalId", user.InternalId ?? string.Empty),
                new Claim("ProfilePictureUrl", user.ProfilePictureUrl ?? string.Empty)
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

                // Obtener el ID interno si existe
                var internalIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "InternalId");
                var internalId = internalIdClaim?.Value;

                return new UserInfoModel
                {
                    UserId = userId,
                    Email = email,
                    Name = name,
                    InternalId = internalId
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al validar token JWT");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<UserInfoModel?> GetUserByIdAsync(long userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return null;
                }

                return new UserInfoModel
                {
                    UserId = user.ExternalId,
                    Email = user.Email,
                    Name = user.Name,
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    InternalId = user.Id.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario por ID: {Message}", ex.Message);
                return null;
            }
        }
    }
}