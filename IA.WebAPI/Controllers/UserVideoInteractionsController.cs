using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IA.WebAPI.Models;
using IA.WebAPI.Models.DTOs;
using IA.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IA.WebAPI.Controllers
{
    /// <summary>
    /// Controlador para gestionar interacciones de usuarios con videos
    /// </summary>
    [Authorize]
    [Route("api/user-videos")]
    [ApiController]
    public class UserVideoInteractionsController : ControllerBase
    {
        private readonly IAContext _context;
        private readonly ILogger<UserVideoInteractionsController> _logger;
        private readonly IFileStorageService _fileService;

        public UserVideoInteractionsController(
            IAContext context,
            ILogger<UserVideoInteractionsController> logger,
            IFileStorageService fileService)
        {
            _context = context;
            _logger = logger;
            _fileService = fileService;
        }

        #region Obtener videos por tipo de interacción

        /// <summary>
        /// Obtiene los videos que le gustan al usuario autenticado con paginación
        /// </summary>
        [HttpGet("likes")]
        [ProducesResponseType(typeof(IEnumerable<VideoDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetUserLikedVideos(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            return await GetVideosByInteractionTypeWithPagination(InteractionType.Like, skip, take);
        }

        /// <summary>
        /// Obtiene los videos favoritos del usuario autenticado con paginación
        /// </summary>
        [HttpGet("favorites")]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetUserFavoriteVideos(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            return await GetVideosByInteractionTypeWithPagination(InteractionType.Favorite, skip, take);
        }

        /// <summary>
        /// Obtiene los videos marcados para ver más tarde por el usuario autenticado con paginación
        /// </summary>
        [HttpGet("watch-later")]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetWatchLaterVideos(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            return await GetVideosByInteractionTypeWithPagination(InteractionType.WatchLater, skip, take);
        }

        /// <summary>
        /// Obtiene los videos vistos por el usuario autenticado (historial) con paginación
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<VideoDto>>> GetViewedVideos(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            return await GetVideosByInteractionTypeWithPagination(InteractionType.View, skip, take);
        }

        /// <summary>
        /// Método común para obtener videos por tipo de interacción
        /// </summary>
        private async Task<ActionResult<IEnumerable<VideoDto>>> GetVideosByInteractionType(InteractionType type)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            try
            {
                // Cambiar para retornar VideoDto con URLs correctas
                var videosQuery = _context.VideoInteractions
                    .Where(i => i.UserId == userId.Value && i.Type == type)
                    .Include(i => i.Video)
                    .ThenInclude(v => v.UploadedByUser)
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => new VideoDto
                    {
                        Id = i.Video.Id,
                        Name = i.Video.Name,
                        Description = i.Video.Description,
                        PublishDate = i.Video.PublishDate,
                        Uri = i.Video.Uri, // Se corregirá después con ApplyCorrectUrls
                        ThumbnailUri = i.Video.ThumbnailUri, // Se corregirá después con ApplyCorrectUrls
                        UploadedByUserId = i.Video.UploadedByUserId,
                        UploadedByUserName = i.Video.UploadedByUser != null ? i.Video.UploadedByUser.Name : null,
                        UploadedByUserProfilePictureUrl = i.Video.UploadedByUser != null ? i.Video.UploadedByUser.ProfilePictureUrl : null,
                        LikesCount = i.Video.Interactions.Count(interaction => interaction.Type == InteractionType.Like),
                        FavoritesCount = i.Video.Interactions.Count(interaction => interaction.Type == InteractionType.Favorite),
                        ViewsCount = i.Video.Interactions.Count(interaction => interaction.Type == InteractionType.View),
                        CommentsCount = i.Video.Comments.Count
                    });

                var videos = await videosQuery.ToListAsync();

                // Aplicar URLs correctas (Azure Blob Storage con SAS tokens)
                videos = ApplyCorrectUrls(videos);

                // Agregar información de interacciones del usuario actual
                var userInteractions = await _context.VideoInteractions
                    .Where(i => i.UserId == userId.Value)
                    .Select(i => new { i.VideoId, i.Type })
                    .ToListAsync();

                var interactionsByVideo = userInteractions
                    .GroupBy(i => i.VideoId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(i => i.Type).ToList()
                    );

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

                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener videos con interacción {Type} para usuario {UserId}", type, userId);
                return StatusCode(500, "Error interno al obtener videos");
            }
        }

        #endregion

        #region Agregar o quitar interacciones

        /// <summary>
        /// Agrega una interacción de like a un video
        /// </summary>
        /// <param name="videoId">ID del video a dar like</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("like/{videoId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> LikeVideo(long videoId)
        {
            return await AddInteraction(videoId, InteractionType.Like, "like");
        }

        /// <summary>
        /// Quita una interacción de tipo Like de un video
        /// </summary>
        [HttpDelete("like/{videoId}")]
        public async Task<ActionResult> UnlikeVideo(long videoId)
        {
            return await RemoveInteraction(videoId, InteractionType.Like, "like");
        }

        /// <summary>
        /// Agrega una interacción de tipo Favorite a un video
        /// </summary>
        [HttpPost("favorite/{videoId}")]
        public async Task<ActionResult> AddToFavorites(long videoId)
        {
            return await AddInteraction(videoId, InteractionType.Favorite, "favoritos");
        }

        /// <summary>
        /// Quita una interacción de tipo Favorite de un video
        /// </summary>
        [HttpDelete("favorite/{videoId}")]
        public async Task<ActionResult> RemoveFromFavorites(long videoId)
        {
            return await RemoveInteraction(videoId, InteractionType.Favorite, "favoritos");
        }

        /// <summary>
        /// Agrega una interacción de tipo WatchLater a un video
        /// </summary>
        [HttpPost("watch-later/{videoId}")]
        public async Task<ActionResult> AddToWatchLater(long videoId)
        {
            return await AddInteraction(videoId, InteractionType.WatchLater, "ver más tarde");
        }

        /// <summary>
        /// Quita una interacción de tipo WatchLater de un video
        /// </summary>
        [HttpDelete("watch-later/{videoId}")]
        public async Task<ActionResult> RemoveFromWatchLater(long videoId)
        {
            return await RemoveInteraction(videoId, InteractionType.WatchLater, "ver más tarde");
        }

        /// <summary>
        /// Registra que el usuario ha visto un video
        /// </summary>
        [HttpPost("view/{videoId}")]
        public async Task<ActionResult> RegisterVideoView(long videoId)
        {
            return await AddInteraction(videoId, InteractionType.View, "historial", allowMultiple: true);
        }

        /// <summary>
        /// Elimina un video del historial
        /// </summary>
        [HttpDelete("view/{videoId}")]
        public async Task<ActionResult> RemoveFromHistory(long videoId)
        {
            return await RemoveInteraction(videoId, InteractionType.View, "historial", removeAll: true);
        }

        /// <summary>
        /// Limpia todo el historial de visualizaciones
        /// </summary>
        [HttpDelete("history")]
        public async Task<ActionResult> ClearHistory()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var userViews = _context.VideoInteractions
                    .Where(i => i.UserId == userId.Value && i.Type == InteractionType.View);

                _context.VideoInteractions.RemoveRange(userViews);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Historial eliminado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar historial del usuario {UserId}", userId);
                return StatusCode(500, "Error interno al limpiar historial");
            }
        }

        #endregion

        #region Métodos auxiliares para interacciones

        /// <summary>
        /// Método común para añadir una interacción
        /// </summary>
        private async Task<ActionResult> AddInteraction(long videoId, InteractionType type, string typeName, bool allowMultiple = false)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            try
            {
                // Verificar que el video existe
                var video = await _context.Videos.FindAsync(videoId);
                if (video == null)
                {
                    return NotFound($"Video con ID {videoId} no encontrado");
                }

                // Si no se permiten múltiples interacciones del mismo tipo, verificar si ya existe
                if (!allowMultiple)
                {
                    var existingInteraction = await _context.VideoInteractions
                        .FirstOrDefaultAsync(i => i.UserId == userId.Value
                                                   && i.VideoId == videoId
                                                   && i.Type == type);

                    if (existingInteraction != null)
                    {
                        return Ok(new { message = $"Este video ya está en tu {typeName}" });
                    }
                }

                // Crear nueva interacción
                var interaction = new VideoInteraction
                {
                    UserId = userId.Value,
                    VideoId = videoId,
                    Type = type,
                    CreatedAt = DateTime.UtcNow
                };

                _context.VideoInteractions.Add(interaction);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Video agregado a {typeName} correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar interacción {Type} al video {VideoId} por usuario {UserId}",
                    type, videoId, userId);
                return StatusCode(500, $"Error interno al procesar {typeName}");
            }
        }

        /// <summary>
        /// Método común para eliminar una interacción
        /// </summary>
        private async Task<ActionResult> RemoveInteraction(long videoId, InteractionType type, string typeName, bool removeAll = false)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            try
            {
                // Buscar la interacción a eliminar
                IQueryable<VideoInteraction> interactionsQuery = _context.VideoInteractions
                    .Where(i => i.UserId == userId.Value
                                 && i.VideoId == videoId
                                 && i.Type == type);

                // Si removeAll es false, solo eliminar una interacción
                if (!removeAll)
                {
                    var interaction = await interactionsQuery.FirstOrDefaultAsync();

                    if (interaction == null)
                    {
                        return NotFound($"Este video no está en tu {typeName}");
                    }

                    _context.VideoInteractions.Remove(interaction);
                }
                else
                {
                    // Eliminar todas las interacciones de este tipo para este video
                    var interactions = await interactionsQuery.ToListAsync();

                    if (!interactions.Any())
                    {
                        return NotFound($"Este video no está en tu {typeName}");
                    }

                    _context.VideoInteractions.RemoveRange(interactions);
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = $"Video eliminado de {typeName} correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar interacción {Type} del video {VideoId} por usuario {UserId}",
                    type, videoId, userId);
                return StatusCode(500, $"Error interno al eliminar de {typeName}");
            }
        }

        #endregion

        #region Helpers

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

        /// <summary>
        /// Método común para obtener videos por tipo de interacción con paginación
        /// </summary>
        private async Task<ActionResult<IEnumerable<VideoDto>>> GetVideosByInteractionTypeWithPagination(
            InteractionType type,
            int skip = 0,
            int take = 20)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            try
            {
                // Limitar el take máximo para evitar consultas muy grandes
                take = Math.Min(take, 100);

                var videosQuery = _context.VideoInteractions
                    .Where(i => i.UserId == userId.Value && i.Type == type)
                    .Include(i => i.Video)
                    .ThenInclude(v => v.UploadedByUser)
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(i => new VideoDto
                    {
                        Id = i.Video.Id,
                        Name = i.Video.Name,
                        Description = i.Video.Description,
                        PublishDate = i.Video.PublishDate,
                        Uri = i.Video.Uri,
                        ThumbnailUri = i.Video.ThumbnailUri,
                        UploadedByUserId = i.Video.UploadedByUserId,
                        UploadedByUserName = i.Video.UploadedByUser != null ? i.Video.UploadedByUser.Name : null,
                        UploadedByUserProfilePictureUrl = i.Video.UploadedByUser != null ? i.Video.UploadedByUser.ProfilePictureUrl : null,
                        LikesCount = i.Video.Interactions.Count(interaction => interaction.Type == InteractionType.Like),
                        FavoritesCount = i.Video.Interactions.Count(interaction => interaction.Type == InteractionType.Favorite),
                        ViewsCount = i.Video.Interactions.Count(interaction => interaction.Type == InteractionType.View),
                        CommentsCount = i.Video.Comments.Count
                    });

                var videos = await videosQuery.ToListAsync();

                // Aplicar URLs correctas
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

                _logger.LogInformation("Obtenidos {Count} videos con interacción {Type} para usuario {UserId} (skip: {Skip}, take: {Take})",
                    videos.Count, type, userId, skip, take);

                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener videos con interacción {Type} para usuario {UserId}", type, userId);
                return StatusCode(500, "Error interno al obtener videos");
            }
        }

        #endregion

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
    }
}