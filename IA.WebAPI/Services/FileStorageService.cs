using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using IA.WebAPI.Options;
using Microsoft.Extensions.Options;
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
        string GetUploadsBaseDirectory();
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly BlobServiceClient? _blobServiceClient;
        private readonly AzureStorageOptions _storageOptions;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileStorageService> _logger;
        private readonly string _localUploadsBaseDirectory;
        private readonly bool _useAzureStorage;

        // Lista de extensiones permitidas incluyendo videos
        private readonly string[] _allowedExtensions = { 
            // Imágenes
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp",
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

        // Tamaño máximo para imágenes/thumbnails: 5MB
        private readonly long _imageMaxFileSize = 5 * 1024 * 1024; // 5MB

        // Mapeo de extensiones a tipos MIME para imágenes
        private readonly Dictionary<string, string> _imageContentTypes = new()
        {
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".gif", "image/gif" },
            { ".bmp", "image/bmp" },
            { ".webp", "image/webp" },
            { ".svg", "image/svg+xml" }
        };

        public FileStorageService(
            IConfiguration configuration,
            IOptions<AzureStorageOptions> storageOptions,
            IWebHostEnvironment environment,
            ILogger<FileStorageService> logger)
        {
            _storageOptions = storageOptions.Value;
            _environment = environment;
            _logger = logger;

            // Determinar qué estrategia de almacenamiento usar
            _useAzureStorage = _storageOptions.ShouldUseAzureStorage(_environment);

            // Configurar almacenamiento local
            _localUploadsBaseDirectory = Path.Combine(_environment.ContentRootPath, "Uploads");

            if (_useAzureStorage)
            {
                // Configurar Azure Blob Storage
                var connectionString = configuration.GetConnectionString("AzureStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("AzureStorage connection string is required when UseAzureStorage is enabled");
                }

                _blobServiceClient = new BlobServiceClient(connectionString);

                // Inicializar contenedores de forma asíncrona
                _ = Task.Run(InitializeContainersAsync);

                _logger.LogInformation("FileStorageService configured for Azure Blob Storage with SAS tokens (Environment: {Environment})",
                    _environment.EnvironmentName);
            }
            else
            {
                // Configurar almacenamiento local
                if (!Directory.Exists(_localUploadsBaseDirectory))
                {
                    Directory.CreateDirectory(_localUploadsBaseDirectory);
                }

                _logger.LogInformation("FileStorageService configured for Local Storage (Environment: {Environment})",
                    _environment.EnvironmentName);
            }
        }

        private async Task InitializeContainersAsync()
        {
            if (_blobServiceClient == null) return;

            try
            {
                // Crear contenedores si no existen (PRIVADOS - sin acceso público)
                await CreateContainerIfNotExistsAsync(_storageOptions.ContainerNameVideos);
                await CreateContainerIfNotExistsAsync(_storageOptions.ContainerNameThumbnails);

                _logger.LogInformation("Azure Blob Storage containers initialized successfully (Private access)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Azure Blob Storage containers");
            }
        }

        private async Task CreateContainerIfNotExistsAsync(string containerName)
        {
            if (_blobServiceClient == null) return;

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            // IMPORTANTE: PublicAccessType.None = Acceso privado, requiere SAS tokens
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        }

        public string GetUploadsBaseDirectory()
        {
            return _useAzureStorage ? _storageOptions.BaseUrl : _localUploadsBaseDirectory;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string subDirectory = "")
        {
            ValidateFile(file);

            if (_useAzureStorage)
            {
                return await SaveFileToAzureAsync(file, subDirectory);
            }
            else
            {
                return await SaveFileToLocalAsync(file, subDirectory);
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            if (_useAzureStorage)
            {
                return await DeleteFileFromAzureAsync(filePath);
            }
            else
            {
                return await DeleteFileFromLocalAsync(filePath);
            }
        }

        public string GetFileUrl(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            if (_useAzureStorage)
            {
                return GetAzureFileUrlWithSAS(fileName);
            }
            else
            {
                return GetLocalFileUrl(fileName);
            }
        }

        public async Task<byte[]> GetFileAsync(string filePath)
        {
            if (_useAzureStorage)
            {
                return await GetFileFromAzureAsync(filePath);
            }
            else
            {
                return await GetFileFromLocalAsync(filePath);
            }
        }

        #region Azure Storage Implementation

        private async Task<string> SaveFileToAzureAsync(IFormFile file, string subDirectory)
        {
            if (_blobServiceClient == null)
                throw new InvalidOperationException("Blob service client is not initialized");

            try
            {
                // Sanitizar el nombre de archivo y crear nombre único
                string safeFileName = GetSafeFileName(file.FileName);
                string uniqueFileName = $"{GenerateUniqueId()}-{safeFileName}";

                // Determinar el contenedor basado en el subdirectorio
                string containerName = GetContainerName(subDirectory);
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

                // Crear el blob client
                var blobClient = containerClient.GetBlobClient(uniqueFileName);

                // Configurar headers HTTP
                var httpHeaders = new BlobHttpHeaders
                {
                    ContentType = GetContentType(file.FileName)
                };

                // Subir el archivo
                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = httpHeaders,
                    AccessTier = AccessTier.Hot // Para acceso frecuente
                });

                _logger.LogInformation("File uploaded to blob storage: {BlobName} in container {ContainerName}",
                    uniqueFileName, containerName);

                // Devolver ruta relativa que incluye el contenedor
                return $"{containerName}/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to blob storage: {FileName}", file.FileName);
                throw new InvalidOperationException($"Failed to upload file: {ex.Message}", ex);
            }
        }

        private async Task<bool> DeleteFileFromAzureAsync(string filePath)
        {
            if (_blobServiceClient == null) return false;

            try
            {
                // Parsear la ruta para obtener contenedor y nombre del blob
                var (containerName, blobName) = ParseFilePath(filePath);

                if (string.IsNullOrEmpty(containerName) || string.IsNullOrEmpty(blobName))
                {
                    _logger.LogWarning("Invalid file path format: {FilePath}", filePath);
                    return false;
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.DeleteIfExistsAsync();

                if (response.Value)
                {
                    _logger.LogInformation("File deleted from blob storage: {FilePath}", filePath);
                    return true;
                }
                else
                {
                    _logger.LogWarning("File not found in blob storage: {FilePath}", filePath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file from blob storage: {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// NUEVO: Genera URLs con SAS tokens para acceso seguro a Azure Blob Storage
        /// </summary>
        private string GetAzureFileUrlWithSAS(string fileName)
        {
            try
            {
                if (_blobServiceClient == null)
                {
                    _logger.LogWarning("Blob service client is null, returning basic URL");
                    return GetAzureFileUrlBasic(fileName);
                }

                // Si ya es una URL completa, devolverla tal como está
                if (fileName.StartsWith("http"))
                    return fileName;

                // Parsear la ruta para obtener contenedor y blob
                var (containerName, blobName) = ParseFilePath(fileName);

                if (string.IsNullOrEmpty(containerName) || string.IsNullOrEmpty(blobName))
                {
                    _logger.LogWarning("Could not parse file path for SAS generation: {FileName}", fileName);
                    return GetAzureFileUrlBasic(fileName);
                }

                // Obtener el blob client
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Verificar si puede generar SAS tokens
                if (!blobClient.CanGenerateSasUri)
                {
                    _logger.LogWarning("Cannot generate SAS URI for blob: {BlobName}", blobName);
                    return GetAzureFileUrlBasic(fileName);
                }

                // Crear SAS token con permisos de lectura y expiración de 24 horas
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    BlobName = blobName,
                    Resource = "b", // blob
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(24) // Token válido por 24 horas
                };

                // Solo permisos de lectura
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                // Generar la URL con SAS token
                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                _logger.LogDebug("Generated SAS URL for blob: {BlobName} (expires: {Expiry})",
                    blobName, sasBuilder.ExpiresOn);

                return sasUri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SAS token for file: {FileName}", fileName);
                // Fallback a URL básica en caso de error
                return GetAzureFileUrlBasic(fileName);
            }
        }

        /// <summary>
        /// Método de fallback para generar URL básica (sin SAS)
        /// </summary>
        private string GetAzureFileUrlBasic(string fileName)
        {
            // Si ya es una URL completa, devolverla tal como está
            if (fileName.StartsWith("http"))
                return fileName;

            // Si contiene el formato contenedor/archivo, construir la URL
            if (fileName.Contains("/"))
            {
                return $"{_storageOptions.BaseUrl}/{fileName}";
            }

            // Para compatibilidad con el formato anterior, asumir que es un video
            return $"{_storageOptions.BaseUrl}/{_storageOptions.ContainerNameVideos}/{fileName}";
        }

        private async Task<byte[]> GetFileFromAzureAsync(string filePath)
        {
            if (_blobServiceClient == null)
                throw new InvalidOperationException("Blob service client is not initialized");

            try
            {
                var (containerName, blobName) = ParseFilePath(filePath);

                if (string.IsNullOrEmpty(containerName) || string.IsNullOrEmpty(blobName))
                {
                    throw new ArgumentException("Invalid file path format", nameof(filePath));
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException("The requested file was not found", filePath);
                }

                var response = await blobClient.DownloadContentAsync();
                return response.Value.Content.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from blob storage: {FilePath}", filePath);
                throw;
            }
        }

        #endregion

        #region Local Storage Implementation

        private async Task<string> SaveFileToLocalAsync(IFormFile file, string subDirectory)
        {
            try
            {
                // Sanitizar el nombre de archivo y crear nombre único
                string safeFileName = GetSafeFileName(file.FileName);
                string uniqueFileName = $"{GenerateUniqueId()}-{safeFileName}";

                // Crear subdirectorio si se especifica
                string targetDirectory = _localUploadsBaseDirectory;
                if (!string.IsNullOrWhiteSpace(subDirectory))
                {
                    // NO sanitizar el subdirectorio para mantener nombres como "Videos" y "Thumbnails"
                    targetDirectory = Path.Combine(_localUploadsBaseDirectory, subDirectory);

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

                // Devolver ruta relativa con separadores correctos
                string relativePath = subDirectory.Length > 0
                    ? Path.Combine(subDirectory, uniqueFileName).Replace("\\", "/")
                    : uniqueFileName;

                _logger.LogInformation("File saved locally: {FilePath}", relativePath);

                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file locally: {FileName}", file.FileName);
                throw new InvalidOperationException($"Failed to save file: {ex.Message}", ex);
            }
        }

        private async Task<bool> DeleteFileFromLocalAsync(string filePath)
        {
            try
            {
                // Sanitizar y validar ruta
                filePath = SanitizeFilePath(filePath);
                string fullPath = Path.Combine(_localUploadsBaseDirectory, filePath);

                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Attempted to delete non-existent file: {FilePath}", filePath);
                    return false;
                }

                // Eliminar el archivo
                await Task.Run(() => File.Delete(fullPath));
                _logger.LogInformation("File deleted locally: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting local file: {FilePath}", filePath);
                return false;
            }
        }

        private string GetLocalFileUrl(string fileName)
        {
            string sanitizedFileName = SanitizeFilePath(fileName);

            // Asegurar que la URL siempre tenga el prefijo /Uploads/
            if (sanitizedFileName.StartsWith("Uploads/"))
            {
                return $"/{sanitizedFileName}";
            }
            else
            {
                return $"/Uploads/{sanitizedFileName}";
            }
        }

        private async Task<byte[]> GetFileFromLocalAsync(string filePath)
        {
            filePath = SanitizeFilePath(filePath);
            string fullPath = Path.Combine(_localUploadsBaseDirectory, filePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Local file not found: {FilePath}", filePath);
                throw new FileNotFoundException("The requested file was not found", filePath);
            }

            return await File.ReadAllBytesAsync(fullPath);
        }

        #endregion

        #region Helper Methods

        private string GetContainerName(string subDirectory)
        {
            return subDirectory.ToLowerInvariant() switch
            {
                "videos" => _storageOptions.ContainerNameVideos,
                "thumbnails" => _storageOptions.ContainerNameThumbnails,
                _ => _storageOptions.ContainerNameVideos // Default a videos
            };
        }

        private (string containerName, string blobName) ParseFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return (string.Empty, string.Empty);

            // Limpiar la ruta
            filePath = filePath.Replace("\\", "/").TrimStart('/');

            // Si contiene formato Uploads/SubDirectory/filename (formato legacy)
            if (filePath.StartsWith("Uploads/"))
            {
                var parts = filePath.Split('/');
                if (parts.Length >= 3)
                {
                    var subDir = parts[1].ToLowerInvariant();
                    var fileName = string.Join("/", parts.Skip(2));
                    var containerName = GetContainerName(subDir);
                    return (containerName, fileName);
                }
            }

            // Si contiene formato container/filename
            if (filePath.Contains("/"))
            {
                var parts = filePath.Split('/', 2);
                return (parts[0], parts[1]);
            }

            // Si es solo el nombre del archivo, asumir que está en videos
            return (_storageOptions.ContainerNameVideos, filePath);
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (_videoContentTypes.TryGetValue(extension, out var videoType))
                return videoType;

            if (_imageContentTypes.TryGetValue(extension, out var imageType))
                return imageType;

            return "application/octet-stream";
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
            // Para imágenes, aplicar límite de 5MB
            else if (_imageContentTypes.ContainsKey(extension))
            {
                maxSizeForType = _imageMaxFileSize;
            }
            else if (extension.StartsWith(".doc") || extension.StartsWith(".xls") || extension.StartsWith(".ppt"))
            {
                // Límite para documentos de Office (20MB)
                maxSizeForType = 20 * 1024 * 1024;
            }

            if (file.Length > maxSizeForType)
            {
                string sizeDisplay = maxSizeForType >= 1024 * 1024 * 1024
                    ? $"{maxSizeForType / (1024.0 * 1024 * 1024):F2}GB"
                    : $"{maxSizeForType / (1024.0 * 1024):F2}MB";

                throw new ArgumentException($"File exceeds maximum allowed size ({sizeDisplay}) for file type {extension}");
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

        #endregion
    }
}