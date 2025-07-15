using Microsoft.EntityFrameworkCore;
namespace IA.WebAPI.Models
{
    public class IAContext : DbContext
    {
        public IAContext(DbContextOptions<IAContext> options)
        : base(options)
        {
#if DEBUG
            // Solo migrar si no estamos en un ambiente de testing
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Testing")
            {
                try
                {
                    this.Database.Migrate();
                }
                catch (InvalidOperationException)
                {
                    // Ignorar errores de migración en tests (cuando se usa InMemory)
                }
            }
#endif
        }

        public DbSet<Video> Videos { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<VideoInteraction> VideoInteractions { get; set; }
        public DbSet<VideoComment> VideoComments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                               .AddJsonFile("appsettings.json")
                               .Build();
                optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configurar usuarios
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.ExternalId).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });
            // Configurar videos
            modelBuilder.Entity<Video>(entity =>
            {
                entity.HasOne(v => v.UploadedByUser)
                    .WithMany()
                    .HasForeignKey(v => v.UploadedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            // Configurar interacciones de video
            modelBuilder.Entity<VideoInteraction>(entity =>
            {
                // Relación con el usuario
                entity.HasOne(i => i.User)
                    .WithMany(u => u.Interactions)
                    .HasForeignKey(i => i.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Relación con el video
                entity.HasOne(i => i.Video)
                    .WithMany(v => v.Interactions)
                    .HasForeignKey(i => i.VideoId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Un usuario solo puede tener un tipo de interacción específica con un video
                entity.HasIndex(i => new { i.UserId, i.VideoId, i.Type }).IsUnique();
            });
            // Configurar comentarios de video
            modelBuilder.Entity<VideoComment>(entity =>
            {
                // Relación con el video
                entity.HasOne(c => c.Video)
                    .WithMany(v => v.Comments)
                    .HasForeignKey(c => c.VideoId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Relación con el usuario
                entity.HasOne(c => c.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull); // Evitar ciclo de eliminación en cascada
                // Relación jerárquica de comentarios (respuestas)
                entity.HasOne(c => c.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(c => c.ParentCommentId)
                    .OnDelete(DeleteBehavior.ClientSetNull); // No eliminar respuestas si se elimina el comentario padre
                // Índices para mejorar rendimiento
                entity.HasIndex(c => c.VideoId);
                entity.HasIndex(c => c.UserId);
                entity.HasIndex(c => c.CreatedAt);
            });
        }
    }
}