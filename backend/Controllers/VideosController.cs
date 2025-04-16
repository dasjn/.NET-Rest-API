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
        private readonly ILogger<VideosController> _logger;

        public VideosController(
            IAContext context,
            IFileStorageService fileService,
            ILogger<VideosController> logger)
        {
            _context = context;
            _fileService = fileService;
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
                    Uri = v.Uri,
                    UploadedByUserId = v.UploadedByUserId,
                    UploadedByUserName = v.UploadedByUser != null ? v.UploadedByUser.Name : null,
                    UploadedByUserProfilePictureUrl = v.UploadedByUser != null ? v.UploadedByUser.ProfilePictureUrl : null,
                    LikesCount = v.Interactions.Count(i => i.Type == InteractionType.Like),
                    FavoritesCount = v.Interactions.Count(i => i.Type == InteractionType.Favorite),
                    ViewsCount = v.Interactions.Count(i => i.Type == InteractionType.View),
                    CommentsCount = v.Comments.Count
                });

            // Si el usuario está autenticado, añadir información sobre sus interacciones
            var videos = await videosQuery.ToListAsync();

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
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetMyVideos()
        {
            long? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("No se pudo identificar al usuario");
            }

            // Obtener solo los videos subidos por el usuario actual, con toda la información necesaria
            var videosQuery = _context.Videos
                .Where(v => v.UploadedByUserId == userId.Value)
                .Select(v => new VideoDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Description = v.Description,
                    PublishDate = v.PublishDate,
                    Uri = v.Uri,
                    UploadedByUserId = v.UploadedByUserId,
                    UploadedByUserName = v.UploadedByUser != null ? v.UploadedByUser.Name : null,
                    UploadedByUserProfilePictureUrl = v.UploadedByUser != null ? v.UploadedByUser.ProfilePictureUrl : null,
                    LikesCount = v.Interactions.Count(i => i.Type == InteractionType.Like),
                    FavoritesCount = v.Interactions.Count(i => i.Type == InteractionType.Favorite),
                    ViewsCount = v.Interactions.Count(i => i.Type == InteractionType.View),
                    CommentsCount = v.Comments.Count
                });

            // Obtener los videos
            var videos = await videosQuery.ToListAsync();

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

            return videos;
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
                    Uri = v.Uri,
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
        [RequestSizeLimit(5368709120)] // 5GB en bytes
        [RequestFormLimits(MultipartBodyLengthLimit = 5368709120)] // 5GB en bytes
        public async Task<ActionResult<VideoDto>> PostVideo([FromForm] IFormFile videoFile, [FromForm] string name, [FromForm] string description)
        {
            try
            {
                if (videoFile == null)
                {
                    return BadRequest("No video file uploaded");
                }

                // Verificar que es un archivo de video por su extensión
                string extension = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
                string[] allowedVideoExtensions = { ".mp4", ".mov", ".avi", ".wmv", ".mkv", ".webm", ".flv", ".m4v" };

                if (!allowedVideoExtensions.Contains(extension))
                {
                    return BadRequest($"Invalid file type. Allowed video formats are: {string.Join(", ", allowedVideoExtensions)}");
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

                // Guardar el archivo usando el servicio de almacenamiento
                string relativePath = await _fileService.SaveFileAsync(videoFile, "Videos");

                // Mantener el formato de ruta original para compatibilidad
                string originalStylePath = Path.Combine("Uploads", "Videos", Path.GetFileName(relativePath));

                // Crear un nuevo objeto Video
                var videoEntity = new Video
                {
                    Name = name,
                    Description = description,
                    PublishDate = DateTime.UtcNow,
                    Uri = originalStylePath, // Usamos el formato que espera el frontend
                    UploadedByUserId = userId // Asignar el ID del usuario que sube el video
                };

                // Guardar en la base de datos
                _context.Videos.Add(videoEntity);
                await _context.SaveChangesAsync();

                // Retornar el video creado con formato DTO
                return CreatedAtAction(
                    nameof(GetVideo),
                    new { id = videoEntity.Id },
                    new VideoDto
                    {
                        Id = videoEntity.Id,
                        Name = videoEntity.Name,
                        Description = videoEntity.Description,
                        PublishDate = videoEntity.PublishDate,
                        Uri = videoEntity.Uri,
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
                    });
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

            // Convertir la ruta al formato que espera FileStorageService
            string storageServicePath = video.Uri.Replace("Uploads/", "").Replace("Uploads\\", "");

            // Eliminar el archivo físico
            try
            {
                await _fileService.DeleteFileAsync(storageServicePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete video file for ID {VideoId}: {Message}", id, ex.Message);
                // Continuamos con la eliminación del registro aunque el archivo físico no se pueda eliminar
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
    }
}