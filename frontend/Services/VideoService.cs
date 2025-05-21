using IA.FrontEnd.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using IA.FrontEnd.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace IA.FrontEnd.Services
{
    public class VideoService
    {
        private readonly HttpClient _httpClient;
        private readonly CustomAuthStateProvider _authStateProvider;
        private readonly string _apiBaseUrl;
        private readonly string _videosApiEndpoint;

        public VideoService(
            HttpClient httpClient,
            AuthenticationStateProvider authStateProvider,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _authStateProvider = (CustomAuthStateProvider)authStateProvider;

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
            IBrowserFile? thumbnailFile = null)
        {
            try
            {
                // Validations
                if (videoData == null || videoData.Length == 0)
                    return (false, null, "No video data to upload.");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
                    return (false, null, "Video must have a title and description.");

                // Get token
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return (false, null, "Authentication token is missing.");

                // Configure client with token
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                // Prepare multipart content
                using var content = new MultipartFormDataContent();
                using var stream = new MemoryStream(videoData);
                using var fileContent = new StreamContent(stream);

                // Set content type
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    contentType ?? "application/octet-stream");

                content.Add(fileContent, "videoFile", fileName);
                content.Add(new StringContent(title, Encoding.UTF8, "text/plain"), "name");
                content.Add(new StringContent(description, Encoding.UTF8, "text/plain"), "description");

                // Add thumbnail if provided
                if (thumbnailFile != null)
                {
                    using var thumbStream = thumbnailFile.OpenReadStream(4 * 1024 * 1024); // 4MB max
                    var thumbBytes = new byte[thumbnailFile.Size];
                    await thumbStream.ReadAsync(thumbBytes);

                    using var thumbContent = new StreamContent(new MemoryStream(thumbBytes));
                    thumbContent.Headers.ContentType = new MediaTypeHeaderValue(thumbnailFile.ContentType);
                    content.Add(thumbContent, "thumbnailFile", thumbnailFile.Name);
                }

                // Make request
                var response = await _httpClient.PostAsync(_videosApiEndpoint, content);

                // Handle response
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

        // Método para formatear la URL del video
        public string FormatVideoUrl(string videoUri)
        {
            if (Uri.IsWellFormedUriString(videoUri, UriKind.Absolute))
                return videoUri;

            return $"{_apiBaseUrl}/{videoUri.TrimStart('/')}";
        }
    }
}