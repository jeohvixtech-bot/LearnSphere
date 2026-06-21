using System.Text;
using LearnSphere.API.Data;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();

// CORS — allow the AngularJS frontend on any localhost port
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.SetIsOriginAllowed(origin =>
        {
            var host = new Uri(origin).Host;
            return host == "localhost" || host == "127.0.0.1";
        })
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LearnSphere API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `TutorSubjects` ADD COLUMN `Price` DECIMAL(10,2) NULL;");
    } catch { }
    try {
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `TutorOfferings` (
                `Id` INT NOT NULL AUTO_INCREMENT,
                `TutorId` INT NOT NULL,
                `Subject` VARCHAR(200) NOT NULL DEFAULT '',
                `Level` VARCHAR(200) NOT NULL DEFAULT '',
                `Mode` VARCHAR(200) NOT NULL DEFAULT '',
                `Qualification` VARCHAR(200) NOT NULL DEFAULT '',
                `Price` DECIMAL(10,2) NOT NULL DEFAULT 0,
                PRIMARY KEY (`Id`),
                CONSTRAINT `FK_TutorOfferings_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors` (`Id`) ON DELETE CASCADE
            );");
    } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` ADD COLUMN `BookingNumber` VARCHAR(20) NOT NULL DEFAULT ''"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Invoices` ADD COLUMN `InvoiceNumber` VARCHAR(20) NOT NULL DEFAULT ''"); } catch { }
    await context.Database.ExecuteSqlRawAsync(
        "UPDATE `Bookings` SET `BookingNumber` = CONCAT('BOK', LPAD(`Id`, 5, '0')) WHERE `BookingNumber` = ''");
    await context.Database.ExecuteSqlRawAsync(
        "UPDATE `Invoices` SET `InvoiceNumber` = CONCAT('INV', LPAD(`Id`, 5, '0')) WHERE `InvoiceNumber` = ''");
    await DbSeeder.SeedAsync(context);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");

var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwrootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wwwrootPath),
    RequestPath = ""
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
