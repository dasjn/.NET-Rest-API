using Microsoft.Extensions.Configuration;
using IA.FrontEnd.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace IA.FrontEnd.Tests.Helpers
{
    public class TestVideoService
    {
        private readonly HttpClient _httpClient;
        private readonly ITestAuthStateProvider _authStateProvider;
        private readonly string _apiBaseUrl;
        private readonly string _videosApiEndpoint;

        public TestVideoService(
            HttpClient httpClient,
            ITestAuthStateProvider authStateProvider,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _authStateProvider = authStateProvider;

            // Configurar URLs desde configuración
            _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7113";
            _videosApiEndpoint = "/api/Videos";
        }

        public async Task<(bool Success, Video? Video, string? ErrorMessage)> UploadVideoAsync(
            byte[] videoData,
            string fileName,
            string title,
            string description,
            string? contentType = null,
            byte[]? thumbnailData = null,
            string? thumbnailFileName = null,
            string? thumbnailContentType = null)
        {
            try
            {
                // Validaciones
                if (videoData == null || videoData.Length == 0)
                    return (false, null, "No video data to upload.");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
                    return (false, null, "Video must have a title and description.");

                // Obtener token
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return (false, null, "Authentication token is missing.");

                // Configurar cliente con token
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                // Preparar contenido multipart
                var content = new MultipartFormDataContent();

                // Agregar archivo de video
                var videoContent = new ByteArrayContent(videoData);
                videoContent.Headers.ContentType = new MediaTypeHeaderValue(
                    contentType ?? "application/octet-stream");

                content.Add(videoContent, "VideoFile", fileName);

                // Agregar thumbnail si se proporciona
                if (thumbnailData != null && !string.IsNullOrEmpty(thumbnailFileName))
                {
                    var thumbnailContent = new ByteArrayContent(thumbnailData);
                    thumbnailContent.Headers.ContentType = new MediaTypeHeaderValue(
                        thumbnailContentType ?? "application/octet-stream");

                    content.Add(thumbnailContent, "ThumbnailFile", thumbnailFileName);
                }

                // Agregar campos de texto
                content.Add(new StringContent(title, Encoding.UTF8, "text/plain"), "Name");
                content.Add(new StringContent(description, Encoding.UTF8, "text/plain"), "Description");

                // Realizar solicitud
                var response = await _httpClient.PostAsync(_videosApiEndpoint, content);

                // Manejar respuesta
                if (response.IsSuccessStatusCode)
                {
                    var videoEntity = await response.Content.ReadFromJsonAsync<Video>();
                    return (true, videoEntity, null);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, null, $"Upload failed: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Exception during upload: {ex.Message}");
            }
        }

        public string FormatVideoUrl(string videoUri)
        {
            if (Uri.IsWellFormedUriString(videoUri, UriKind.Absolute))
                return videoUri;

            var normalizedPath = videoUri.Replace('\\', '/').TrimStart('/');
            return $"{_apiBaseUrl}/{normalizedPath}";
        }

        public string FormatThumbnailUrl(string? thumbnailUri)
        {
            if (string.IsNullOrEmpty(thumbnailUri))
                return string.Empty;

            if (Uri.IsWellFormedUriString(thumbnailUri, UriKind.Absolute))
                return thumbnailUri;

            var normalizedPath = thumbnailUri.Replace('\\', '/').TrimStart('/');
            return $"{_apiBaseUrl}/{normalizedPath}";
        }
    }
}