using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace IA.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var buildDate = new FileInfo(assembly.Location).LastWriteTime;

            return Ok(new
            {
                Version = "1.0.0",
                BuildDate = buildDate.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                ImageProxyFixed = "2025-06-27-CORS-Headers", // ← Cambia esto cada deploy
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            });
        }
    }
}
