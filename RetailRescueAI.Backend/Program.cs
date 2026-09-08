using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Services;
using RetailRescueAI.Backend.Services.AI;
using RetailRescueAI.Backend.Services.AI.Agents;
using RetailRescueAI.Backend.Services.BackgroundJobs;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. CORS Policy for Next.js Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 3. Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "RetailRescueAI_SuperSecretKey_2026_Enterprise_Security_JwtToken!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

// 4. Controllers & JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 5. OpenAPI Configuration (.NET 9 Native)
builder.Services.AddOpenApi();

// 6. AI Multi-Agent & Domain Services
builder.Services.AddHttpClient<GeminiLLMService>();
builder.Services.AddSingleton<MockLLMService>();
builder.Services.AddScoped<ILLMService, GeminiLLMService>();

builder.Services.AddScoped<ExpiryAgent>();
builder.Services.AddScoped<SalesAgent>();
builder.Services.AddScoped<PromotionAgent>();
builder.Services.AddScoped<ReviserAgent>();
builder.Services.AddScoped<OrchestratorAgent>();
builder.Services.AddScoped<ManagerChatbotService>();

builder.Services.AddScoped<PosPromotionEngine>();
builder.Services.AddScoped<InventoryService>();

// 7. Background Worker (IHostedService every 3 hours)
builder.Services.AddHostedService<ExpiryAiScheduledWorker>();

var app = builder.Build();

// 8. Auto-migrate and Seed Database on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Initializing database and seeding Japanese retail data...");
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbInitializer.InitializeAsync(context);
        logger.LogInformation("Database initialization and seeding completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

// 9. HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
