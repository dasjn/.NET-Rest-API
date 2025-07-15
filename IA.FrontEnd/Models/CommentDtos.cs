using System.ComponentModel.DataAnnotations;

namespace IA.FrontEnd.Models
{
    public class CommentDto
    {
        public long Id { get; set; }
        public long VideoId { get; set; }
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserProfilePictureUrl { get; set; }
        public long? ParentCommentId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsOwnComment { get; set; }
        public List<CommentDto> Replies { get; set; } = new List<CommentDto>();
    }

    public class CreateCommentDto
    {
        [Required(ErrorMessage = "El contenido del comentario es obligatorio")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "El comentario debe tener entre 1 y 500 caracteres")]
        public required string Content { get; set; }

        public long? ParentCommentId { get; set; }
    }

    public class UpdateCommentDto
    {
        public long Id { get; set; }
        public required string Content { get; set; }
    }
}