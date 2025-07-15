using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IA.WebAPI.Models;
using IA.WebAPI.Models.Auth;
using IA.WebAPI.Options;
using IA.WebAPI.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IA.WebAPI.Tests.Helpers
{
    public class TestAuthService : IAuthService
    {
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<AuthService> _logger;
        private readonly DbContext _context;

        public TestAuthService(
            IOptions<AuthOptions> authOptions,
            ILogger<AuthService> logger,
            DbContext context)
        {
            _jwtOptions = authOptions.Value.Jwt;
            _logger = logger;
            _context = context;
        }

        public async Task<(string token, UserInfoModel userInfo)> AuthenticateUserAsync(
            string externalUserId,
            string email,
            string name,
            string? profilePictureUrl = null)
        {
            try
            {
                // Buscar si el usuario ya existe
                var existingUser = await _context.Set<User>()
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

                    _context.Set<User>().Add(existingUser);
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
                    ProfilePictureUrl = profilePictureUrl ?? existingUser.ProfilePictureUrl,
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

        public string GenerateJwtToken(UserInfoModel user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.Name ?? string.Empty),
                new Claim("InternalId", user.InternalId ?? string.Empty)
            };

            // Agregar claim para la imagen de perfil si existe
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                claims.Add(new Claim("ProfilePictureUrl", user.ProfilePictureUrl));
            }

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

                // Obtener la URL de la imagen de perfil si existe
                var profilePictureClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "ProfilePictureUrl");
                var profilePictureUrl = profilePictureClaim?.Value;

                return new UserInfoModel
                {
                    UserId = userId,
                    Email = email,
                    Name = name,
                    InternalId = internalId,
                    ProfilePictureUrl = profilePictureUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al validar token JWT");
                return null;
            }
        }

        public async Task<UserInfoModel?> GetUserByIdAsync(long userId)
        {
            try
            {
                var user = await _context.Set<User>().FindAsync(userId);

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