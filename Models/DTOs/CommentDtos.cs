using System.ComponentModel.DataAnnotations;

namespace IA.WebAPI.Models.DTOs
{
    /// <summary>
    /// DTO para enviar un nuevo comentario
    /// </summary>
    public class CreateCommentDto
    {
        /// <summary>
        /// Contenido del comentario
        /// </summary>
        [Required(ErrorMessage = "El contenido del comentario es obligatorio")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "El comentario debe tener entre 1 y 500 caracteres")]
        public required string Content { get; set; }

        /// <summary>
        /// ID del comentario padre para respuestas (opcional)
        /// </summary>
        public long? ParentCommentId { get; set; }
    }

    /// <summary>
    /// DTO para actualizar un comentario existente
    /// </summary>
    public class UpdateCommentDto
    {
        /// <summary>
        /// ID del comentario a actualizar
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Nuevo contenido del comentario
        /// </summary>
        public required string Content { get; set; }
    }

    /// <summary>
    /// DTO con información completa de un comentario para mostrar
    /// </summary>
    public class CommentDto
    {
        /// <summary>
        /// ID del comentario
        /// </summary>
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
        /// Nombre del usuario que hizo el comentario
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// URL de la imagen de perfil del usuario
        /// </summary>
        public string? UserProfilePictureUrl { get; set; }

        /// <summary>
        /// ID del comentario padre (para respuestas a comentarios)
        /// Null si es un comentario de primer nivel
        /// </summary>
        public long? ParentCommentId { get; set; }

        /// <summary>
        /// Contenido del comentario
        /// </summary>
        public string Content { get; set; } = string.Empty;

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
        public bool IsEdited { get; set; }

        /// <summary>
        /// Indica si el usuario actual es el autor del comentario
        /// </summary>
        public bool IsOwnComment { get; set; }

        /// <summary>
        /// Lista de respuestas a este comentario
        /// </summary>
        public List<CommentDto> Replies { get; set; } = new List<CommentDto>();
    }
}