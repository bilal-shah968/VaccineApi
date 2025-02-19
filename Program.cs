using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using VaccineAPI.Models;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Starting Vaccine API...");

// Add logging for debugging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add controllers with Newtonsoft JSON settings
builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddNewtonsoftJson(options => { options.UseMemberCasing(); });

// Add CORS policy
builder.Services.AddCors(p => p.AddPolicy("corsapp", policy =>
{
    policy.WithOrigins("*").AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("Content-Disposition");
}));

// Add Swagger/OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Vaccine API", Version = "v1" });
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First()); // Fix duplicate endpoint issue
});

// Register AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Register PdfService for dependency injection
builder.Services.AddScoped<PdfService>();

// Configure MySQL connection for EF Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       Environment.GetEnvironmentVariable("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is not configured.");
}

// Log database connection (excluding sensitive info)
Console.WriteLine($"Using Database: {connectionString.Split(';')[0]}");

var serverVersion = new MySqlServerVersion(new Version(8, 0, 31));

try
{
    // Add DbContext to services
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, serverVersion)
               .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
               .EnableSensitiveDataLogging()
               .EnableDetailedErrors()
    );
}
catch (Exception ex)
{
    Console.WriteLine($"Error configuring DbContext: {ex.Message}");
}

var app = builder.Build();

// Enable middleware in the correct order
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Show detailed error pages in development
}

// Enable Swagger and Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vaccine API v1");
    c.RoutePrefix = ""; // Makes Swagger UI available at root URL
});

app.UseHttpsRedirection(); // Redirect HTTP to HTTPS

// Serve static files from the "Resources" folder
var resourcesPath = Path.Combine(builder.Environment.ContentRootPath, "Resources");
if (!Directory.Exists(resourcesPath))
{
    Directory.CreateDirectory(resourcesPath); // Ensure Resources folder exists
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(resourcesPath),
    RequestPath = "/Resources"
});

// Enable CORS before authentication and authorization middleware
app.UseCors("corsapp");

app.UseAuthorization(); // Ensure it is after CORS and static files

app.MapControllers(); // Map API controllers

Console.WriteLine("Vaccine API is running...");

app.Run();
