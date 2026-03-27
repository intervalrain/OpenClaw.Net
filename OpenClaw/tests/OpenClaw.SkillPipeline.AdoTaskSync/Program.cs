using Mediator;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenClaw.Application;
using OpenClaw.Application.Pipelines;
using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Skills;
using OpenClaw.Hosting;
using OpenClaw.Infrastructure;

using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// Setup DI
var services = new ServiceCollection();

// Logging
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});

// Core OpenClaw services
services.AddOpenClaw(configuration);

// Add Application layer (Mediator + Pipelines)
services.AddApplication();
services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(IApplicationMarker).Assembly];
});

// Infrastructure with PostgreSQL
services.AddInfrastructure(configuration);

// Build initial service provider to authenticate user via LoginCommand
var tempSp = services.BuildServiceProvider();
var tempLogger = tempSp.GetRequiredService<ILogger<Program>>();

// Authenticate user from config (email/password) using LoginCommand (same as production)
var email = configuration.GetValue<string>("TestUser:Email");
var password = configuration.GetValue<string>("TestUser:Password");

if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
{
    tempLogger.LogError("TestUser:Email and TestUser:Password must be configured in appsettings.json");
    return 1;
}

CurrentUser currentUser;
using (var authScope = tempSp.CreateScope())
{
    var mediator = authScope.ServiceProvider.GetRequiredService<ISender>();
    var loginResult = await mediator.Send(new LoginCommand(email, password));

    if (loginResult.IsError)
    {
        tempLogger.LogError("Login failed: {Errors}", string.Join(", ", loginResult.Errors.Select(e => e.Description)));
        return 1;
    }

    var authResponse = loginResult.Value;
    currentUser = new CurrentUser(
        authResponse.Id,
        authResponse.Name,
        authResponse.Email,
        authResponse.Permissions,
        authResponse.Roles);

    tempLogger.LogInformation("Authenticated as: {Name} ({Email})", authResponse.Name, authResponse.Email);
}

// Re-register ICurrentUserProvider with authenticated user
services.AddScoped<ICurrentUserProvider>(_ => new ConfigurableCurrentUserProvider(currentUser));

var sp = services.BuildServiceProvider();
var logger = sp.GetRequiredService<ILogger<Program>>();

logger.LogInformation("=== OpenClaw US-5 Playground ===");
logger.LogInformation("Running ADO Task Sync Pipeline\n");

// Get the pipeline
using var scope = sp.CreateScope();
var pipeline = scope.ServiceProvider.GetServices<IToolPipeline>()
    .OfType<AdoTaskSyncPipeline>()
    .FirstOrDefault();

if (pipeline == null)
{
    logger.LogError("AdoTaskSyncPipeline not found");
    return 1;
}

logger.LogInformation("Pipeline: {Name}", pipeline.Name);
logger.LogInformation("Description: {Description}\n", pipeline.Description);

// Check for auto-approve mode (useful for CI/testing)
var autoApprove = configuration.GetValue<bool>("AutoApprove");
if (autoApprove)
{
    logger.LogInformation("Auto-approve mode ENABLED (set AutoApprove=false to prompt for approval)");
}

// Run pipeline with user approval handler
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

try
{
    // Create pipeline context with test user
    var context = new PipelineExecutionContext(Guid.NewGuid(), null);

    var result = await pipeline.RunAsync(
        context,
        onApprovalRequired: async request =>
        {
            logger.LogInformation("\n=== Approval Required ===");
            logger.LogInformation("Step: {Step}", request.StepName);
            logger.LogInformation("Description: {Description}\n", request.Description);

            logger.LogInformation("Proposed changes:");
            foreach (var change in request.ProposedChanges)
            {
                logger.LogInformation("  - Work Item #{Id}: {Current} -> {Proposed}",
                    change.WorkItemId, change.CurrentState, change.ProposedState);
                logger.LogInformation("    Reason: {Reason}", change.Reason);
            }

            if (autoApprove)
            {
                logger.LogInformation("Auto-approving changes (AutoApprove=true)");
                return true;
            }

            // Check if running in interactive mode
            if (!Console.IsInputRedirected)
            {
                Console.Write("\nApprove these changes? (y/n): ");
                var input = Console.ReadLine()?.Trim().ToLower();
                var approved = input == "y" || input == "yes";

                logger.LogInformation("User response: {Response}", approved ? "APPROVED" : "REJECTED");
                return approved;
            }
            else
            {
                logger.LogWarning("Non-interactive mode detected. Set AutoApprove=true to auto-approve changes.");
                return false;
            }
        },
        ct: cts.Token);

    // Print results
    logger.LogInformation("\n=== Pipeline Result ===");
    logger.LogInformation("Success: {Success}", result.IsSuccess);
    logger.LogInformation("Summary: {Summary}\n", result.Summary);

    logger.LogInformation("Steps:");
    foreach (var step in result.Steps)
    {
        var status = step.IsSuccess ? "PASS" : "FAIL";
        logger.LogInformation("  [{Status}] {Step}", status, step.StepName);

        if (!string.IsNullOrEmpty(step.Output))
        {
            var truncated = step.Output.Length > 200
                ? step.Output[..200] + "..."
                : step.Output;
            logger.LogDebug("    Output: {Output}", truncated);
        }

        if (!string.IsNullOrEmpty(step.Error))
        {
            logger.LogWarning("    Error: {Error}", step.Error);
        }
    }

    return result.IsSuccess ? 0 : 1;
}
catch (Exception ex)
{
    logger.LogError(ex, "Pipeline failed");
    return 1;
}

public partial class Program { }

internal class ConfigurableCurrentUserProvider(CurrentUser user) : ICurrentUserProvider
{
    public CurrentUser GetCurrentUser() => user;
}
