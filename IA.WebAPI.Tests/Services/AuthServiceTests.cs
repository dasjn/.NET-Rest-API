using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IA.WebAPI.Services;
using IA.WebAPI.Models;
using IA.WebAPI.Models.Auth;
using IA.WebAPI.Options;
using IA.WebAPI.Tests.Helpers;
using FluentAssertions;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace IA.WebAPI.Tests.Services
{
    public class AuthServiceTests : IDisposable
    {
        private readonly TestIAContext _context;
        private readonly Mock<ILogger<AuthService>> _mockLogger;
        private readonly IOptions<AuthOptions> _authOptions;
        private readonly TestAuthService _authService;

        public AuthServiceTests()
        {
            // Configurar base de datos en memoria
            var options = new DbContextOptionsBuilder<TestIAContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new TestIAContext(options);

            // Configurar mock del logger
            _mockLogger = new Mock<ILogger<AuthService>>();

            // Configurar opciones de autenticación
            var authOptionsValue = new AuthOptions
            {
                Jwt = new JwtOptions
                {
                    Key = "TestSecretKeyThatIsLongEnoughForHS256Algorithm123456789",
                    Issuer = "TestIssuer",
                    Audience = "TestAudience",
                    ExpiryInMinutes = 60
                }
            };
            _authOptions = Microsoft.Extensions.Options.Options.Create(authOptionsValue);

            // Crear instancia del servicio
            _authService = new TestAuthService(_authOptions, _mockLogger.Object, _context);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task AuthenticateUserAsync_NewUser_ShouldCreateUserAndReturnToken()
        {
            // Arrange
            var externalUserId = "google_123456";
            var email = "test@example.com";
            var name = "Test User";
            var profilePictureUrl = "https://example.com/picture.jpg";

            // Act
            var result = await _authService.AuthenticateUserAsync(externalUserId, email, name, profilePictureUrl);

            // Assert
            result.token.Should().NotBeNullOrEmpty();
            result.userInfo.Should().NotBeNull();
            result.userInfo.UserId.Should().Be(externalUserId);
            result.userInfo.Email.Should().Be(email);
            result.userInfo.Name.Should().Be(name);
            result.userInfo.ProfilePictureUrl.Should().Be(profilePictureUrl);
            result.userInfo.InternalId.Should().NotBeNullOrEmpty();

            // Verificar que el usuario se guardó en la base de datos
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.ExternalId == externalUserId);
            userInDb.Should().NotBeNull();
            userInDb!.Email.Should().Be(email);
            userInDb.Name.Should().Be(name);
            userInDb.ProfilePictureUrl.Should().Be(profilePictureUrl);
        }

        [Fact]
        public async Task AuthenticateUserAsync_ExistingUser_ShouldUpdateUserAndReturnToken()
        {
            // Arrange
            var externalUserId = "google_654321";
            var originalEmail = "old@example.com";
            var originalName = "Old Name";
            var newEmail = "new@example.com";
            var newName = "New Name";
            var newProfilePictureUrl = "https://example.com/new-picture.jpg";

            // Crear usuario existente
            var existingUser = new User
            {
                ExternalId = externalUserId,
                Email = originalEmail,
                Name = originalName,
                RegisteredDate = DateTime.UtcNow.AddDays(-30),
                LastLoginDate = DateTime.UtcNow.AddDays(-1)
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var originalLastLogin = existingUser.LastLoginDate;

            // Act
            var result = await _authService.AuthenticateUserAsync(externalUserId, newEmail, newName, newProfilePictureUrl);

            // Assert
            result.token.Should().NotBeNullOrEmpty();
            result.userInfo.Email.Should().Be(newEmail);
            result.userInfo.Name.Should().Be(newName);
            result.userInfo.ProfilePictureUrl.Should().Be(newProfilePictureUrl);

            // Verificar que el usuario se actualizó en la base de datos
            var updatedUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalId == externalUserId);
            updatedUser.Should().NotBeNull();
            updatedUser!.Email.Should().Be(newEmail);
            updatedUser.Name.Should().Be(newName);
            updatedUser.ProfilePictureUrl.Should().Be(newProfilePictureUrl);
            updatedUser.LastLoginDate.Should().BeAfter(originalLastLogin);
        }

        [Fact]
        public void GenerateJwtToken_ValidUser_ShouldReturnValidToken()
        {
            // Arrange
            var userInfo = new UserInfoModel
            {
                UserId = "test_user_123",
                Email = "test@example.com",
                Name = "Test User",
                InternalId = "1",
                ProfilePictureUrl = "https://example.com/picture.jpg"
            };

            // Act
            var token = _authService.GenerateJwtToken(userInfo);

            // Assert
            token.Should().NotBeNullOrEmpty();

            // Verificar que el token se puede decodificar
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userInfo.UserId);
            jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == userInfo.Email);
            jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == userInfo.Name);
            jwtToken.Claims.Should().Contain(c => c.Type == "InternalId" && c.Value == userInfo.InternalId);
            jwtToken.Claims.Should().Contain(c => c.Type == "ProfilePictureUrl" && c.Value == userInfo.ProfilePictureUrl);
        }

        [Fact]
        public void ValidateToken_ValidToken_ShouldReturnUserInfo()
        {
            // Arrange
            var userInfo = new UserInfoModel
            {
                UserId = "test_user_456",
                Email = "validate@example.com",
                Name = "Validate User",
                InternalId = "2",
                ProfilePictureUrl = "https://example.com/validate.jpg"
            };

            var token = _authService.GenerateJwtToken(userInfo);

            // Act
            var result = _authService.ValidateToken(token);

            // Assert
            result.Should().NotBeNull();
            result!.UserId.Should().Be(userInfo.UserId);
            result.Email.Should().Be(userInfo.Email);
            result.Name.Should().Be(userInfo.Name);
            result.InternalId.Should().Be(userInfo.InternalId);
            result.ProfilePictureUrl.Should().Be(userInfo.ProfilePictureUrl);
        }

        [Fact]
        public void ValidateToken_InvalidToken_ShouldReturnNull()
        {
            // Arrange
            var invalidToken = "invalid.token.here";

            // Act
            var result = _authService.ValidateToken(invalidToken);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateToken_EmptyToken_ShouldReturnNull()
        {
            // Act
            var result = _authService.ValidateToken(string.Empty);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateToken_NullToken_ShouldReturnNull()
        {
            // Act
            var result = _authService.ValidateToken(null!);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetUserByIdAsync_ExistingUser_ShouldReturnUserInfo()
        {
            // Arrange
            var user = new User
            {
                ExternalId = "external_789",
                Email = "getuser@example.com",
                Name = "Get User Test",
                ProfilePictureUrl = "https://example.com/getuser.jpg",
                RegisteredDate = DateTime.UtcNow,
                LastLoginDate = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _authService.GetUserByIdAsync(user.Id);

            // Assert
            result.Should().NotBeNull();
            result!.UserId.Should().Be(user.ExternalId);
            result.Email.Should().Be(user.Email);
            result.Name.Should().Be(user.Name);
            result.ProfilePictureUrl.Should().Be(user.ProfilePictureUrl);
            result.InternalId.Should().Be(user.Id.ToString());
        }

        [Fact]
        public async Task GetUserByIdAsync_NonExistingUser_ShouldReturnNull()
        {
            // Arrange
            var nonExistingUserId = 999L;

            // Act
            var result = await _authService.GetUserByIdAsync(nonExistingUserId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GenerateJwtToken_UserWithoutProfilePicture_ShouldNotIncludeProfilePictureClaim()
        {
            // Arrange
            var userInfo = new UserInfoModel
            {
                UserId = "test_user_no_pic",
                Email = "nopic@example.com",
                Name = "No Pic User",
                InternalId = "3",
                ProfilePictureUrl = null
            };

            // Act
            var token = _authService.GenerateJwtToken(userInfo);

            // Assert
            token.Should().NotBeNullOrEmpty();

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            jwtToken.Claims.Should().NotContain(c => c.Type == "ProfilePictureUrl");
        }

        [Fact]
        public async Task AuthenticateUserAsync_ExistingUserWithoutNewProfilePicture_ShouldKeepExistingProfilePicture()
        {
            // Arrange
            var externalUserId = "google_keep_pic";
            var email = "keep@example.com";
            var name = "Keep Pic User";
            var existingProfilePictureUrl = "https://example.com/existing.jpg";

            // Crear usuario existente con imagen de perfil
            var existingUser = new User
            {
                ExternalId = externalUserId,
                Email = email,
                Name = name,
                ProfilePictureUrl = existingProfilePictureUrl,
                RegisteredDate = DateTime.UtcNow.AddDays(-30),
                LastLoginDate = DateTime.UtcNow.AddDays(-1)
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            // Act - No proporcionar nueva imagen de perfil
            var result = await _authService.AuthenticateUserAsync(externalUserId, email, name, null);

            // Assert
            result.userInfo.ProfilePictureUrl.Should().Be(existingProfilePictureUrl);

            // Verificar en la base de datos
            var updatedUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalId == externalUserId);
            updatedUser!.ProfilePictureUrl.Should().Be(existingProfilePictureUrl);
        }
    }
}