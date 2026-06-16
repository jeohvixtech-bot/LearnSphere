using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context) => _context = context;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalParents = await _context.Users.CountAsync(u => u.Role == "parent");
        var verifiedTutors = await _context.Tutors.CountAsync(t => t.IsVerified);
        var totalSessions = await _context.Bookings.CountAsync(b => b.Status == "completed");
        var grossRevenue = await _context.Invoices.Where(i => i.Status == "Paid").SumAsync(i => i.Amount);

        return Ok(new AdminStatsDto
        {
            TotalParents = totalParents,
            TotalVerifiedTutors = verifiedTutors,
            TotalSessions = totalSessions,
            GrossRevenue = grossRevenue
        });
    }

    [HttpGet("tutors/unverified")]
    public async Task<IActionResult> GetUnverifiedTutors()
    {
        var tutors = await _context.Tutors
            .Where(t => !t.IsVerified)
            .Include(t => t.User)
            .Include(t => t.Subjects)
            .Include(t => t.Qualifications)
            .ToListAsync();

        return Ok(tutors.Select(t => new
        {
            t.Id,
            t.UserId,
            Name = t.User.Name,
            t.ImageUrl,
            t.ExperienceYears,
            t.IsVerified,
            Subjects = t.Subjects.Select(s => s.Subject),
            Qualifications = t.Qualifications.Select(q => q.Qualification)
        }));
    }

    [HttpPatch("tutors/{id}/verify")]
    public async Task<IActionResult> VerifyTutor(int id)
    {
        var tutor = await _context.Tutors
            .Include(t => t.Qualifications)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null) return NotFound();

        tutor.IsVerified = true;
        tutor.Qualifications.Insert(0, new TutorQualification
        {
            TutorId = tutor.Id,
            Qualification = "Verified by operations team"
        });

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes()
    {
        var disputes = await _context.Bookings
            .Where(b => b.IssueReport != null)
            .Include(b => b.IssueReport)
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .ToListAsync();

        return Ok(disputes.Select(b => new
        {
            b.Id,
            b.Subject,
            b.Status,
            TutorName = b.Tutor?.User?.Name,
            StudentName = b.Student?.Name,
            b.IssueReport
        }));
    }

    [HttpPatch("disputes/{bookingId}/resolve")]
    public async Task<IActionResult> ResolveDispute(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.IssueReport)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return NotFound();

        if (booking.IssueReport != null)
        {
            _context.IssueReports.Remove(booking.IssueReport);
        }
        booking.Status = "completed";

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("institutions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInstitutions([FromQuery] string? country, [FromQuery] string? type, [FromQuery] string? search)
    {
        var query = _context.Institutions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(country) && country != "All")
            query = query.Where(i => i.Country == country);

        if (!string.IsNullOrWhiteSpace(type) && type != "All")
            query = query.Where(i => i.Type == type);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s) || i.Type.ToLower().Contains(s));
        }

        var institutions = await query.Take(20).ToListAsync();
        return Ok(institutions.Select(i => new InstitutionDto { Id = i.Id, Name = i.Name, Country = i.Country, Type = i.Type }));
    }
}
