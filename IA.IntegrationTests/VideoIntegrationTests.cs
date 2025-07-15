using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using IA.WebAPI.Models.DTOs;
using IA.WebAPI.Models;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace IA.IntegrationTests;

public class VideoIntegrationTests : IntegrationTestBase
{
    public VideoIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task VideoUploadFlow_Integration_ShouldWorkEndToEnd()
    {
        // Arrange
        await ClearDatabaseAsync();
        var token = await CreateAuthenticatedUserAsync();
        SetAuthorizationHeader(token);

        // Crear datos de video falsos
        var videoData = Encoding.UTF8.GetBytes("fake video content for testing");
        var videoContent = new ByteArrayContent(videoData);
        videoContent.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");

        var formData = new MultipartFormDataContent
        {
            { new StringContent("Integration Test Video"), "name" },
            { new StringContent("Video subido durante test de integración"), "description" },
            { videoContent, "file", "test-video.mp4" }
        };

        // Act
        var response = await _client.PostAsync("/api/videos", formData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var videoResponse = JsonSerializer.Deserialize<VideoDto>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        videoResponse.Should().NotBeNull();
        videoResponse!.Name.Should().Be("Integration Test Video");
        videoResponse.Description.Should().Be("Video subido durante test de integración");
        videoResponse.Uri.Should().NotBeNullOrEmpty();
        videoResponse.UploadedByUserId.Should().BeGreaterThan(0);

        // Verificar que el video se guardó en la base de datos
        var videoInDb = _context.Videos.FirstOrDefault(v => v.Id == videoResponse.Id);
        videoInDb.Should().NotBeNull();
        videoInDb!.Name.Should().Be("Integration Test Video");
    }

    [Fact]
    public async Task VideoPlaybackFlow_Integration_ShouldReturnPlayableVideo()
    {
        // Arrange
        await ClearDatabaseAsync();
        var token = await CreateAuthenticatedUserAsync();

        var user = _context.Users.First();
        var video = new Video
        {
            Id = 1,
            Name = "Test Playback Video",
            Description = "Video para probar reproducción",
            Uri = "/uploads/videos/test-playback.mp4",
            ThumbnailUri = "/uploads/thumbnails/test-playback.webp",
            PublishDate = DateTime.UtcNow,
            UploadedByUserId = user.Id
        };

        _context.Videos.Add(video);
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/videos/{video.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var videoResponse = JsonSerializer.Deserialize<VideoDto>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        videoResponse.Should().NotBeNull();
        videoResponse!.Id.Should().Be(video.Id);
        videoResponse.Name.Should().Be("Test Playback Video");
        videoResponse.Uri.Should().Be("/uploads/videos/test-playback.mp4");
        videoResponse.ThumbnailUri.Should().Be("/uploads/thumbnails/test-playback.webp");
        videoResponse.ViewsCount.Should().Be(0);
        videoResponse.LikesCount.Should().Be(0);
    }

    [Fact]
    public async Task AuthenticationFlow_Integration_ShouldWorkEndToEnd()
    {
        // Arrange
        await ClearDatabaseAsync();
        var token = await CreateAuthenticatedUserAsync("auth-test@example.com", "Auth Test User");

        // Act - Hacer request a endpoint protegido
        SetAuthorizationHeader(token);
        var response = await _client.GetAsync("/api/videos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var videos = JsonSerializer.Deserialize<VideoDto[]>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        videos.Should().NotBeNull();
        videos!.Should().BeEmpty(); // No hay videos en la BD limpia
    }

    [Fact]
    public async Task SearchVideos_Integration_ShouldReturnCorrectResults()
    {
        // Arrange
        await ClearDatabaseAsync();
        var token = await CreateAuthenticatedUserAsync();
        var user = _context.Users.First();

        // Crear videos de prueba
        var videos = new[]
        {
            new Video
            {
                Name = "JavaScript Tutorial",
                Description = "Aprende JavaScript básico",
                Uri = "/uploads/videos/js-tutorial.mp4",
                ThumbnailUri = "/uploads/thumbnails/js-tutorial.webp",
                PublishDate = DateTime.UtcNow.AddDays(-1),
                UploadedByUserId = user.Id
            },
            new Video
            {
                Name = "Python Programming",
                Description = "Introducción a Python",
                Uri = "/uploads/videos/python-intro.mp4",
                ThumbnailUri = "/uploads/thumbnails/python-intro.webp",
                PublishDate = DateTime.UtcNow.AddDays(-2),
                UploadedByUserId = user.Id
            },
            new Video
            {
                Name = "React Components",
                Description = "Cómo crear componentes en React",
                Uri = "/uploads/videos/react-components.mp4",
                ThumbnailUri = "/uploads/thumbnails/react-components.webp",
                PublishDate = DateTime.UtcNow.AddDays(-3),
                UploadedByUserId = user.Id
            }
        };

        _context.Videos.AddRange(videos);
        await _context.SaveChangesAsync();

        // Act - Buscar videos con "JavaScript"
        var searchResponse = await _client.GetAsync("/api/videos?search=JavaScript");

        // Assert
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchContent = await searchResponse.Content.ReadAsStringAsync();
        var searchResults = JsonSerializer.Deserialize<VideoDto[]>(searchContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        searchResults.Should().NotBeNull();
        searchResults!.Should().HaveCount(1);
        searchResults!.First().Name.Should().Be("JavaScript Tutorial");

        // Act - Obtener todos los videos (sin filtro)
        var allResponse = await _client.GetAsync("/api/videos");
        var allContent = await allResponse.Content.ReadAsStringAsync();
        var allVideos = JsonSerializer.Deserialize<VideoDto[]>(allContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        allVideos.Should().NotBeNull();
        allVideos!.Should().HaveCount(3);
        allVideos.Should().BeInDescendingOrder(v => v.PublishDate); // Ordenados por fecha descendente
    }
}