using RetailRescueAI.Backend.Services.AI.Agents;

namespace RetailRescueAI.Backend.Services.BackgroundJobs;

public class ExpiryAiScheduledWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpiryAiScheduledWorker> _logger;

    public ExpiryAiScheduledWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExpiryAiScheduledWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=================================================");
        _logger.LogInformation("[ExpiryAiScheduledWorker] AI Background Scheduler Service initialized.");
        _logger.LogInformation("=================================================");

        // Wait a few seconds after application startup before initial run
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[ExpiryAiScheduledWorker] Triggering scheduled 3-hour AI inventory analysis...");

                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<OrchestratorAgent>();
                var recommendations = await orchestrator.RunFullPipelineAsync(stoppingToken);

                _logger.LogInformation("[ExpiryAiScheduledWorker] Scheduled analysis completed. Generated {Count} new recommendations.", recommendations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpiryAiScheduledWorker] Error during scheduled AI execution.");
            }

            // Read interval from config (default to 180 minutes = 3 hours, but allows 5 or 10 min for dev)
            int intervalMinutes = _configuration.GetValue<int>("Scheduler:IntervalMinutes", 180);
            _logger.LogInformation("[ExpiryAiScheduledWorker] Next execution in {Minutes} minutes.", intervalMinutes);

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
}

