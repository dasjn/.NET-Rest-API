using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IA.WebAPI.Models;
using IA.WebAPI.Services;
using Microsoft.Extensions.Logging;

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
        public async Task<ActionResult<IEnumerable<Video>>> GetVideos()
        {
            // Obtener los videos de la base de datos
            var videos = await _context.Videos.ToListAsync();

            // No modificamos la Uri aquí - mantenemos el formato original que espera el frontend
            return videos;
        }

        // GET: api/Videos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Video>> GetVideo(long id)
        {
            var video = await _context.Videos.FindAsync(id);

            if (video == null)
            {
                return NotFound();
            }

            return video;
        }

        // PUT: api/Videos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutVideo(long id, Video video)
        {
            if (id != video.Id)
            {
                return BadRequest();
            }

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
        [HttpPost]
        [RequestSizeLimit(5368709120)] // 5GB en bytes
        [RequestFormLimits(MultipartBodyLengthLimit = 5368709120)] // 5GB en bytes
        public async Task<ActionResult<Video>> PostVideo([FromForm] IFormFile videoFile, [FromForm] string name, [FromForm] string description)
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
                    Uri = originalStylePath // Usamos el formato que espera el frontend
                };

                // Guardar en la base de datos
                _context.Videos.Add(videoEntity);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetVideo), new { id = videoEntity.Id }, videoEntity);
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVideo(long id)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video == null)
            {
                return NotFound();
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

            // Eliminar el registro
            _context.Videos.Remove(video);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool VideoExists(long id)
        {
            return _context.Videos.Any(e => e.Id == id);
        }
    }
}