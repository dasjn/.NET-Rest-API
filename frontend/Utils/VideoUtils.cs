using IA.FrontEnd.Services;

namespace IA.FrontEnd.Utils
{
    public static class VideoUtils
    {
        public static string GetVideoThumbnail(long videoId, string? thumbnailUri, VideoService videoService)
        {
            // Si hay un thumbnail personalizado, usarlo
            if (!string.IsNullOrEmpty(thumbnailUri))
            {
                var formattedUrl = videoService.FormatThumbnailUrl(thumbnailUri);
                if (!string.IsNullOrEmpty(formattedUrl))
                {
                    return formattedUrl;
                }
            }
            // Fallback a imagen placeholder basada en el ID del video
            return "https://picsum.photos/400/225?random=" + videoId;
        }

        public static string FormatViews(int views)
        {
            if (views >= 1000000)
                return $"{views / 1000000.0:F1}M";
            if (views >= 1000)
                return $"{views / 1000.0:F1}K";
            return views.ToString();
        }

        public static string FormatDate(DateTime publishedDate)
        {
            var timeSpan = DateTime.Now - publishedDate;

            if (timeSpan.TotalDays >= 365)
                return $"{(int)(timeSpan.TotalDays / 365)} año{((int)(timeSpan.TotalDays / 365) > 1 ? "s" : "")}";
            if (timeSpan.TotalDays >= 30)
                return $"{(int)(timeSpan.TotalDays / 30)} mes{((int)(timeSpan.TotalDays / 30) > 1 ? "es" : "")}";
            if (timeSpan.TotalDays >= 7)
                return $"{(int)(timeSpan.TotalDays / 7)} semana{((int)(timeSpan.TotalDays / 7) > 1 ? "s" : "")}";
            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays} día{((int)timeSpan.TotalDays > 1 ? "s" : "")}";
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h";
            if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes}min";
            return "Ahora";
        }
    }
}