using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Hubs;
using RetailRescueAI.Backend.Repositories.Implementations;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.AI;
using RetailRescueAI.Backend.Services.AI.Agents;
using RetailRescueAI.Backend.Services.AI.Plugins;
using RetailRescueAI.Backend.Services.AI.SemanticKernel;
using RetailRescueAI.Backend.Services.BackgroundJobs;
using RetailRescueAI.Backend.Services.Implementations;
using RetailRescueAI.Backend.Services.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=retailrescue.db";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (connectionString.Contains(".db") || (connectionString.Contains("Data Source=") && !connectionString.Contains("Initial Catalog") && !connectionString.Contains("database.windows.net")))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// 2. CORS Policy for Next.js Frontend & SignalR Real-time
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 2.1 SignalR for Real-time POS notifications
builder.Services.AddSignalR();

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

// 6. Repositories Layer (Data Access)
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IInventoryBatchRepository, InventoryBatchRepository>();
builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();
builder.Services.AddScoped<ISaleRepository, SaleRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAiRecommendationRepository, AiRecommendationRepository>();
builder.Services.AddScoped<IPromotionResultRepository, PromotionResultRepository>();

// 7. Services Layer (Business Logic)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPosService, PosService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IAiRecommendationService, AiRecommendationService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IResultService, ResultService>();

// 8. AI Multi-Agent Layer (Microsoft Semantic Kernel)
builder.Services.AddHttpClient<GeminiLLMService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ILLMService, GeminiLLMService>();

// Register Semantic Kernel IChatCompletionService bridged to ILLMService
builder.Services.AddScoped<IChatCompletionService, SemanticKernelChatCompletionService>();

// Register Semantic Kernel Native Plugins
builder.Services.AddScoped<InventoryDataPlugin>();
builder.Services.AddScoped<SalesVelocityPlugin>();
builder.Services.AddScoped<SafetyGuardrailPlugin>();
builder.Services.AddScoped<ComboStrategyPlugin>();

// Register Semantic Kernel with DI and Native Plugins
builder.Services.AddScoped<Kernel>(sp =>
{
    var kernel = new Kernel(sp);
    kernel.Plugins.AddFromObject(sp.GetRequiredService<InventoryDataPlugin>(), "InventoryDataPlugin");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<SalesVelocityPlugin>(), "SalesVelocityPlugin");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<SafetyGuardrailPlugin>(), "SafetyGuardrailPlugin");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<ComboStrategyPlugin>(), "ComboStrategyPlugin");
    return kernel;
});

// Register Specialized AI Agents
builder.Services.AddScoped<ExpiryAgent>();
builder.Services.AddScoped<SalesAgent>();
builder.Services.AddScoped<PromotionAgent>();
builder.Services.AddScoped<ReviserAgent>();
builder.Services.AddScoped<OrchestratorAgent>();

// 9. Background Worker (IHostedService every 3 hours)
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

// 9. HTTP Request Pipeline & Swagger UI
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "RetailRescueAI API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", time = RetailRescueAI.Backend.Common.AppClock.Now }));
app.MapControllers();

// 10. SignalR Hub Endpoints
app.MapHub<PromotionHub>("/hubs/promotions");

app.Run();
