using FFMpegCore;
using FFMpegCore.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace IA.WebAPI.Services
{
    /// <summary>
    /// Interfaz para el servicio de generación de thumbnails
    /// </summary>
    public interface IThumbnailGeneratorService
    {
        /// <summary>
        /// Genera un thumbnail desde un video al 20% de su duración
        /// </summary>
        /// <param name="videoFilePath">Ruta completa al archivo de video</param>
        /// <param name="videoFileName">Nombre del archivo de video (para generar nombre del thumbnail)</param>
        /// <returns>Ruta relativa del thumbnail generado o null si falló</returns>
        Task<string?> GenerateThumbnailFromVideoAsync(string videoFilePath, string videoFileName);
    }

    /// <summary>
    /// Servicio para generar thumbnails automáticamente desde videos
    /// </summary>
    public class ThumbnailGeneratorService : IThumbnailGeneratorService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ThumbnailGeneratorService> _logger;
        private readonly string _uploadsBaseDirectory;
        private readonly string _thumbnailsDirectory;

        public ThumbnailGeneratorService(
            IWebHostEnvironment environment,
            ILogger<ThumbnailGeneratorService> logger)
        {
            _environment = environment;
            _logger = logger;
            _uploadsBaseDirectory = Path.Combine(_environment.ContentRootPath, "Uploads");
            _thumbnailsDirectory = Path.Combine(_uploadsBaseDirectory, "Thumbnails");

            // Crear directorio de thumbnails si no existe
            if (!Directory.Exists(_thumbnailsDirectory))
            {
                Directory.CreateDirectory(_thumbnailsDirectory);
            }
        }

        public async Task<string?> GenerateThumbnailFromVideoAsync(string videoFilePath, string videoFileName)
        {
            try
            {
                _logger.LogInformation("Iniciando generación de thumbnail para video: {VideoFileName}", videoFileName);

                // Verificar que el archivo de video existe
                if (!File.Exists(videoFilePath))
                {
                    _logger.LogWarning("Archivo de video no encontrado: {VideoFilePath}", videoFilePath);
                    return null;
                }

                // Analizar el video para obtener información
                var mediaInfo = await FFProbe.AnalyseAsync(videoFilePath);

                if (mediaInfo.Duration.TotalSeconds < 1)
                {
                    _logger.LogWarning("Video muy corto para generar thumbnail: {Duration} segundos", mediaInfo.Duration.TotalSeconds);
                    return null;
                }

                // Calcular tiempo al 20% del video (mínimo 1 segundo, máximo en segundo 30)
                var targetSeconds = Math.Max(1, Math.Min(30, mediaInfo.Duration.TotalSeconds * 0.2));
                var timeSpan = TimeSpan.FromSeconds(targetSeconds);

                // Generar nombre único para el thumbnail (PNG temporal y WebP final)
                var tempPngFileName = $"{Path.GetFileNameWithoutExtension(videoFileName)}_{Guid.NewGuid():N}.png";
                var finalWebpFileName = $"{Path.GetFileNameWithoutExtension(videoFileName)}_{Guid.NewGuid():N}.webp";

                var tempPngPath = Path.Combine(_thumbnailsDirectory, tempPngFileName);
                var finalWebpPath = Path.Combine(_thumbnailsDirectory, finalWebpFileName);

                _logger.LogInformation("Generando thumbnail PNG temporal en tiempo {TimeSpan} para video {VideoFileName}", timeSpan, videoFileName);

                // Generar el thumbnail PNG temporal
                await FFMpeg.SnapshotAsync(
                    videoFilePath,
                    tempPngPath,
                    new System.Drawing.Size(1280, 720), // Usar System.Drawing.Size explícitamente
                    timeSpan);

                // Esperar un poco para que FFMpeg termine de escribir
                await Task.Delay(500);

                // Verificar que el PNG temporal se generó
                if (!File.Exists(tempPngPath))
                {
                    _logger.LogError("Thumbnail PNG temporal no se generó: {TempPngPath}", tempPngPath);
                    return null;
                }

                _logger.LogInformation("Convirtiendo PNG a WebP: {TempPngPath} → {FinalWebpPath}", tempPngPath, finalWebpPath);

                // Convertir PNG a WebP con compresión optimizada
                using (var image = await SixLabors.ImageSharp.Image.LoadAsync(tempPngPath))
                {
                    var webpEncoder = new WebpEncoder()
                    {
                        Quality = 85, // Calidad 85% (buen balance calidad/tamaño)
                        Method = WebpEncodingMethod.BestQuality,
                        FileFormat = WebpFileFormatType.Lossy
                    };

                    await image.SaveAsync(finalWebpPath, webpEncoder);
                }

                // Eliminar PNG temporal
                try
                {
                    File.Delete(tempPngPath);
                    _logger.LogInformation("PNG temporal eliminado: {TempPngPath}", tempPngPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo eliminar PNG temporal: {TempPngPath}", tempPngPath);
                }

                // Esperar un poco y verificar que el WebP se generó correctamente
                await Task.Delay(200);

                // Verificar múltiples veces si es necesario
                int maxRetries = 5;
                for (int i = 0; i < maxRetries; i++)
                {
                    if (File.Exists(finalWebpPath))
                    {
                        var fileInfo = new FileInfo(finalWebpPath);
                        if (fileInfo.Length > 0)
                        {
                            // Archivo encontrado y no está vacío
                            _logger.LogInformation("Thumbnail WebP verificado correctamente: {FinalWebpPath} ({FileSize} bytes)",
                                finalWebpPath, fileInfo.Length);
                            break;
                        }
                    }

                    if (i < maxRetries - 1) // No es el último intento
                    {
                        _logger.LogInformation("Reintento {Retry}/{MaxRetries} verificando thumbnail WebP: {FinalWebpPath}",
                            i + 1, maxRetries, finalWebpPath);
                        await Task.Delay(200); // Esperar 200ms más
                    }
                }

                // Verificación final
                if (!File.Exists(finalWebpPath))
                {
                    _logger.LogError("Thumbnail WebP no se generó correctamente después de {MaxRetries} intentos: {FinalWebpPath}",
                        maxRetries, finalWebpPath);
                    return null;
                }

                var finalFileInfo = new FileInfo(finalWebpPath);
                if (finalFileInfo.Length == 0)
                {
                    _logger.LogError("Thumbnail WebP generado está vacío: {FinalWebpPath}", finalWebpPath);
                    File.Delete(finalWebpPath);
                    return null;
                }

                // Devolver ruta relativa desde Uploads
                var relativePath = Path.Combine("Thumbnails", finalWebpFileName);

                _logger.LogInformation("Thumbnail WebP generado exitosamente: {RelativePath} ({FileSize} bytes)",
                    relativePath, finalFileInfo.Length);

                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar thumbnail para video: {VideoFileName}", videoFileName);
                return null;
            }
        }
    }
}