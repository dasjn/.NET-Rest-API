namespace IA.WebAPI.Models
{
    public class Video
    {
        public long Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime PublishDate { get; set; }
        public required string Uri { get; set; }

        // Relación con el usuario que subió el video
        public long? UploadedByUserId { get; set; }

        // Propiedades de navegación
        public virtual User? UploadedByUser { get; set; }
        public virtual ICollection<VideoInteraction> Interactions { get; set; } = new List<VideoInteraction>();

        // Relación con comentarios
        public virtual ICollection<VideoComment> Comments { get; set; } = new List<VideoComment>();
    }
}