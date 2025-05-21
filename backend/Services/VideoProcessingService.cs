using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace IA.WebAPI.Services
{
    public interface IVideoProcessingService
    {
        /// <summary>
        /// Extracts a thumbnail from a video at the specified position
        /// </summary>
        /// <param name="videoPath">Path to the video file</param>
        /// <param name="framePositionSeconds">Position in seconds to extract the frame from</param>
        /// <returns>Path to the extracted thumbnail, or null if extraction failed</returns>
        Task<string?> ExtractThumbnailAsync(string videoPath, int framePositionSeconds = 1);
    }

    public class VideoProcessingService : IVideoProcessingService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<VideoProcessingService> _logger;

        public VideoProcessingService(
            IWebHostEnvironment environment,
            ILogger<VideoProcessingService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string?> ExtractThumbnailAsync(string videoPath, int framePositionSeconds = 1)
        {
            try
            {
                // Get full paths
                var fullVideoPath = Path.Combine(_environment.ContentRootPath, videoPath);
                var fileName = $"thumb_{Path.GetFileNameWithoutExtension(videoPath)}_{Guid.NewGuid():N}.jpg";
                var thumbnailDir = Path.Combine(_environment.ContentRootPath, "Uploads", "Thumbnails");
                var thumbnailPath = Path.Combine(thumbnailDir, fileName);

                // Ensure directory exists
                if (!Directory.Exists(thumbnailDir))
                    Directory.CreateDirectory(thumbnailDir);

                // Create process to run FFmpeg
                using var process = new Process();
                process.StartInfo.FileName = "ffmpeg"; // Ensure FFmpeg is installed and in PATH
                process.StartInfo.Arguments = $"-i \"{fullVideoPath}\" -ss 00:00:{framePositionSeconds:D2} -vframes 1 -q:v 2 \"{thumbnailPath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                _logger.LogInformation("Executing FFmpeg command: {Command}", process.StartInfo.Arguments);

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    _logger.LogError("FFmpeg error: {Error}", error);
                    return null;
                }

                // Return relative path from web root
                return Path.Combine("Uploads", "Thumbnails", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting thumbnail from video: {VideoPath}", videoPath);
                return null;
            }
        }
    }
}