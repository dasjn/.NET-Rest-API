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
            try
            {
                var token = await _authStateProvider.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var videos = await _httpClient.GetFromJsonAsync<List<VideoDto>>($"{_apiBaseUrl}/api/videos");
                return videos ?? new List<VideoDto>();
            }
            catch
            {
                return new List<VideoDto>();
            }
        }

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