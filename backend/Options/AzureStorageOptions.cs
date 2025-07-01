namespace IA.WebAPI.Options
{
    public class AzureStorageOptions
    {
        public const string SectionName = "AzureStorage";

        public bool UseAzureStorage { get; set; } = false;
        public string AccountName { get; set; } = string.Empty;
        public string ContainerNameVideos { get; set; } = "videos";
        public string ContainerNameThumbnails { get; set; } = "thumbnails";
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Determina si debe usar Azure Storage basado en el entorno y configuración
        /// En producción siempre es true, en desarrollo respeta la configuración
        /// </summary>
        public bool ShouldUseAzureStorage(IWebHostEnvironment environment)
        {
            // En producción, siempre usar Azure Storage
            if (environment.IsProduction())
            {
                return true;
            }

            // En desarrollo, usar el valor configurado
            return UseAzureStorage;
        }
    }
}