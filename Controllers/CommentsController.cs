using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IA.WebAPI.Models;
using IA.WebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IA.WebAPI.Controllers
{
    [RequireHttps]
    [Route("api/videos/{videoId}/comments")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly IAContext _context;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(
            IAContext context,
            ILogger<CommentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los comentarios de primer nivel de un video
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommentDto>>> GetVideoComments(long videoId)
        {
            try
            {
                // Verificar que el video existe
                if (!await _context.Videos.AnyAsync(v => v.Id == videoId))
                {
                    return NotFound($"Video con ID {videoId} no encontrado");
                }

                // ID del usuario actual (si está autenticado)
                long? currentUserId = User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null;

                // Obtener los comentarios de primer nivel (sin padre)
                var topLevelComments = await _context.VideoComments
                    .Where(c => c.VideoId == videoId && c.ParentCommentId == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .Include(c => c.User)
                    .Include(c => c.Replies)
                        .ThenInclude(r => r.User)
                    .ToListAsync();

                // Convertir a DTOs
                var commentDtos = topLevelComments.Select(c => new CommentDto
                {
                    Id = c.Id,
                    VideoId = c.VideoId,
                    UserId = c.UserId,
                    UserName = c.User.Name,
                    UserProfilePictureUrl = c.User.ProfilePictureUrl,
                    ParentCommentId = c.ParentCommentId,
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    IsEdited = c.IsEdited,
                    IsOwnComment = currentUserId.HasValue && c.UserId == currentUserId.Value,
                    Replies = c.Replies.OrderBy(r => r.CreatedAt).Select(r => new CommentDto
                    {
                        Id = r.Id,
                        VideoId = r.VideoId,
                        UserId = r.UserId,
                        UserName = r.User.Name,
                        UserProfilePictureUrl = r.User.ProfilePictureUrl,
                        ParentCommentId = r.ParentCommentId,
                        Content = r.Content,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsEdited = r.IsEdited,
                        IsOwnComment = currentUserId.HasValue && r.UserId == currentUserId.Value,
                        Replies = new List<CommentDto>() // Las respuestas a respuestas se cargarán bajo demanda
                    }).ToList()
                }).ToList();

                return Ok(commentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comentarios del video {VideoId}", videoId);
                return StatusCode(500, "Error interno al obtener comentarios");
            }
        }

        /// <summary>
        /// Añade un nuevo comentario a un video
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<CommentDto>> AddComment(long videoId, CreateCommentDto commentDto)
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

                // Si es una respuesta, verificar que el comentario padre existe
                if (commentDto.ParentCommentId.HasValue)
                {
                    var parentComment = await _context.VideoComments.FindAsync(commentDto.ParentCommentId.Value);
                    if (parentComment == null)
                    {
                        return NotFound($"Comentario padre con ID {commentDto.ParentCommentId.Value} no encontrado");
                    }

                    // Verificar que el comentario padre pertenece al mismo video
                    if (parentComment.VideoId != videoId)
                    {
                        return BadRequest("El comentario padre no pertenece a este video");
                    }

                    // Verificar que no es una respuesta a una respuesta (máximo un nivel de anidamiento)
                    if (parentComment.ParentCommentId != null)
                    {
                        return BadRequest("No se permiten respuestas a respuestas");
                    }
                }

                // Crear el nuevo comentario
                var comment = new VideoComment
                {
                    VideoId = videoId,
                    UserId = userId.Value,
                    ParentCommentId = commentDto.ParentCommentId,
                    Content = commentDto.Content,
                    CreatedAt = DateTime.UtcNow
                };

                _context.VideoComments.Add(comment);
                await _context.SaveChangesAsync();

                // Obtener el usuario para incluir su información en la respuesta
                var user = await _context.Users.FindAsync(userId.Value);

                // Crear DTO para la respuesta
                var response = new CommentDto
                {
                    Id = comment.Id,
                    VideoId = comment.VideoId,
                    UserId = comment.UserId,
                    UserName = user?.Name ?? "Usuario desconocido",
                    UserProfilePictureUrl = user?.ProfilePictureUrl,
                    ParentCommentId = comment.ParentCommentId,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    IsEdited = false,
                    IsOwnComment = true,
                    Replies = new List<CommentDto>()
                };

                return CreatedAtAction(nameof(GetComment), new { videoId, commentId = comment.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear comentario en video {VideoId} por usuario {UserId}", videoId, userId);
                return StatusCode(500, "Error interno al crear comentario");
            }
        }

        /// <summary>
        /// Obtiene un comentario específico por su ID
        /// </summary>
        [HttpGet("{commentId}")]
        public async Task<ActionResult<CommentDto>> GetComment(long videoId, long commentId)
        {
            try
            {
                var comment = await _context.VideoComments
                    .Where(c => c.Id == commentId && c.VideoId == videoId)
                    .Include(c => c.User)
                    .Include(c => c.Replies)
                        .ThenInclude(r => r.User)
                    .FirstOrDefaultAsync();

                if (comment == null)
                {
                    return NotFound($"Comentario con ID {commentId} no encontrado en el video {videoId}");
                }

                // ID del usuario actual (si está autenticado)
                long? currentUserId = User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null;

                // Convertir a DTO
                var commentDto = new CommentDto
                {
                    Id = comment.Id,
                    VideoId = comment.VideoId,
                    UserId = comment.UserId,
                    UserName = comment.User.Name,
                    UserProfilePictureUrl = comment.User.ProfilePictureUrl,
                    ParentCommentId = comment.ParentCommentId,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    UpdatedAt = comment.UpdatedAt,
                    IsEdited = comment.IsEdited,
                    IsOwnComment = currentUserId.HasValue && comment.UserId == currentUserId.Value,
                    Replies = comment.Replies.OrderBy(r => r.CreatedAt).Select(r => new CommentDto
                    {
                        Id = r.Id,
                        VideoId = r.VideoId,
                        UserId = r.UserId,
                        UserName = r.User.Name,
                        UserProfilePictureUrl = r.User.ProfilePictureUrl,
                        ParentCommentId = r.ParentCommentId,
                        Content = r.Content,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsEdited = r.IsEdited,
                        IsOwnComment = currentUserId.HasValue && r.UserId == currentUserId.Value
                    }).ToList()
                };

                return Ok(commentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comentario {CommentId} del video {VideoId}", commentId, videoId);
                return StatusCode(500, "Error interno al obtener comentario");
            }
        }

        /// <summary>
        /// Actualiza un comentario existente
        /// </summary>
        [Authorize]
        [HttpPut("{commentId}")]
        public async Task<IActionResult> UpdateComment(long videoId, long commentId, UpdateCommentDto commentDto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (commentId != commentDto.Id)
            {
                return BadRequest("El ID del comentario en la URL no coincide con el ID en los datos");
            }

            try
            {
                var comment = await _context.VideoComments.FindAsync(commentId);
                if (comment == null)
                {
                    return NotFound($"Comentario con ID {commentId} no encontrado");
                }

                // Verificar que el comentario pertenece al video especificado
                if (comment.VideoId != videoId)
                {
                    return BadRequest("El comentario no pertenece a este video");
                }

                // Verificar que el usuario es el autor del comentario
                if (comment.UserId != userId.Value)
                {
                    return Forbid("Solo el autor puede editar este comentario");
                }

                // Actualizar el contenido
                comment.Content = commentDto.Content;
                comment.UpdatedAt = DateTime.UtcNow;

                _context.Entry(comment).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar comentario {CommentId} del video {VideoId} por usuario {UserId}",
                    commentId, videoId, userId);
                return StatusCode(500, "Error interno al actualizar comentario");
            }
        }

        /// <summary>
        /// Elimina un comentario
        /// </summary>
        [Authorize]
        [HttpDelete("{commentId}")]
        public async Task<IActionResult> DeleteComment(long videoId, long commentId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var comment = await _context.VideoComments.FindAsync(commentId);
                if (comment == null)
                {
                    return NotFound($"Comentario con ID {commentId} no encontrado");
                }

                // Verificar que el comentario pertenece al video especificado
                if (comment.VideoId != videoId)
                {
                    return BadRequest("El comentario no pertenece a este video");
                }

                // Verificar que el usuario es el autor del comentario o el propietario del video
                var isAuthor = comment.UserId == userId.Value;
                var isVideoOwner = await _context.Videos
                    .AnyAsync(v => v.Id == videoId && v.UploadedByUserId == userId.Value);

                if (!isAuthor && !isVideoOwner)
                {
                    return Forbid("Solo el autor del comentario o el propietario del video pueden eliminar este comentario");
                }

                // Si es un comentario con respuestas, también eliminar las respuestas
                if (comment.ParentCommentId == null)
                {
                    var replies = await _context.VideoComments
                        .Where(c => c.ParentCommentId == commentId)
                        .ToListAsync();

                    _context.VideoComments.RemoveRange(replies);
                }

                _context.VideoComments.Remove(comment);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar comentario {CommentId} del video {VideoId} por usuario {UserId}",
                    commentId, videoId, userId);
                return StatusCode(500, "Error interno al eliminar comentario");
            }
        }

        /// <summary>
        /// Obtiene las respuestas a un comentario específico
        /// </summary>
        [HttpGet("{commentId}/replies")]
        public async Task<ActionResult<IEnumerable<CommentDto>>> GetCommentReplies(long videoId, long commentId)
        {
            try
            {
                // Verificar que el comentario existe y pertenece al video
                var parentComment = await _context.VideoComments
                    .FirstOrDefaultAsync(c => c.Id == commentId && c.VideoId == videoId);

                if (parentComment == null)
                {
                    return NotFound($"Comentario con ID {commentId} no encontrado en el video {videoId}");
                }

                // ID del usuario actual (si está autenticado)
                long? currentUserId = User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null;

                // Obtener las respuestas
                var replies = await _context.VideoComments
                    .Where(c => c.ParentCommentId == commentId)
                    .OrderBy(c => c.CreatedAt)
                    .Include(c => c.User)
                    .ToListAsync();

                // Convertir a DTOs
                var replyDtos = replies.Select(r => new CommentDto
                {
                    Id = r.Id,
                    VideoId = r.VideoId,
                    UserId = r.UserId,
                    UserName = r.User.Name,
                    UserProfilePictureUrl = r.User.ProfilePictureUrl,
                    ParentCommentId = r.ParentCommentId,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    IsEdited = r.IsEdited,
                    IsOwnComment = currentUserId.HasValue && r.UserId == currentUserId.Value,
                    Replies = new List<CommentDto>() // Las respuestas a respuestas no se permiten
                }).ToList();

                return Ok(replyDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener respuestas al comentario {CommentId} del video {VideoId}",
                    commentId, videoId);
                return StatusCode(500, "Error interno al obtener respuestas");
            }
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