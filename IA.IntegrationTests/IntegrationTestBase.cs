using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using IA.WebAPI.Models;
using IA.WebAPI.Models.Auth;
using IA.WebAPI.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;

namespace IA.IntegrationTests;

public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> _factory;
    protected readonly HttpClient _client;
    protected readonly IServiceScope _scope;
    protected readonly IAContext _context;
    protected readonly IAuthService _authService;

    public IntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        // Configurar variable de entorno antes de crear la factory
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Remover el DbContext existente
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<IAContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Remover cualquier registro directo del IAContext
                var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAContext));
                if (contextDescriptor != null)
                {
                    services.Remove(contextDescriptor);
                }

                // Registrar DbContext completamente nuevo para testing
                services.AddDbContext<IAContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                           .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                }, ServiceLifetime.Scoped);
            });
        });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IAContext>();
        _authService = _scope.ServiceProvider.GetRequiredService<IAuthService>();

        // Crear la base de datos sin llamar a EnsureCreated
        // La base de datos InMemory se crea automáticamente en la primera operación
    }

    protected async Task<string> CreateAuthenticatedUserAsync(string email = "test@example.com", string name = "Test User")
    {
        var user = new User
        {
            Email = email,
            Name = name,
            ExternalId = Guid.NewGuid().ToString(),
            ProfilePictureUrl = "https://example.com/profile.jpg",
            RegisteredDate = DateTime.UtcNow,
            LastLoginDate = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var userInfo = new UserInfoModel
        {
            UserId = user.ExternalId,
            InternalId = user.Id.ToString(),
            Email = user.Email,
            Name = user.Name,
            ProfilePictureUrl = user.ProfilePictureUrl
        };

        var token = _authService.GenerateJwtToken(userInfo);
        return token;
    }

    protected void SetAuthorizationHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected async Task<HttpResponseMessage> PostJsonAsync<T>(string url, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync(url, content);
    }

    protected async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    protected async Task ClearDatabaseAsync()
    {
        // Usar una nueva transacción para asegurar la limpieza
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.VideoInteractions.RemoveRange(_context.VideoInteractions);
            _context.VideoComments.RemoveRange(_context.VideoComments);
            _context.Videos.RemoveRange(_context.Videos);
            _context.Users.RemoveRange(_context.Users);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _scope?.Dispose();
        _client?.Dispose();
    }
}