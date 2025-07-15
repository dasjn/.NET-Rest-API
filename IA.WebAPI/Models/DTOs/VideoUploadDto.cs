using System.ComponentModel.DataAnnotations;

namespace IA.WebAPI.Models.DTOs
{
    /// <summary>
    /// DTO para subir un video con thumbnail opcional
    /// </summary>
    public class VideoUploadDto
    {
        /// <summary>
        /// Archivo de video a subir
        /// </summary>
        [Required(ErrorMessage = "Video file is required")]
        public IFormFile VideoFile { get; set; } = null!;

        /// <summary>
        /// Imagen thumbnail del video (opcional)
        /// </summary>
        public IFormFile? ThumbnailFile { get; set; }

        /// <summary>
        /// Título del video
        /// </summary>
        [Required(ErrorMessage = "Video name is required")]
        [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descripción del video
        /// </summary>
        [StringLength(10000, ErrorMessage = "Description cannot exceed 5000 characters")]
        public string? Description { get; set; }
    }
}