namespace IA.WebAPI.Models
{
    /// <summary>
    /// Representa una interacción de un usuario con un video
    /// </summary>
    public class VideoInteraction
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long VideoId { get; set; }
        public DateTime CreatedAt { get; set; }
        public InteractionType Type { get; set; }  // Tipo de interacción

        // Propiedades de navegación
        public virtual User User { get; set; } = null!;
        public virtual Video Video { get; set; } = null!;
    }

    /// <summary>
    /// Enumera los tipos de interacciones de usuario con un video
    /// </summary>
    public enum InteractionType
    {
        Like,
        Favorite,
        WatchLater,
        View     
    }
}