using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IA.WebAPI.Models.DTOs
{
    /// <summary>
    /// DTO para representar un video con información adicional
    /// </summary>
    public class VideoDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime PublishDate { get; set; }
        public string Uri { get; set; } = string.Empty;
        public string? ThumbnailUri { get; set; }
        public long? UploadedByUserId { get; set; }
        public string? UploadedByUserName { get; set; }
        public string? UploadedByUserProfilePictureUrl { get; set; }

        // Contadores de interacciones
        public int LikesCount { get; set; }
        public int FavoritesCount { get; set; }
        public int ViewsCount { get; set; }
        public int CommentsCount { get; set; }

        // Interacciones del usuario actual
        public bool UserHasLiked { get; set; }
        public bool UserHasFavorited { get; set; }
        public bool UserHasWatchLater { get; set; }
        public bool UserHasViewed { get; set; }
    }

    /// <summary>
    /// DTO para actualizar un video
    /// </summary>
    public class VideoUpdateDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Clase auxiliar para ranking de resultados de búsqueda
    /// </summary>
    public class VideoSearchResult
    {
        public Video Video { get; set; } = null!;
        public int Relevance { get; set; }
        public string MatchType { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para respuesta paginada de videos
    /// </summary>
    public class PaginatedVideoResponse
    {
        public List<VideoDto> Videos { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
        public string? SearchQuery { get; set; }
    }
}
