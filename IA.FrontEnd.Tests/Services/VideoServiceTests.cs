using Microsoft.Extensions.Configuration;
using IA.FrontEnd.Models;
using IA.FrontEnd.Tests.Helpers;
using FluentAssertions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace IA.FrontEnd.Tests.Services
{
    public class VideoServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ITestAuthStateProvider> _mockAuthStateProvider;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly TestVideoService _videoService;

        public VideoServiceTests()
        {
            // Configurar mock del HttpMessageHandler
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://localhost:7113")
            };

            // Configurar mock del AuthStateProvider
            _mockAuthStateProvider = new Mock<ITestAuthStateProvider>();

            // Configurar mock de la configuración
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["ApiBaseUrl"]).Returns("https://localhost:7113");

            // Crear instancia del servicio
            _videoService = new TestVideoService(
                _httpClient,
                _mockAuthStateProvider.Object,
                _mockConfiguration.Object);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        [Fact]
        public async Task UploadVideoAsync_WithValidData_ShouldReturnSuccess()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake video data");
            var fileName = "test-video.mp4";
            var title = "Test Video";
            var description = "Test Description";
            var token = "valid-jwt-token";

            var expectedVideo = new Video
            {
                Id = 1,
                Name = title,
                Description = description,
                Uri = "/uploads/videos/test-video.mp4",
                ThumbnailUri = "/uploads/thumbnails/test-video-thumb.jpg",
                PublishDate = DateTime.UtcNow
            };

            // Configurar mock del auth provider para devolver token válido
            _mockAuthStateProvider.Setup(x => x.GetTokenAsync())
                .ReturnsAsync(token);

            // Configurar mock de la respuesta HTTP
            var jsonResponse = JsonSerializer.Serialize(expectedVideo);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains("/api/Videos")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description);

            // Assert
            result.Success.Should().BeTrue();
            result.Video.Should().NotBeNull();
            result.Video!.Id.Should().Be(expectedVideo.Id);
            result.Video.Name.Should().Be(expectedVideo.Name);
            result.Video.Description.Should().Be(expectedVideo.Description);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task UploadVideoAsync_WithThumbnail_ShouldIncludeThumbnailInRequest()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake video data");
            var thumbnailData = Encoding.UTF8.GetBytes("fake thumbnail data");
            var fileName = "test-video.mp4";
            var thumbnailFileName = "test-thumbnail.jpg";
            var title = "Test Video";
            var description = "Test Description";
            var token = "valid-jwt-token";

            var expectedVideo = new Video
            {
                Id = 1,
                Name = title,
                Description = description,
                Uri = "/uploads/videos/test-video.mp4",
                ThumbnailUri = "/uploads/thumbnails/test-thumbnail.jpg",
                PublishDate = DateTime.UtcNow
            };

            _mockAuthStateProvider.Setup(x => x.GetTokenAsync())
                .ReturnsAsync(token);

            var jsonResponse = JsonSerializer.Serialize(expectedVideo);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description,
                thumbnailData: thumbnailData,
                thumbnailFileName: thumbnailFileName);

            // Assert
            result.Success.Should().BeTrue();
            result.Video.Should().NotBeNull();
            result.Video!.ThumbnailUri.Should().Be(expectedVideo.ThumbnailUri);
        }

        [Fact]
        public async Task UploadVideoAsync_WithEmptyVideoData_ShouldReturnFailure()
        {
            // Arrange
            var videoData = Array.Empty<byte>();
            var fileName = "test-video.mp4";
            var title = "Test Video";
            var description = "Test Description";

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description);

            // Assert
            result.Success.Should().BeFalse();
            result.Video.Should().BeNull();
            result.ErrorMessage.Should().Be("No video data to upload.");
        }

        [Fact]
        public async Task UploadVideoAsync_WithEmptyTitle_ShouldReturnFailure()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake video data");
            var fileName = "test-video.mp4";
            var title = "";
            var description = "Test Description";

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description);

            // Assert
            result.Success.Should().BeFalse();
            result.Video.Should().BeNull();
            result.ErrorMessage.Should().Be("Video must have a title and description.");
        }

        [Fact]
        public async Task UploadVideoAsync_WithEmptyDescription_ShouldReturnFailure()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake video data");
            var fileName = "test-video.mp4";
            var title = "Test Video";
            var description = "";

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description);

            // Assert
            result.Success.Should().BeFalse();
            result.Video.Should().BeNull();
            result.ErrorMessage.Should().Be("Video must have a title and description.");
        }

        [Fact]
        public async Task UploadVideoAsync_WithoutAuthToken_ShouldReturnFailure()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake video data");
            var fileName = "test-video.mp4";
            var title = "Test Video";
            var description = "Test Description";

            _mockAuthStateProvider.Setup(x => x.GetTokenAsync())
                .ReturnsAsync(string.Empty);

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description);

            // Assert
            result.Success.Should().BeFalse();
            result.Video.Should().BeNull();
            result.ErrorMessage.Should().Be("Authentication token is missing.");
        }

        [Fact]
        public async Task UploadVideoAsync_WithHttpError_ShouldReturnFailure()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake video data");
            var fileName = "test-video.mp4";
            var title = "Test Video";
            var description = "Test Description";
            var token = "valid-jwt-token";

            _mockAuthStateProvider.Setup(x => x.GetTokenAsync())
                .ReturnsAsync(token);

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Bad Request", Encoding.UTF8, "text/plain")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description);

            // Assert
            result.Success.Should().BeFalse();
            result.Video.Should().BeNull();
            result.ErrorMessage.Should().Contain("Upload failed: BadRequest");
        }

        [Fact]
        public void FormatVideoUrl_WithAbsoluteUrl_ShouldReturnUnchanged()
        {
            // Arrange
            var absoluteUrl = "https://example.com/video.mp4";

            // Act
            var result = _videoService.FormatVideoUrl(absoluteUrl);

            // Assert
            result.Should().Be(absoluteUrl);
        }

        [Fact]
        public void FormatVideoUrl_WithRelativePath_ShouldReturnFullUrl()
        {
            // Arrange
            var relativePath = "/uploads/videos/test.mp4";

            // Act
            var result = _videoService.FormatVideoUrl(relativePath);

            // Assert
            result.Should().Be("https://localhost:7113/uploads/videos/test.mp4");
        }

        [Fact]
        public void FormatVideoUrl_WithBackslashes_ShouldNormalizeToForwardSlashes()
        {
            // Arrange
            var pathWithBackslashes = "\\uploads\\videos\\test.mp4";

            // Act
            var result = _videoService.FormatVideoUrl(pathWithBackslashes);

            // Assert
            result.Should().Be("https://localhost:7113/uploads/videos/test.mp4");
        }

        [Fact]
        public void FormatThumbnailUrl_WithNullInput_ShouldReturnEmptyString()
        {
            // Act
            var result = _videoService.FormatThumbnailUrl(null);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void FormatThumbnailUrl_WithEmptyInput_ShouldReturnEmptyString()
        {
            // Act
            var result = _videoService.FormatThumbnailUrl("");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void FormatThumbnailUrl_WithAbsoluteUrl_ShouldReturnUnchanged()
        {
            // Arrange
            var absoluteUrl = "https://example.com/thumbnail.jpg";

            // Act
            var result = _videoService.FormatThumbnailUrl(absoluteUrl);

            // Assert
            result.Should().Be(absoluteUrl);
        }

        [Fact]
        public void FormatThumbnailUrl_WithRelativePath_ShouldReturnFullUrl()
        {
            // Arrange
            var relativePath = "/uploads/thumbnails/test.jpg";

            // Act
            var result = _videoService.FormatThumbnailUrl(relativePath);

            // Assert
            result.Should().Be("https://localhost:7113/uploads/thumbnails/test.jpg");
        }

        [Fact]
        public async Task UploadVideoAsync_WithHttpException_ShouldReturnFailureWithExceptionMessage()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake video data");
            var fileName = "test-video.mp4";
            var title = "Test Video";
            var description = "Test Description";
            var token = "valid-jwt-token";

            _mockAuthStateProvider.Setup(x => x.GetTokenAsync())
                .ReturnsAsync(token);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _videoService.UploadVideoAsync(
                videoData, fileName, title, description);

            // Assert
            result.Success.Should().BeFalse();
            result.Video.Should().BeNull();
            result.ErrorMessage.Should().Contain("Exception during upload: Network error");
        }
    }
}