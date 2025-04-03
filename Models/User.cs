namespace IA.WebAPI.Models
{
    /// <summary>
    /// Representa un usuario registrado en el sistema
    /// </summary>
    public class User
    {
        public long Id { get; set; }

        /// <summary>
        /// ID externo del proveedor de autenticación (Google, etc.)
        /// </summary>
        public required string ExternalId { get; set; }

        /// <summary>
        /// Email del usuario
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Nombre completo del usuario
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// URL de la imagen de perfil
        /// </summary>
        public string? ProfilePictureUrl { get; set; }

        /// <summary>
        /// Fecha de registro en el sistema
        /// </summary>
        public DateTime RegisteredDate { get; set; }

        /// <summary>
        /// Última fecha de inicio de sesión
        /// </summary>
        public DateTime LastLoginDate { get; set; }

        // Navegación propiedades
        public virtual ICollection<VideoInteraction> Interactions { get; set; } = new List<VideoInteraction>();

        // Relación con comentarios realizados
        public virtual ICollection<VideoComment> Comments { get; set; } = new List<VideoComment>();
    }
}