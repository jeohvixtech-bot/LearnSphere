using System.Text.Json;
using LearnSphere.API.Controllers;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LearnSphere.Tests.Controllers;

/// <summary>
/// Example-based unit tests for <see cref="AuthController"/>.
/// Tests drive the real <see cref="AuthService"/> against an isolated in-memory database,
/// so no external dependencies (MySQL, HTTP infrastructure) are required.
/// </summary>
public class AuthControllerTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    /// <summary>Returns an IConfiguration that satisfies AuthService's JWT needs.</summary>
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]      = "LearnSphere_SuperSecret_JWT_Key_2026_CHANGE_IN_PRODUCTION",
                ["Jwt:Issuer"]   = "LearnSphere.API",
                ["Jwt:Audience"] = "LearnSphere.Client"
            })
            .Build();

    /// <summary>
    /// Creates a controller wired to the real AuthService and an isolated in-memory DB.
    /// The <paramref name="db"/> out-parameter lets tests inspect DB state afterwards.
    /// </summary>
    private static (AuthController controller, AppDbContext db) BuildSut()
    {
        var db      = TestDbContextFactory.Create();
        var service = new AuthService(db, BuildConfig());
        var ctrl    = new AuthController(service);
        return (ctrl, db);
    }

    /// <summary>Hashes a plain-text password exactly as AuthService does.</summary>
    private static string Hash(string plain) => BCrypt.Net.BCrypt.HashPassword(plain);

    /// <summary>
    /// Round-trips <paramref name="value"/> through JSON (case-insensitive) so that
    /// anonymous objects returned by the controller are easily read as typed records.
    /// </summary>
    private static T Deserialise<T>(object? value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<T>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    // ── test cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_HappyPath_Returns200_WithTokenRoleNameUserId()
    {
        // Arrange — seed a user directly into the isolated DB
        var (ctrl, db) = BuildSut();
        var seeded = new User
        {
            Email        = "alice@example.com",
            PasswordHash = Hash("secret123"),
            Role         = "parent",
            Name         = "Alice",
            CreatedAt    = DateTime.UtcNow
        };
        db.Users.Add(seeded);
        await db.SaveChangesAsync();

        // Act
        var result = await ctrl.Login(new LoginDto("alice@example.com", "secret123"));

        // Assert — 200 with all required fields
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var response = Deserialise<AuthResponseDto>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token), "token must not be empty");
        Assert.Equal("parent", response.Role);
        Assert.Equal("Alice",  response.Name);
        Assert.True(response.UserId > 0,  "userId must be a positive integer");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var (ctrl, db) = BuildSut();
        db.Users.Add(new User
        {
            Email        = "bob@example.com",
            PasswordHash = Hash("correctPassword"),
            Role         = "parent",
            Name         = "Bob",
            CreatedAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await ctrl.Login(new LoginDto("bob@example.com", "wrongPassword"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        // Empty DB — no users exist
        var (ctrl, _) = BuildSut();

        var result = await ctrl.Login(new LoginDto("nobody@example.com", "any"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Register_HappyPath_Returns200_WithToken()
    {
        var (ctrl, _) = BuildSut();

        var result = await ctrl.Register(
            new RegisterDto("carol@example.com", "pass1234", "Carol", "parent"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var response = Deserialise<AuthResponseDto>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token), "token must not be empty");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var (ctrl, db) = BuildSut();

        // Pre-seed a user with the target email
        db.Users.Add(new User
        {
            Email        = "dave@example.com",
            PasswordHash = Hash("pass"),
            Role         = "parent",
            Name         = "Dave",
            CreatedAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Attempt to register with the same email
        var result = await ctrl.Register(
            new RegisterDto("dave@example.com", "newpass", "Dave 2", "parent"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_TutorRole_Returns200_AndCreatesTutorProfileRow()
    {
        var (ctrl, db) = BuildSut();

        var result = await ctrl.Register(
            new RegisterDto("eve@example.com", "pass1234", "Eve", "tutor"));

        // Response must be 200 with a token
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var response = Deserialise<AuthResponseDto>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token), "token must not be empty");

        // A Tutor profile row must exist whose UserId matches the returned userId
        var tutorRow = db.Tutors.SingleOrDefault(t => t.UserId == response.UserId);
        Assert.NotNull(tutorRow);
        Assert.Equal(response.UserId, tutorRow!.UserId);
    }
}
