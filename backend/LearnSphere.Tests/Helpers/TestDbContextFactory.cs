using LearnSphere.API.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.Tests.Helpers;

public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a fresh <see cref="AppDbContext"/> backed by an isolated in-memory database.
    /// Each call produces a unique database name, so tests never share state.
    /// </summary>
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
