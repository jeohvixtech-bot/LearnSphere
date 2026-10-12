using System.Security.Claims;
using LearnSphere.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

// Serves tutor verification documents.
//
// These used to be handed out by UseStaticFiles along with everything else under wwwroot,
// which runs BEFORE authentication — so an NRIC or passport scan was readable by anyone
// holding the URL, signed in or not. The filename is a GUID, but the URL is not a secret:
// it is returned by the API, stored in the database, and sits in an admin's browser
// history.
//
// The route deliberately matches the old static path, so URLs already recorded against
// TutorDocuments keep resolving — they now just pass through an authorisation check first.
[ApiController]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public DocumentsController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet("/uploads/documents/{fileName}")]
    public async Task<IActionResult> GetDocument(string fileName)
    {
        // The route value can only ever be one path segment, but a traversal attempt is
        // rejected outright rather than relying on that — this reads from disk.
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..")
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || Path.IsPathRooted(fileName))
        {
            return BadRequest(new { message = "Invalid file name." });
        }

        // Match on the stored URL's tail rather than reconstructing the whole thing: the
        // scheme and host recorded at upload time may differ from the current request's
        // (a rename, or a move behind a proxy) and that must not lock an admin out.
        var doc = await _context.TutorDocuments
            .Include(d => d.Tutor)
            .FirstOrDefaultAsync(d => d.FileUrl != null && d.FileUrl.EndsWith(fileName));

        if (doc == null) return NotFound();

        var isAdmin = User.IsInRole("admin");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // The owning tutor, or an admin reviewing them. Nobody else — a parent browsing
        // the catalogue has no business reading a tutor's identity papers.
        if (!isAdmin && doc.Tutor?.UserId != userId) return Forbid();

        var path = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "documents", fileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };

        return PhysicalFile(path, contentType);
    }
}
