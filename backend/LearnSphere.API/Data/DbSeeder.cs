using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Users.AnyAsync()) return;

        // Seed admin user
        var adminUser = new User
        {
            Email = "admin@learnsphere.sg",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = "admin",
            Name = "Admin",
            CreatedAt = DateTime.UtcNow
        };

        // Seed parent user
        var parentUser = new User
        {
            Email = "sarah.tan@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Parent@123"),
            Role = "parent",
            Name = "Sarah Tan",
            CreatedAt = DateTime.UtcNow
        };

        // Seed tutor user
        var tutorUser1 = new User
        {
            Email = "lim.ws@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Tutor@123"),
            Role = "tutor",
            Name = "Mr. Lim Wei Sheng",
            CreatedAt = DateTime.UtcNow
        };

        var tutorUser2 = new User
        {
            Email = "rachel.wong@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Tutor@123"),
            Role = "tutor",
            Name = "Ms. Rachel Wong",
            CreatedAt = DateTime.UtcNow
        };

        var tutorUser3 = new User
        {
            Email = "daniel.tan@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Tutor@123"),
            Role = "tutor",
            Name = "Mr. Daniel Tan",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(adminUser, parentUser, tutorUser1, tutorUser2, tutorUser3);
        await context.SaveChangesAsync();

        // Seed tutor profiles (empty — tutors complete their profiles after login)
        var tutor1 = new Tutor { UserId = tutorUser1.Id };
        var tutor2 = new Tutor { UserId = tutorUser2.Id };
        var tutor3 = new Tutor { UserId = tutorUser3.Id };

        context.Tutors.AddRange(tutor1, tutor2, tutor3);
        await context.SaveChangesAsync();

        // Seed institutions
        context.Institutions.AddRange(
            new Institution { Name = "Raffles Institution", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Hwa Chong Institution", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Nanyang Girls' High School", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Anglo-Chinese School (Independent)", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Dunman High School", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "CHIJ St. Nicholas Girls' School (Secondary)", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Victoria School", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Catholic High School (Secondary)", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Raffles Girls' School (Secondary)", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "River Valley High School", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Methodist Girls' School (Secondary)", Country = "Singapore", Type = "Secondary" },
            new Institution { Name = "Nanyang Primary School", Country = "Singapore", Type = "Primary" },
            new Institution { Name = "Rosyth School", Country = "Singapore", Type = "Primary" },
            new Institution { Name = "Anglo-Chinese School (Primary)", Country = "Singapore", Type = "Primary" },
            new Institution { Name = "Raffles Girls' Primary School", Country = "Singapore", Type = "Primary" },
            new Institution { Name = "Hwa Chong International School", Country = "Singapore", Type = "Junior College" },
            new Institution { Name = "Raffles Junior College", Country = "Singapore", Type = "Junior College" },
            new Institution { Name = "Victoria Junior College", Country = "Singapore", Type = "Junior College" },
            new Institution { Name = "Nanyang Technological University", Country = "Singapore", Type = "University/Tertiary" },
            new Institution { Name = "National University of Singapore", Country = "Singapore", Type = "University/Tertiary" },
            new Institution { Name = "Singapore Polytechnic", Country = "Singapore", Type = "Polytechnic/Vocational" },
            new Institution { Name = "Ngee Ann Polytechnic", Country = "Singapore", Type = "Polytechnic/Vocational" },
            new Institution { Name = "SMK Taman Tun Dr Ismail", Country = "Malaysia", Type = "Secondary" },
            new Institution { Name = "SMK Damansara Utama", Country = "Malaysia", Type = "Secondary" },
            new Institution { Name = "SRJK(C) Pudu", Country = "Malaysia", Type = "Primary" },
            new Institution { Name = "Universiti Malaya", Country = "Malaysia", Type = "University/Tertiary" }
        );

        await context.SaveChangesAsync();
    }
}
