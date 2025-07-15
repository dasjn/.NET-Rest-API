using IA.FrontEnd.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using IA.FrontEnd.Models;

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
            byte[]? thumbnailData = null,
            string? thumbnailFileName = null,
            string? thumbnailContentType = null)
        {
            try
            {
                // DEBUG: Log what we received
                Console.WriteLine($"🔍 DEBUG VideoService: Video data size: {videoData?.Length ?? 0} bytes");
                Console.WriteLine($"🔍 DEBUG VideoService: Thumbnail data size: {thumbnailData?.Length ?? 0} bytes");
                Console.WriteLine($"🔍 DEBUG VideoService: Thumbnail filename: {thumbnailFileName ?? "null"}");
                Console.WriteLine($"🔍 DEBUG VideoService: Thumbnail content type: {thumbnailContentType ?? "null"}");

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

                Console.WriteLine("🔍 DEBUG VideoService: Creating multipart content...");

                // Preparar contenido multipart - SIN using statements para evitar que se cierren los streams
                var content = new MultipartFormDataContent();

                // Agregar archivo de video - crear ByteArrayContent en lugar de StreamContent
                var videoContent = new ByteArrayContent(videoData);
                videoContent.Headers.ContentType = new MediaTypeHeaderValue(
                    contentType ?? "application/octet-stream");

                content.Add(videoContent, "VideoFile", fileName);
                Console.WriteLine($"🔍 DEBUG VideoService: Added video file: {fileName}");

                // Agregar thumbnail si se proporciona - crear ByteArrayContent en lugar de StreamContent
                if (thumbnailData != null && !string.IsNullOrEmpty(thumbnailFileName))
                {
                    Console.WriteLine($"🔍 DEBUG VideoService: Adding thumbnail file: {thumbnailFileName}");

                    var thumbnailContent = new ByteArrayContent(thumbnailData);
                    thumbnailContent.Headers.ContentType = new MediaTypeHeaderValue(
                        thumbnailContentType ?? "application/octet-stream");

                    content.Add(thumbnailContent, "ThumbnailFile", thumbnailFileName);
                    Console.WriteLine("🔍 DEBUG VideoService: Thumbnail added to multipart content");
                }
                else
                {
                    Console.WriteLine("🔍 DEBUG VideoService: No thumbnail data provided");
                }

                // Agregar campos de texto
                content.Add(new StringContent(title, Encoding.UTF8, "text/plain"), "Name");
                content.Add(new StringContent(description, Encoding.UTF8, "text/plain"), "Description");
                Console.WriteLine("🔍 DEBUG VideoService: Added text fields");

                Console.WriteLine($"🔍 DEBUG VideoService: Sending POST request to {_videosApiEndpoint}");

                // Realizar solicitud
                var response = await _httpClient.PostAsync(_videosApiEndpoint, content);

                Console.WriteLine($"🔍 DEBUG VideoService: Response status: {response.StatusCode}");

                // Manejar respuesta
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"🔍 DEBUG VideoService: Response content: {responseContent}");

                    var videoEntity = await response.Content.ReadFromJsonAsync<Video>();
                    Console.WriteLine($"🔍 DEBUG VideoService: Deserialized video ID: {videoEntity?.Id}");
                    Console.WriteLine($"🔍 DEBUG VideoService: Deserialized video ThumbnailUri: {videoEntity?.ThumbnailUri}");

                    return (true, videoEntity, null);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"🔍 DEBUG VideoService: Error response: {errorContent}");
                    return (false, null, $"Upload failed: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔍 DEBUG VideoService: Exception: {ex.Message}");
                Console.WriteLine($"🔍 DEBUG VideoService: Stack trace: {ex.StackTrace}");
                return (false, null, $"Exception during upload: {ex.Message}");
            }
        }

        // Método para formatear la URL del video
        public string FormatVideoUrl(string videoUri)
        {
            if (Uri.IsWellFormedUriString(videoUri, UriKind.Absolute))
                return videoUri;

            // Normalizar las barras invertidas a barras normales para URLs web
            var normalizedPath = videoUri.Replace('\\', '/').TrimStart('/');
            return $"{_apiBaseUrl}/{normalizedPath}";
        }

        // Método para formatear la URL del thumbnail
        public string FormatThumbnailUrl(string? thumbnailUri)
        {
            if (string.IsNullOrEmpty(thumbnailUri))
                return string.Empty;

            if (Uri.IsWellFormedUriString(thumbnailUri, UriKind.Absolute))
                return thumbnailUri;

            // Normalizar las barras invertidas a barras normales para URLs web
            var normalizedPath = thumbnailUri.Replace('\\', '/').TrimStart('/');
            return $"{_apiBaseUrl}/{normalizedPath}";
        }
    }
}