using Microsoft.EntityFrameworkCore;

namespace IA.WebAPI.Models
{
    public class IAContext: DbContext
    {
        public IAContext(DbContextOptions<IAContext> options)
        : base(options)
        {
#if DEBUG
            this.Database.Migrate();
#endif
        }


        public DbSet<Video> Videos { get; set; }

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
            //Analizar si necesitamos customizar tablas o relaciones
            base.OnModelCreating(modelBuilder);
        }
    }
}
