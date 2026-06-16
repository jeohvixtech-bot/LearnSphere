using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StudentsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetMyStudents()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var students = await _context.Students
            .Where(s => s.ParentUserId == userId)
            .ToListAsync();
        return Ok(students.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = new Student
        {
            ParentUserId = userId,
            Name = dto.Name,
            BirthDate = dto.BirthDate,
            School = dto.School,
            EducationLevel = dto.EducationLevel,
            SubjectSelect = dto.SubjectSelect,
            LearningGoal = dto.LearningGoal,
            PhotoUrl = dto.PhotoUrl
        };
        _context.Students.Add(student);
        await _context.SaveChangesAsync();
        return Ok(MapToDto(student));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStudentDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.ParentUserId == userId);
        if (student == null) return NotFound();

        if (dto.Name != null) student.Name = dto.Name;
        if (dto.BirthDate != null) student.BirthDate = dto.BirthDate;
        if (dto.School != null) student.School = dto.School;
        if (dto.EducationLevel != null) student.EducationLevel = dto.EducationLevel;
        if (dto.SubjectSelect != null) student.SubjectSelect = dto.SubjectSelect;
        if (dto.LearningGoal != null) student.LearningGoal = dto.LearningGoal;
        if (dto.PhotoUrl != null) student.PhotoUrl = dto.PhotoUrl;

        await _context.SaveChangesAsync();
        return Ok(MapToDto(student));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.ParentUserId == userId);
        if (student == null) return NotFound();

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
        return Ok();
    }

    private static StudentDto MapToDto(Student s) => new()
    {
        Id = s.Id,
        ParentUserId = s.ParentUserId,
        Name = s.Name,
        BirthDate = s.BirthDate,
        School = s.School,
        EducationLevel = s.EducationLevel,
        SubjectSelect = s.SubjectSelect,
        LearningGoal = s.LearningGoal,
        PhotoUrl = s.PhotoUrl
    };
}
