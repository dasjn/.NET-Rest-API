using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IA.WebAPI.Models;
using IA.WebAPI.Models.DTOs;
using IA.WebAPI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace IA.WebAPI.Controllers
{
    [RequireHttps]
    [Route("api/[controller]")]
    [ApiController]
    public class VideosController : ControllerBase
    {
        private readonly IAContext _context;
        private readonly IFileStorageService _fileService;
        private readonly IThumbnailGeneratorService _thumbnailGenerator;
        private readonly ILogger<VideosController> _logger;

        public VideosController(
            IAContext context,
            IFileStorageService fileService,
            IThumbnailGeneratorService thumbnailGenerator,
            ILogger<VideosController> logger)
        {
            _context = context;
            _fileService = fileService;
            _thumbnailGenerator = thumbnailGenerator;
            _logger = logger;
        }

        // GET: api/Videos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetVideos()
        {
            long? userId = User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null;

            // Obtener los videos con contadores de interacciones e información de usuario
            var videosQuery = _context.Videos
                .Select(v => new VideoDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Description = v.Description,
                    PublishDate = v.PublishDate,
                    Uri = v.Uri, // Se corregirá después con ApplyCorrectUrls
                    ThumbnailUri = v.ThumbnailUri, // Se corregirá después con ApplyCorrectUrls
                    UploadedByUserId = v.UploadedByUserId,
                    UploadedByUserName = v.UploadedByUser != null ? v.UploadedByUser.Name : null,
                    UploadedByUserProfilePictureUrl = v.UploadedByUser != null ? v.UploadedByUser.ProfilePictureUrl : null,
                    LikesCount = v.Interactions.Count(i => i.Type == InteractionType.Like),
                    FavoritesCount = v.Interactions.Count(i => i.Type == InteractionType.Favorite),
                    ViewsCount = v.Interactions.Count(i => i.Type == InteractionType.View),
                    CommentsCount = v.Comments.Count
                });

            // Obtener los videos de la base de datos
            var videos = await videosQuery.ToListAsync();

            // Aplicar URLs correctas (local o blob storage)
            videos = ApplyCorrectUrls(videos);

            if (userId.HasValue)
            {
                // Obtener todas las interacciones del usuario actual en una sola consulta
                var userInteractions = await _context.VideoInteractions
                    .Where(i => i.UserId == userId.Value)
                    .Select(i => new { i.VideoId, i.Type })
                    .ToListAsync();

                // Agrupar interacciones por video ID para proceso más eficiente
                var interactionsByVideo = userInteractions
                    .GroupBy(i => i.VideoId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(i => i.Type).ToList()
                    );

                // Completar información de interacciones para cada video
                foreach (var video in videos)
                {
                    if (interactionsByVideo.TryGetValue(video.Id, out var interactions))
                    {
                        video.UserHasLiked = interactions.Contains(InteractionType.Like);
                        video.UserHasFavorited = interactions.Contains(InteractionType.Favorite);
                        video.UserHasWatchLater = interactions.Contains(InteractionType.WatchLater);
                        video.UserHasViewed = interactions.Contains(InteractionType.View);
                    }
                }
            }

            return videos;
        }

        // GET: api/Videos/my-videos
        [Authorize]
        [HttpGet("my-videos")]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetMyVideos(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            long? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("No se pudo identificar al usuario");
            }

            try
            {
                // Limitar el take máximo para evitar consultas muy grandes
                take = Math.Min(take, 100);

                // Obtener solo los videos subidos por el usuario actual con paginación
                var videosQuery = _context.Videos
                    .Where(v => v.UploadedByUserId == userId.Value)
                    .Include(v => v.UploadedByUser)
                    .OrderByDescending(v => v.PublishDate)
                    .Skip(skip)
                    .Take(take)
                    .Select(v => new VideoDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Description = v.Description,
                        PublishDate = v.PublishDate,
                        Uri = v.Uri,
                        ThumbnailUri = v.ThumbnailUri,
                        UploadedByUserId = v.UploadedByUserId,
                        UploadedByUserName = v.UploadedByUser != null ? v.UploadedByUser.Name : null,
                        UploadedByUserProfilePictureUrl = v.UploadedByUser != null ? v.UploadedByUser.ProfilePictureUrl : null,
                        LikesCount = v.Interactions.Count(i => i.Type == InteractionType.Like),
                        FavoritesCount = v.Interactions.Count(i => i.Type == InteractionType.Favorite),
                        ViewsCount = v.Interactions.Count(i => i.Type == InteractionType.View),
                        CommentsCount = v.Comments.Count
                    });

                var videos = await videosQuery.ToListAsync();

                // Aplicar URLs correctas (local o blob storage)
                videos = ApplyCorrectUrls(videos);

                // Agregar información de interacciones del usuario actual
                if (videos.Any())
                {
                    var videoIds = videos.Select(v => v.Id).ToList();
                    var userInteractions = await _context.VideoInteractions
                        .Where(i => i.UserId == userId.Value && videoIds.Contains(i.VideoId))
                        .Select(i => new { i.VideoId, i.Type })
                        .ToListAsync();

                    var interactionsByVideo = userInteractions
                        .GroupBy(i => i.VideoId)
                        .ToDictionary(g => g.Key, g => g.Select(i => i.Type).ToList());

                    foreach (var video in videos)
                    {
                        if (interactionsByVideo.TryGetValue(video.Id, out var interactions))
                        {
                            video.UserHasLiked = interactions.Contains(InteractionType.Like);
                            video.UserHasFavorited = interactions.Contains(InteractionType.Favorite);
                            video.UserHasWatchLater = interactions.Contains(InteractionType.WatchLater);
                            video.UserHasViewed = interactions.Contains(InteractionType.View);
                        }
                    }
                }

                _logger.LogInformation("Obtenidos {Count} videos del usuario {UserId} (skip: {Skip}, take: {Take})",
                    videos.Count, userId, skip, take);

                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener videos del usuario {UserId}", userId);
                return StatusCode(500, "Error interno al obtener mis videos");
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<VideoDto>>> SearchVideos(
            [FromQuery] string query = "",
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            long? userId = User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null;

            try
            {
                IQueryable<Video> videosQuery;

                if (string.IsNullOrWhiteSpace(query))
                {
                    // Si no hay query, devolver todos los videos ordenados por fecha
                    videosQuery = _context.Videos
                        .Include(v => v.UploadedByUser)
                        .OrderByDescending(v => v.PublishDate);
                }
                else
                {
                    // Normalizar la búsqueda
                    query = query.ToLower().Trim();

                    // Buscar en videos, descripción y usuario
                    videosQuery = _context.Videos
                        .Include(v => v.UploadedByUser)
                        .Where(v => v.Name.ToLower().Contains(query) ||
                                   (v.Description != null && v.Description.ToLower().Contains(query)) ||
                                   (v.UploadedByUser != null && v.UploadedByUser.Name.ToLower().Contains(query)))
                        .OrderByDescending(v => v.PublishDate);
                }

                // Aplicar paginación y proyectar a DTO
                var videos = await videosQuery
                    .Skip(skip)
                    .Take(take)
                    .Select(v => new VideoDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Description = v.Description,
                        PublishDate = v.PublishDate,
                        Uri = v.Uri,
                        ThumbnailUri = v.ThumbnailUri,
                        UploadedByUserId = v.UploadedByUserId,
                        UploadedByUserName = v.UploadedByUser != null ? v.UploadedByUser.Name : null,
                        UploadedByUserProfilePictureUrl = v.UploadedByUser != null ? v.UploadedByUser.ProfilePictureUrl : null,
                        LikesCount = v.Interactions.Count(i => i.Type == InteractionType.Like),
                        FavoritesCount = v.Interactions.Count(i => i.Type == InteractionType.Favorite),
                        ViewsCount = v.Interactions.Count(i => i.Type == InteractionType.View),
                        CommentsCount = v.Comments.Count
                    })
                    .ToListAsync();

                // Aplicar URLs correctas
                videos = ApplyCorrectUrls(videos);

                // Agregar información de interacciones del usuario si está autenticado
                if (userId.HasValue && videos.Any())
                {
                    var videoIds = videos.Select(v => v.Id).ToList();
                    var userInteractions = await _context.VideoInteractions
                        .Where(i => i.UserId == userId.Value && videoIds.Contains(i.VideoId))
                        .Select(i => new { i.VideoId, i.Type })
                        .ToListAsync();

                    var interactionsByVideo = userInteractions
                        .GroupBy(i => i.VideoId)
                        .ToDictionary(g => g.Key, g => g.Select(i => i.Type).ToList());

                    foreach (var video in videos)
                    {
                        if (interactionsByVideo.TryGetValue(video.Id, out var interactions))
                        {
                            video.UserHasLiked = interactions.Contains(InteractionType.Like);
                            video.UserHasFavorited = interactions.Contains(InteractionType.Favorite);
                            video.UserHasWatchLater = interactions.Contains(InteractionType.WatchLater);
                            video.UserHasViewed = interactions.Contains(InteractionType.View);
                        }
                    }
                }

                _logger.LogInformation("Búsqueda de videos: query='{Query}', skip={Skip}, take={Take}, found={Count}",
                    query ?? "empty", skip, take, videos.Count);

                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en búsqueda de videos con query: {Query}", query);
                return StatusCode(500, "Error interno en la búsqueda");
            }
        }

        // GET: api/Videos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<VideoDto>> GetVideo(long id)
        {
            // Verificar que el video existe
            var video = await _context.Videos
                .Where(v => v.Id == id)
                .Select(v => new VideoDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Description = v.Description,
                    PublishDate = v.PublishDate,
                    Uri = v.Uri, // Se corregirá después con ApplyCorrectUrls
                    ThumbnailUri = v.ThumbnailUri, // Se corregirá después con ApplyCorrectUrls
                    UploadedByUserId = v.UploadedByUserId,
                    UploadedByUserName = v.UploadedByUser != null ? v.UploadedByUser.Name : null,
                    UploadedByUserProfilePictureUrl = v.UploadedByUser != null ? v.UploadedByUser.ProfilePictureUrl : null,
                    LikesCount = v.Interactions.Count(i => i.Type == InteractionType.Like),
                    FavoritesCount = v.Interactions.Count(i => i.Type == InteractionType.Favorite),
                    ViewsCount = v.Interactions.Count(i => i.Type == InteractionType.View),
                    CommentsCount = v.Comments.Count
                })
                .FirstOrDefaultAsync();

            if (video == null)
            {
                return NotFound();
            }

            // Aplicar URLs correctas (local o blob storage)
            video = ApplyCorrectUrls(video);

            // Si hay un usuario autenticado, añadir información sobre sus interacciones
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                {
                    // Obtener todas las interacciones del usuario con este video
                    var interactions = await _context.VideoInteractions
                        .Where(i => i.UserId == userId.Value && i.VideoId == id)
                        .Select(i => i.Type)
                        .ToListAsync();

                    video.UserHasLiked = interactions.Contains(InteractionType.Like);
                    video.UserHasFavorited = interactions.Contains(InteractionType.Favorite);
                    video.UserHasWatchLater = interactions.Contains(InteractionType.WatchLater);
                    video.UserHasViewed = interactions.Contains(InteractionType.View);
                }
            }

            return video;
        }

        // PUT: api/Videos/5
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutVideo(long id, VideoUpdateDto videoDto)
        {
            if (id != videoDto.Id)
            {
                return BadRequest("El ID en la URL no coincide con el ID en los datos");
            }

            // Obtener el video existente
            var video = await _context.Videos.FindAsync(id);
            if (video == null)
            {
                return NotFound();
            }

            // Verificar si el usuario actual es el propietario del video
            var userId = GetCurrentUserId();
            if (video.UploadedByUserId.HasValue && userId.HasValue && video.UploadedByUserId.Value != userId.Value)
            {
                return Forbid("Solo el propietario puede editar este video");
            }

            // Actualizar solo los campos permitidos
            video.Name = videoDto.Name;
            video.Description = videoDto.Description;

            _context.Entry(video).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VideoExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Videos
        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(5368709120)] // 5GB en bytes
        [RequestFormLimits(MultipartBodyLengthLimit = 5368709120)] // 5GB en bytes
        public async Task<ActionResult<VideoDto>> PostVideo([FromForm] VideoUploadDto uploadDto)
        {
            try
            {
                if (uploadDto.VideoFile == null)
                {
                    return BadRequest("No video file uploaded");
                }

                // Verificar que es un archivo de video por su extensión
                string extension = Path.GetExtension(uploadDto.VideoFile.FileName).ToLowerInvariant();
                string[] allowedVideoExtensions = { ".mp4", ".mov", ".avi", ".wmv", ".mkv", ".webm", ".flv", ".m4v" };

                if (!allowedVideoExtensions.Contains(extension))
                {
                    return BadRequest($"Invalid file type. Allowed video formats are: {string.Join(", ", allowedVideoExtensions)}");
                }

                // Validar thumbnail si se proporciona
                string? thumbnailRelativePath = null;
                if (uploadDto.ThumbnailFile != null)
                {
                    string thumbnailExtension = Path.GetExtension(uploadDto.ThumbnailFile.FileName).ToLowerInvariant();
                    string[] allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

                    if (!allowedImageExtensions.Contains(thumbnailExtension))
                    {
                        return BadRequest($"Invalid thumbnail type. Allowed image formats are: {string.Join(", ", allowedImageExtensions)}");
                    }

                    // Verificar tamaño del thumbnail (máximo 5MB)
                    if (uploadDto.ThumbnailFile.Length > 5 * 1024 * 1024)
                    {
                        return BadRequest("Thumbnail file size exceeds 5MB limit");
                    }
                }

                // Obtener el ID del usuario actual
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized("No se pudo identificar al usuario");
                }

                // Obtener el nombre del usuario para incluirlo en la respuesta
                var userName = await _context.Users
                    .Where(u => u.Id == userId.Value)
                    .Select(u => u.Name)
                    .FirstOrDefaultAsync() ?? "Usuario";

                // Guardar el archivo de video usando el servicio de almacenamiento
                string videoRelativePath = await _fileService.SaveFileAsync(uploadDto.VideoFile, "Videos");
                string fullVideoPath = Path.Combine(_fileService.GetUploadsBaseDirectory(), videoRelativePath);

                // Guardar thumbnail si se proporciona, o generar uno automáticamente
                if (uploadDto.ThumbnailFile != null)
                {
                    thumbnailRelativePath = await _fileService.SaveFileAsync(uploadDto.ThumbnailFile, "Thumbnails");
                    _logger.LogInformation("Thumbnail proporcionado por usuario para video: {VideoName}", uploadDto.Name);
                }
                else
                {
                    // Generar thumbnail automáticamente desde el video
                    _logger.LogInformation("Generando thumbnail automático para video: {VideoName}", uploadDto.Name);
                    thumbnailRelativePath = await _thumbnailGenerator.GenerateThumbnailFromVideoAsync(
                        fullVideoPath,
                        uploadDto.VideoFile.FileName);

                    if (thumbnailRelativePath != null)
                    {
                        _logger.LogInformation("Thumbnail generado automáticamente: {ThumbnailPath}", thumbnailRelativePath);
                    }
                    else
                    {
                        _logger.LogWarning("No se pudo generar thumbnail automático para video: {VideoName}", uploadDto.Name);
                    }
                }

                // Crear un nuevo objeto Video (guardamos las rutas como están, sin URLs completas)
                var videoEntity = new Video
                {
                    Name = uploadDto.Name,
                    Description = uploadDto.Description,
                    PublishDate = DateTime.UtcNow,
                    Uri = videoRelativePath, // Guardamos solo la ruta relativa
                    ThumbnailUri = thumbnailRelativePath, // Guardamos solo la ruta relativa
                    UploadedByUserId = userId
                };

                // Guardar en la base de datos
                _context.Videos.Add(videoEntity);
                await _context.SaveChangesAsync();

                // Crear DTO para la respuesta con URLs correctas
                var responseDto = new VideoDto
                {
                    Id = videoEntity.Id,
                    Name = videoEntity.Name,
                    Description = videoEntity.Description,
                    PublishDate = videoEntity.PublishDate,
                    Uri = videoEntity.Uri, // Se corregirá a continuación
                    ThumbnailUri = videoEntity.ThumbnailUri, // Se corregirá a continuación
                    UploadedByUserId = userId,
                    UploadedByUserName = userName,
                    LikesCount = 0,
                    FavoritesCount = 0,
                    ViewsCount = 0,
                    CommentsCount = 0,
                    UserHasLiked = false,
                    UserHasFavorited = false,
                    UserHasWatchLater = false,
                    UserHasViewed = false
                };

                // Aplicar URLs correctas (local o blob storage) para la respuesta
                responseDto = ApplyCorrectUrls(responseDto);

                // Retornar el video creado con URLs correctas
                return CreatedAtAction(
                    nameof(GetVideo),
                    new { id = videoEntity.Id },
                    responseDto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid video upload attempt: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading video");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error uploading video file");
            }
        }

        // DELETE: api/Videos/5
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVideo(long id)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video == null)
            {
                return NotFound();
            }

            // Verificar si el usuario actual es el propietario del video
            var userId = GetCurrentUserId();
            if (video.UploadedByUserId.HasValue && userId.HasValue && video.UploadedByUserId.Value != userId.Value)
            {
                return Forbid("Solo el propietario puede eliminar este video");
            }

            // Eliminar archivos físicos (usar las rutas tal como están almacenadas)
            try
            {
                if (!string.IsNullOrEmpty(video.Uri))
                {
                    await _fileService.DeleteFileAsync(video.Uri);
                }

                if (!string.IsNullOrEmpty(video.ThumbnailUri))
                {
                    await _fileService.DeleteFileAsync(video.ThumbnailUri);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete files for video ID {VideoId}: {Message}", id, ex.Message);
                // Continuamos con la eliminación del registro aunque los archivos físicos no se puedan eliminar
            }

            // Eliminar todas las interacciones asociadas
            var interactions = _context.VideoInteractions.Where(i => i.VideoId == id);
            _context.VideoInteractions.RemoveRange(interactions);

            // Eliminar todos los comentarios asociados
            var comments = _context.VideoComments.Where(c => c.VideoId == id);
            _context.VideoComments.RemoveRange(comments);

            // Eliminar el registro
            _context.Videos.Remove(video);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool VideoExists(long id)
        {
            return _context.Videos.Any(e => e.Id == id);
        }

        /// <summary>
        /// Obtiene el ID del usuario autenticado
        /// </summary>
        private long? GetCurrentUserId()
        {
            var internalIdClaim = User.FindFirst("InternalId")?.Value;
            if (string.IsNullOrEmpty(internalIdClaim) || !long.TryParse(internalIdClaim, out var userId))
            {
                _logger.LogWarning("No se pudo obtener el ID interno del usuario autenticado");
                return null;
            }

            return userId;
        }

        #region URL Helper Methods

        /// <summary>
        /// Aplica las URLs correctas (local o blob storage) a un VideoDto
        /// </summary>
        private VideoDto ApplyCorrectUrls(VideoDto video)
        {
            // Aplicar URL correcta para el video
            if (!string.IsNullOrEmpty(video.Uri))
            {
                video.Uri = _fileService.GetFileUrl(video.Uri);
            }

            // Aplicar URL correcta para el thumbnail
            if (!string.IsNullOrEmpty(video.ThumbnailUri))
            {
                video.ThumbnailUri = _fileService.GetFileUrl(video.ThumbnailUri);
            }

            return video;
        }

        /// <summary>
        /// Aplica las URLs correctas a una lista de VideoDto
        /// </summary>
        private List<VideoDto> ApplyCorrectUrls(List<VideoDto> videos)
        {
            foreach (var video in videos)
            {
                ApplyCorrectUrls(video);
            }
            return videos;
        }

        #endregion

    #if DEBUG
        /// <summary>
        /// ENDPOINT TEMPORAL SOLO PARA TESTING - Genera videos de prueba
        /// </summary>
        [HttpPost("generate-test-data")]
        [ApiExplorerSettings(IgnoreApi = false)]
        public async Task<ActionResult> GenerateTestData([FromQuery] int count = 50)
        {
            try
            {
                // Buscar un video y thumbnail existente para reutilizar
                var existingVideo = await _context.Videos.FirstOrDefaultAsync();
                if (existingVideo == null)
                {
                    return BadRequest("Necesitas tener al menos 1 video existente para usar como plantilla");
                }

                // Obtener usuarios existentes
                var users = await _context.Users.ToListAsync();
                if (!users.Any())
                {
                    return BadRequest("Necesitas tener al menos 1 usuario en la BD");
                }

                var random = new Random();
                var videoTitles = new[]
                {
            "Técnicas Avanzadas de Cardiología Intervencionista",
            "Angioplastia Coronaria: Casos Complejos",
            "Stents de Nueva Generación en 2024",
            "Cirugía Mínimamente Invasiva del Corazón",
            "Diagnóstico por Imagen Cardiovascular",
            "Tratamiento de Arritmias Complejas",
            "Cateterismo Cardíaco: Guía Completa",
            "Emergencias Cardiovasculares en UCI",
            "Ecocardiografía Transesofágica Avanzada",
            "Procedimientos Endovasculares Periféricos",
            "Valvuloplastia Percutánea: Últimos Avances",
            "Manejo de Shock Cardiogénico",
            "Técnicas de Ablación Cardíaca",
            "Implante de Marcapasos y DAI",
            "Cirugía de Bypass Coronario",
            "Tratamiento del Infarto Agudo",
            "Medicina Regenerativa Cardiovascular",
            "Terapias Génicas en Cardiología",
            "Dispositivos de Asistencia Ventricular",
            "Trasplante Cardíaco: Indicaciones",
            "Cardiología Pediátrica Intervencionista",
            "Prevención Cardiovascular Primaria",
            "Rehabilitación Cardíaca Post-Operatoria",
            "Farmacología Cardiovascular Moderna",
            "Biomarcadores en Síndrome Coronario"
        };

                var descriptions = new[]
                {
            "Una explicación detallada de las técnicas más modernas en el campo cardiovascular.",
            "Casos clínicos reales con análisis paso a paso de los procedimientos realizados.",
            "Revisión completa de la literatura científica más actual en cardiología.",
            "Presentación de casos complejos con enfoque multidisciplinario.",
            "Guía práctica para residentes y especialistas en formación.",
            "Análisis de complicaciones y cómo manejarlas en tiempo real.",
            "Demostración práctica de nuevas tecnologías en el laboratorio.",
            "Sesión clínica interactiva con expertos internacionales.",
            "Tutorial paso a paso con imágenes de alta calidad.",
            "Conferencia magistral dictada en congreso internacional."
        };

                var videosToCreate = new List<Video>();
                var baseDate = DateTime.UtcNow.AddDays(-365); // Empezar hace un año

                for (int i = 0; i < count; i++)
                {
                    var title = videoTitles[random.Next(videoTitles.Length)];
                    var description = descriptions[random.Next(descriptions.Length)];
                    var user = users[random.Next(users.Count)];

                    // Variar fechas de publicación
                    var publishDate = baseDate.AddDays(random.Next(0, 365)).AddHours(random.Next(0, 24));

                    // Agregar variación al título para búsquedas
                    var titleVariations = new[]
                    {
                $"{title} - Parte {i + 1}",
                $"{title}: Experiencia Clínica",
                $"{title} - Casos Prácticos",
                $"{title}: Revisión 2024",
                $"Masterclass: {title}",
                $"{title} - Enfoque Actualizado"
            };

                    var finalTitle = titleVariations[random.Next(titleVariations.Length)];

                    videosToCreate.Add(new Video
                    {
                        Name = finalTitle,
                        Description = $"{description} Video de demostración número {i + 1} para testing de la plataforma.",
                        PublishDate = publishDate,
                        Uri = existingVideo.Uri, // Reutilizar el archivo físico
                        ThumbnailUri = existingVideo.ThumbnailUri, // Reutilizar thumbnail
                        UploadedByUserId = user.Id
                    });
                }

                _context.Videos.AddRange(videosToCreate);
                await _context.SaveChangesAsync();

                // Generar algunas interacciones aleatorias
                await GenerateRandomInteractions(videosToCreate, users, random);

                _logger.LogInformation("Generados {Count} videos de prueba exitosamente", count);

                return Ok(new
                {
                    message = $"✅ Generados {count} videos de prueba exitosamente",
                    videosCreated = count,
                    usersUsed = users.Count,
                    dateRange = $"{baseDate:yyyy-MM-dd} to {DateTime.UtcNow:yyyy-MM-dd}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando datos de prueba");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        private async Task GenerateRandomInteractions(List<Video> videos, List<User> users, Random random)
        {
            var interactions = new List<VideoInteraction>();

            foreach (var video in videos.Take(30)) // Solo para algunos videos
            {
                var numInteractions = random.Next(0, Math.Min(5, users.Count));
                var selectedUsers = users.OrderBy(x => random.Next()).Take(numInteractions);

                foreach (var user in selectedUsers)
                {
                    // Probabilidad de diferentes interacciones
                    if (random.NextDouble() < 0.7) // 70% probabilidad de view
                    {
                        interactions.Add(new VideoInteraction
                        {
                            VideoId = video.Id,
                            UserId = user.Id,
                            Type = InteractionType.View,
                            CreatedAt = video.PublishDate.AddDays(random.Next(1, 30))
                        });
                    }

                    if (random.NextDouble() < 0.3) // 30% probabilidad de like
                    {
                        interactions.Add(new VideoInteraction
                        {
                            VideoId = video.Id,
                            UserId = user.Id,
                            Type = InteractionType.Like,
                            CreatedAt = video.PublishDate.AddDays(random.Next(1, 30))
                        });
                    }

                    if (random.NextDouble() < 0.1) // 10% probabilidad de favorite
                    {
                        interactions.Add(new VideoInteraction
                        {
                            VideoId = video.Id,
                            UserId = user.Id,
                            Type = InteractionType.Favorite,
                            CreatedAt = video.PublishDate.AddDays(random.Next(1, 30))
                        });
                    }
                }
            }

            if (interactions.Any())
            {
                _context.VideoInteractions.AddRange(interactions);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Generadas {Count} interacciones de prueba", interactions.Count);
            }
        }
    #endif
    }
}