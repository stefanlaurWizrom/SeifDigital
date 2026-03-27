using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace SeifDigital.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public DiagnosticController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet("paths")]
        public IActionResult GetPaths()
        {
            var webRootPath = _environment.WebRootPath;
            var contentRootPath = _environment.ContentRootPath;
            var uploadsPath = Path.Combine(webRootPath, "uploads");

            return Ok(new
            {
                webRootPath,
                contentRootPath,
                uploadsPath,
                webRootPathExists = Directory.Exists(webRootPath),
                uploadsPathExists = Directory.Exists(uploadsPath),
                canWriteToUploads = CanWriteToDirectory(uploadsPath),
                environment = _environment.EnvironmentName
            });
        }

        private bool CanWriteToDirectory(string path)
        {
            try
            {
                var testFile = Path.Combine(path, $"test_{Guid.NewGuid()}.txt");
                System.IO.File.WriteAllText(testFile, "test");
                System.IO.File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
