using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IA.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ImageProxyController> _logger;

        public ImageProxyController(
            IHttpClientFactory httpClientFactory,
            ILogger<ImageProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Proxy para imágenes externas para evitar problemas de CORS
        /// </summary>
        /// <param name="url">URL de la imagen a obtener</param>
        /// <returns>La imagen obtenida desde la URL</returns>
        [HttpGet("get")]
        [AllowAnonymous]
        [ResponseCache(Duration = 86400)] // Cache por 24 horas
        public async Task<IActionResult> GetImage([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return BadRequest("URL parameter is required");
            }

            try
            {
                // Validar que la URL es segura
                if (!IsUrlSafe(url))
                {
                    _logger.LogWarning("Attempted to proxy potentially unsafe URL: {Url}", url);
                    return BadRequest("URL not allowed");
                }

                var httpClient = _httpClientFactory.CreateClient("ImageProxy");
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch image from {Url}: {StatusCode}", url, response.StatusCode);
                    return StatusCode((int)response.StatusCode);
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                if (!contentType.StartsWith("image/"))
                {
                    _logger.LogWarning("URL did not return an image: {ContentType}", contentType);
                    return BadRequest("URL did not return an image");
                }

                var imageBytes = await response.Content.ReadAsByteArrayAsync();

                // ✅ AGREGAR ESTOS HEADERS CORS ESPECÍFICOS
                Response.Headers.Append("Cache-Control", "public,max-age=86400");
                Response.Headers.Append("Cross-Origin-Resource-Policy", "cross-origin");
                Response.Headers.Append("Access-Control-Allow-Origin", "*");
                Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
                Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

                return File(imageBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying image from {Url}", url);
                return StatusCode(500, "Error fetching the image");
            }
        }

        /// <summary>
        /// Verifica que la URL es segura para el proxy
        /// </summary>
        private bool IsUrlSafe(string url)
        {
            try
            {
                // Crear un objeto Uri para facilitar la validación
                var uri = new Uri(url);

                // Lista de dominios permitidos
                var allowedDomains = new[]
                {
                    "googleusercontent.com",
                    "lh3.googleusercontent.com",
                    "lh4.googleusercontent.com",
                    "lh5.googleusercontent.com",
                    "lh6.googleusercontent.com",
                    "storage.googleapis.com",
                };

                // Verificar si el dominio está en la lista de permitidos
                foreach (var domain in allowedDomains)
                {
                    if (uri.Host.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                // No está en la lista de dominios permitidos
                return false;
            }
            catch
            {
                // URL malformada
                return false;
            }
        }
    }
}