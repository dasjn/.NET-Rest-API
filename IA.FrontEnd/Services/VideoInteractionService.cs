using IA.FrontEnd.Auth;
using IA.FrontEnd.Models;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace IA.FrontEnd.Services
{
    public class VideoInteractionService
    {
        private readonly HttpClient _httpClient;
        private readonly CustomAuthStateProvider _authStateProvider;
        private readonly string _apiBaseUrl;

        public VideoInteractionService(
            HttpClient httpClient,
            AuthenticationStateProvider authStateProvider,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _authStateProvider = (CustomAuthStateProvider)authStateProvider;
            _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7113";
        }


        #region User Video Interactions (Likes, Favorites, Watch Later)

        public async Task<bool> LikeVideo(long videoId)
        {
            return await AddInteraction(videoId, "like");
        }

        public async Task<bool> UnlikeVideo(long videoId)
        {
            return await RemoveInteraction(videoId, "like");
        }

        public async Task<bool> AddToFavorites(long videoId)
        {
            return await AddInteraction(videoId, "favorite");
        }

        public async Task<bool> RemoveFromFavorites(long videoId)
        {
            return await RemoveInteraction(videoId, "favorite");
        }

        public async Task<bool> AddToWatchLater(long videoId)
        {
            return await AddInteraction(videoId, "watch-later");
        }

        public async Task<bool> RemoveFromWatchLater(long videoId)
        {
            return await RemoveInteraction(videoId, "watch-later");
        }

        public async Task<bool> RegisterVideoView(long videoId)
        {
            return await AddInteraction(videoId, "view");
        }

        private async Task<bool> AddInteraction(long videoId, string interactionType)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/api/user-videos/{interactionType}/{videoId}", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> RemoveInteraction(long videoId, string interactionType)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/api/user-videos/{interactionType}/{videoId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Comments

        public async Task<List<CommentDto>> GetVideoComments(long videoId)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.GetFromJsonAsync<List<CommentDto>>($"{_apiBaseUrl}/api/videos/{videoId}/comments");
                return response ?? new List<CommentDto>();
            }
            catch
            {
                return new List<CommentDto>();
            }
        }

        public async Task<CommentDto?> AddComment(long videoId, string content, long? parentCommentId = null)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return null;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var createCommentDto = new CreateCommentDto
                {
                    Content = content,
                    ParentCommentId = parentCommentId
                };

                var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/videos/{videoId}/comments", createCommentDto);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CommentDto>();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> UpdateComment(long videoId, long commentId, string content)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var updateCommentDto = new UpdateCommentDto
                {
                    Id = commentId,
                    Content = content
                };

                var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/api/videos/{videoId}/comments/{commentId}", updateCommentDto);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteComment(long videoId, long commentId)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/api/videos/{videoId}/comments/{commentId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Video Listings

        public async Task<List<VideoDto>> GetAllVideos()
        {
            return await GetAllVideosWithPagination(0, 100); // Cargar más por defecto
        }

        public async Task<List<VideoDto>> GetMyVideos()
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>($"{_apiBaseUrl}/api/videos/my-videos");
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener mis videos: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        public async Task<List<VideoDto>> GetLikedVideos()
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>($"{_apiBaseUrl}/api/user-videos/likes");
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener videos con 'me gusta': {ex.Message}");
                return new List<VideoDto>();
            }
        }

        // Videos marcados para "Ver más tarde"
        public async Task<List<VideoDto>> GetWatchLaterVideos()
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>($"{_apiBaseUrl}/api/user-videos/watch-later");
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener videos de 'ver más tarde': {ex.Message}");
                return new List<VideoDto>();
            }
        }

        // Videos vistos (historial)
        public async Task<List<VideoDto>> GetViewedVideos()
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>($"{_apiBaseUrl}/api/user-videos/history");
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener historial de videos: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        // Método mejorado para búsqueda con paginación
        public async Task<List<VideoDto>> SearchVideosWithContext(
    string query,
    string context = "all",
    int skip = 0,
    int take = 20)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Para búsqueda global, usar el endpoint de búsqueda
                if (context == "all")
                {
                    var searchUrl = $"{_apiBaseUrl}/api/videos/search?skip={skip}&take={take}";

                    // Solo agregar query si no está vacío
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var encodedQuery = Uri.EscapeDataString(query);
                        searchUrl += $"&query={encodedQuery}";
                    }

                    var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>(searchUrl);
                    return videos ?? new List<VideoDto>();
                }

                // Para búsquedas contextuales, usar la lógica anterior (filtrado local)
                List<VideoDto> contextVideos = context.ToLower() switch
                {
                    "myvideos" => await GetMyVideos(),
                    "history" => await GetViewedVideos(),
                    "liked" => await GetLikedVideos(),
                    "watchlater" => await GetWatchLaterVideos(),
                    "favorites" => await _httpClient.GetFromJsonAsync<List<VideoDto>>($"{_apiBaseUrl}/api/user-videos/favorites") ?? new List<VideoDto>(),
                    _ => await GetAllVideosWithPagination(0, 1000) // Cargar muchos para filtrar localmente
                };

                // Filtrar localmente para contextos específicos
                if (!string.IsNullOrWhiteSpace(query))
                {
                    query = query.ToLower();
                    contextVideos = contextVideos
                        .Where(v =>
                            v.Name.ToLower().Contains(query) ||
                            (v.Description != null && v.Description.ToLower().Contains(query)) ||
                            (v.UploadedByUserName != null && v.UploadedByUserName.ToLower().Contains(query)))
                        .ToList();
                }

                return contextVideos.Skip(skip).Take(take).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching videos with context: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        public async Task<List<VideoDto>> GetAllVideosWithPagination(int skip = 0, int take = 20)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Usar el endpoint de búsqueda sin query para obtener todos los videos
                var url = $"{_apiBaseUrl}/api/videos/search?skip={skip}&take={take}";
                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>(url);
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener videos: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        public async Task<List<VideoDto>> GetMyVideosWithPagination(int skip = 0, int take = 20)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiBaseUrl}/api/videos/my-videos?skip={skip}&take={take}";
                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>(url);
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener mis videos con paginación: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        public async Task<List<VideoDto>> GetLikedVideosWithPagination(int skip = 0, int take = 20)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiBaseUrl}/api/user-videos/likes?skip={skip}&take={take}";
                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>(url);
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener videos con 'me gusta' con paginación: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        public async Task<List<VideoDto>> GetWatchLaterVideosWithPagination(int skip = 0, int take = 20)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiBaseUrl}/api/user-videos/watch-later?skip={skip}&take={take}";
                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>(url);
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener videos de 'ver más tarde' con paginación: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        public async Task<List<VideoDto>> GetViewedVideosWithPagination(int skip = 0, int take = 20)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return new List<VideoDto>();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiBaseUrl}/api/user-videos/history?skip={skip}&take={take}";
                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>(url);
                return videos ?? new List<VideoDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener historial con paginación: {ex.Message}");
                return new List<VideoDto>();
            }
        }

        // Limpiar historial de vistas
        public async Task<bool> ClearHistory()
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/api/user-videos/history");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al limpiar historial: {ex.Message}");
                return false;
            }
        }

        // Limpiar lista de "Ver más tarde"
        public async Task<bool> ClearWatchLaterList()
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Primero obtenemos la lista actual para procesar cada video
                var watchLaterVideos = await GetWatchLaterVideos();
                if (watchLaterVideos == null || !watchLaterVideos.Any())
                    return true; // No hay nada que eliminar

                // Eliminar cada video de la lista
                bool allSuccess = true;
                foreach (var video in watchLaterVideos)
                {
                    bool success = await RemoveFromWatchLater(video.Id);
                    if (!success)
                        allSuccess = false;
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al limpiar lista 'Ver más tarde': {ex.Message}");
                return false;
            }
        }

        // to do: Implementar un metodo de labeling para que los videos se recomienden a los usuarios en base a sus gustos
        public async Task<List<VideoDto>> GetRecommendedVideos(long currentVideoId)
        {
            try
            {
                var allVideos = await GetAllVideos();
                return allVideos.Where(v => v.Id != currentVideoId).ToList();
            }
            catch
            {
                return new List<VideoDto>();
            }
        }

        #endregion

        public async Task<VideoDto?> GetVideoDetails(long videoId)
        {
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                return await _httpClient.GetFromJsonAsync<VideoDto>($"{_apiBaseUrl}/api/videos/{videoId}");
            }
            catch
            {
                return null;
            }
        }
    }
}