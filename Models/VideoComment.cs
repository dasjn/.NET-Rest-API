namespace IA.WebAPI.Models
{
    /// <summary>
    /// Representa un comentario de un usuario en un video
    /// </summary>
    public class VideoComment
    {
        public long Id { get; set; }

        /// <summary>
        /// ID del video al que pertenece el comentario
        /// </summary>
        public long VideoId { get; set; }

        /// <summary>
        /// ID del usuario que hizo el comentario
        /// </summary>
        public long UserId { get; set; }

        /// <summary>
        /// ID del comentario padre (para respuestas a comentarios)
        /// Null si es un comentario de primer nivel
        /// </summary>
        public long? ParentCommentId { get; set; }

        /// <summary>
        /// Contenido del comentario
        /// </summary>
        public required string Content { get; set; }

        /// <summary>
        /// Fecha y hora de creación del comentario
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Fecha y hora de última edición del comentario
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Indica si el comentario ha sido editado
        /// </summary>
        public bool IsEdited => UpdatedAt.HasValue;

        // Propiedades de navegación
        public virtual Video Video { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual VideoComment? ParentComment { get; set; }
        public virtual ICollection<VideoComment> Replies { get; set; } = new List<VideoComment>();
    }
}