using Microsoft.EntityFrameworkCore;
using IA.WebAPI.Models;

namespace IA.WebAPI.Tests.Helpers
{
    public class TestIAContext : DbContext
    {
        public TestIAContext(DbContextOptions<TestIAContext> options)
            : base(options)
        {
            // No ejecutar Migrate() en tests
        }

        public DbSet<Video> Videos { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<VideoInteraction> VideoInteractions { get; set; }
        public DbSet<VideoComment> VideoComments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Usar la misma configuración que IAContext
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
                    .OnDelete(DeleteBehavior.ClientSetNull);

                // Relación jerárquica de comentarios (respuestas)
                entity.HasOne(c => c.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(c => c.ParentCommentId)
                    .OnDelete(DeleteBehavior.ClientSetNull);

                // Índices para mejorar rendimiento
                entity.HasIndex(c => c.VideoId);
                entity.HasIndex(c => c.UserId);
                entity.HasIndex(c => c.CreatedAt);
            });
        }
    }
}