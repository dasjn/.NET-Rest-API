// Actualización de Services/FileStorageService.cs para aumentar el límite de videos a 5GB
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace IA.WebAPI.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string subDirectory = "");
        Task<bool> DeleteFileAsync(string filePath);
        string GetFileUrl(string fileName);
        Task<byte[]> GetFileAsync(string filePath);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileStorageService> _logger;
        private readonly string _uploadsBaseDirectory;

        // Lista de extensiones permitidas incluyendo videos
        private readonly string[] _allowedExtensions = { 
            // Imágenes
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", 
            // Documentos
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", 
            // Videos
            ".mp4", ".mov", ".avi", ".wmv", ".mkv", ".webm", ".flv", ".m4v", 
            // Audio
            ".mp3", ".wav", ".aac", ".ogg", ".flac"
        };

        // Tamaño máximo para videos: 5GB
        private readonly long _videoMaxFileSize = 5L * 1024 * 1024 * 1024; // 5GB

        // Tamaño máximo para otros archivos
        private readonly long _defaultMaxFileSize = 100 * 1024 * 1024; // 100MB

        // Mapeo de extensiones a tipos MIME para videos
        private readonly Dictionary<string, string> _videoContentTypes = new()
        {
            { ".mp4", "video/mp4" },
            { ".mov", "video/quicktime" },
            { ".avi", "video/x-msvideo" },
            { ".wmv", "video/x-ms-wmv" },
            { ".mkv", "video/x-matroska" },
            { ".webm", "video/webm" },
            { ".flv", "video/x-flv" },
            { ".m4v", "video/x-m4v" }
        };

        public FileStorageService(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<FileStorageService> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;

            // Directorio base para uploads
            _uploadsBaseDirectory = Path.Combine(_environment.ContentRootPath, "Uploads");

            // Crear el directorio si no existe
            if (!Directory.Exists(_uploadsBaseDirectory))
            {
                Directory.CreateDirectory(_uploadsBaseDirectory);
            }
        }

        public async Task<string> SaveFileAsync(IFormFile file, string subDirectory = "")
        {
            ValidateFile(file);

            // Sanitizar el nombre de archivo y crear nombre único
            string safeFileName = GetSafeFileName(file.FileName);
            string uniqueFileName = $"{GenerateUniqueId()}-{safeFileName}";

            // Crear subdirectorio si se especifica
            string targetDirectory = _uploadsBaseDirectory;
            if (!string.IsNullOrWhiteSpace(subDirectory))
            {
                // Sanitizar subdirectorio
                subDirectory = Regex.Replace(subDirectory, @"[^\w\d]", "_");
                targetDirectory = Path.Combine(_uploadsBaseDirectory, subDirectory);

                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
            }

            string filePath = Path.Combine(targetDirectory, uniqueFileName);

            // Guardar el archivo físicamente
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Devolver ruta relativa
            string relativePath = subDirectory.Length > 0
                ? Path.Combine(subDirectory, uniqueFileName)
                : uniqueFileName;

            _logger.LogInformation("Archivo guardado: {FilePath}", relativePath);

            return relativePath;
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                // Sanitizar y validar ruta
                filePath = SanitizeFilePath(filePath);
                string fullPath = Path.Combine(_uploadsBaseDirectory, filePath);

                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Intento de eliminar archivo inexistente: {FilePath}", filePath);
                    return false;
                }

                // Eliminar el archivo
                await Task.Run(() => File.Delete(fullPath));
                _logger.LogInformation("Archivo eliminado: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar archivo: {FilePath}", filePath);
                return false;
            }
        }

        public string GetFileUrl(string fileName)
        {
            string sanitizedFileName = SanitizeFilePath(fileName);
            return $"/Uploads/{sanitizedFileName}";
        }

        public async Task<byte[]> GetFileAsync(string filePath)
        {
            filePath = SanitizeFilePath(filePath);
            string fullPath = Path.Combine(_uploadsBaseDirectory, filePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Archivo solicitado no encontrado: {FilePath}", filePath);
                throw new FileNotFoundException("The requested file was not found", filePath);
            }

            return await File.ReadAllBytesAsync(fullPath);
        }

        private void ValidateFile(IFormFile file)
        {
            // Validar tamaño
            if (file.Length <= 0)
            {
                throw new ArgumentException("Empty file");
            }

            // Obtener la extensión del archivo
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            // Validar extensión
            if (!_allowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"File type {extension} is not allowed");
            }

            // Determinar el tamaño máximo según el tipo de archivo
            long maxSizeForType = _defaultMaxFileSize;

            // Para videos, aplicar límite de 5GB
            if (_videoContentTypes.ContainsKey(extension))
            {
                maxSizeForType = _videoMaxFileSize;
            }
            else if (extension.StartsWith(".doc") || extension.StartsWith(".xls") || extension.StartsWith(".ppt"))
            {
                // Límite para documentos de Office (20MB)
                maxSizeForType = 20 * 1024 * 1024;
            }
            else if (extension.StartsWith(".jpg") || extension.StartsWith(".png") || extension.StartsWith(".gif"))
            {
                // Límite para imágenes (10MB)
                maxSizeForType = 10 * 1024 * 1024;
            }

            if (file.Length > maxSizeForType)
            {
                string sizeInGB = maxSizeForType >= 1024 * 1024 * 1024
                    ? $"{maxSizeForType / (1024.0 * 1024 * 1024):F2}GB"
                    : $"{maxSizeForType / (1024.0 * 1024):F2}MB";

                throw new ArgumentException($"File exceeds maximum allowed size ({sizeInGB}) for file type {extension}");
            }

            // Validar nombre de archivo
            if (string.IsNullOrWhiteSpace(file.FileName) || file.FileName.Length > 255)
            {
                throw new ArgumentException("Invalid file name");
            }
        }

        private string GetSafeFileName(string fileName)
        {
            // Eliminar caracteres inválidos y limitar longitud
            string safeName = Regex.Replace(fileName, @"[^\w\d\._-]", "_");

            // Si el nombre limpio sigue siendo muy largo, truncarlo
            if (safeName.Length > 50)
            {
                string extension = Path.GetExtension(safeName);
                safeName = safeName.Substring(0, 45) + extension;
            }

            return safeName;
        }

        private string GenerateUniqueId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 16);
        }

        private string SanitizeFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            // Prevenir directory traversal
            filePath = filePath.Replace("../", "").Replace("..\\", "");

            // Limpiar caracteres especiales excepto / para subdirectorios
            return Regex.Replace(filePath, @"[^\w\d\._/-]", "_");
        }
    }
}