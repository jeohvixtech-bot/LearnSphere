using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadController(IWebHostEnvironment env) => _env = env;

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { message = "Only JPG, PNG, WEBP are allowed." });

        var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "profiles");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"{Request.Scheme}://{Request.Host}/uploads/profiles/{fileName}";
        return Ok(new { url });
    }

    // Tutor verification documents — type distinguishes an "intro_video" upload
    // (allowed as MP4/MOV, 10-100MB) from every other document type (JPG/PNG/PDF,
    // 100KB-5MB). See TutorsController.SaveDocument, which records the resulting url.
    [HttpPost("document")]
    public async Task<IActionResult> UploadDocument(IFormFile file, [FromQuery] string type)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        bool isVideo = type == "intro_video";

        var allowedImage = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var allowedVideo = new[] { ".mp4", ".mov" };
        var allowed = isVideo ? allowedVideo : allowedImage;

        if (!allowed.Contains(ext))
            return BadRequest(new { message = $"Invalid file type. Allowed: {string.Join(", ", allowed)}" });

        long minBytes = isVideo ? 10L * 1024 * 1024 : 100 * 1024;
        long maxBytes = isVideo ? 100L * 1024 * 1024 : 5L * 1024 * 1024;

        if (file.Length < minBytes)
            return BadRequest(new { message = isVideo
                ? "Video must be at least 10 MB."
                : "File is too small. Minimum size is 100 KB." });

        if (file.Length > maxBytes)
            return BadRequest(new { message = isVideo
                ? "Video must be between 10 MB and 100 MB."
                : "File must be between 100 KB and 5 MB." });

        var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "documents");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"{Request.Scheme}://{Request.Host}/uploads/documents/{fileName}";
        return Ok(new { url, fileName = file.FileName, fileSizeBytes = file.Length });
    }
}
