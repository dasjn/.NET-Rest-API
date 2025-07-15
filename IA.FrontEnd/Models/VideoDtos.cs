namespace IA.FrontEnd.Models
{
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
}