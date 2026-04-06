using System.Net;
using Microsoft.Extensions.Logging;
using OpenClaw.Application.Agents.Middlewares;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;

namespace OpenClaw.Application.UnitTests.Agents;

public class RetryMiddlewareTests
{
    private readonly RetryMiddleware _middleware;
    private readonly AgentContext _context;

    public RetryMiddlewareTests()
    {
        var logger = Substitute.For<ILogger<RetryMiddleware>>();
        _middleware = new RetryMiddleware(logger, maxRetries: 2, baseDelaySeconds: 0.01);
        _context = new AgentContext
        {
            UserInput = "test",
            LlmProvider = Substitute.For<ILlmProvider>(),
            Skills = [],
            Options = new AgentPipelineOptions()
        };
    }

    [Fact]
    public async Task Invoke_Success_ShouldReturnImmediately()
    {
        AgentDelegate next = (_, _) => Task.FromResult("ok");

        var result = await _middleware.InvokeAsync(_context, next);

        result.ShouldBe("ok");
    }

    [Fact]
    public async Task Invoke_TransientThenSuccess_ShouldRetry()
    {
        var callCount = 0;
        AgentDelegate next = (_, _) =>
        {
            callCount++;
            if (callCount == 1)
                throw new HttpRequestException("overloaded", null, HttpStatusCode.ServiceUnavailable);
            return Task.FromResult("ok after retry");
        };

        var result = await _middleware.InvokeAsync(_context, next);

        result.ShouldBe("ok after retry");
        callCount.ShouldBe(2);
    }

    [Fact]
    public async Task Invoke_NonRetryable_ShouldThrowImmediately()
    {
        var callCount = 0;
        AgentDelegate next = (_, _) =>
        {
            callCount++;
            throw new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized);
        };

        await Should.ThrowAsync<HttpRequestException>(
            () => _middleware.InvokeAsync(_context, next));

        callCount.ShouldBe(1); // No retry
    }

    [Fact]
    public async Task Invoke_ExhaustedRetries_ShouldThrow()
    {
        var callCount = 0;
        AgentDelegate next = (_, _) =>
        {
            callCount++;
            throw new HttpRequestException("rate limit", null, HttpStatusCode.TooManyRequests);
        };

        await Should.ThrowAsync<HttpRequestException>(
            () => _middleware.InvokeAsync(_context, next));

        callCount.ShouldBe(3); // 1 initial + 2 retries
    }

    [Fact]
    public async Task Invoke_UserCancellation_ShouldNotRetry()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        AgentDelegate next = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("should not reach");
        };

        await Should.ThrowAsync<OperationCanceledException>(
            () => _middleware.InvokeAsync(_context, next, cts.Token));
    }
}
