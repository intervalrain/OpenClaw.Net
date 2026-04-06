using System.Net;
using OpenClaw.Contracts.Llm;

namespace OpenClaw.Application.UnitTests.Agents;

public class LlmErrorClassifierTests
{
    [Fact]
    public void Classify_HttpTooManyRequests_ShouldBeRateLimited()
    {
        var ex = new HttpRequestException("Rate limit", null, HttpStatusCode.TooManyRequests);
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.RateLimited);
    }

    [Fact]
    public void Classify_Http503_ShouldBeOverloaded()
    {
        var ex = new HttpRequestException("Overloaded", null, HttpStatusCode.ServiceUnavailable);
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.Overloaded);
    }

    [Fact]
    public void Classify_Http500_ShouldBeServerError()
    {
        var ex = new HttpRequestException("Internal error", null, HttpStatusCode.InternalServerError);
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.ServerError);
    }

    [Fact]
    public void Classify_Http401_ShouldBeAuthError()
    {
        var ex = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.AuthError);
    }

    [Fact]
    public void Classify_Http400WithContextLength_ShouldBeContextOverflow()
    {
        var ex = new HttpRequestException("maximum context length exceeded", null, HttpStatusCode.BadRequest);
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.ContextOverflow);
    }

    [Fact]
    public void Classify_Http400Generic_ShouldBeInvalidRequest()
    {
        var ex = new HttpRequestException("bad request", null, HttpStatusCode.BadRequest);
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.InvalidRequest);
    }

    [Fact]
    public void Classify_TaskCanceled_ShouldBeConnectionError()
    {
        var ex = new TaskCanceledException("Timeout");
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.ConnectionError);
    }

    [Fact]
    public void Classify_MessageContainsRate_ShouldBeRateLimited()
    {
        var ex = new Exception("rate limit exceeded");
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.RateLimited);
    }

    [Fact]
    public void Classify_Unknown_ShouldBeUnknown()
    {
        var ex = new Exception("something weird happened");
        LlmErrorClassifier.Classify(ex).ShouldBe(LlmErrorCategory.Unknown);
    }

    [Theory]
    [InlineData(LlmErrorCategory.RateLimited, true)]
    [InlineData(LlmErrorCategory.Overloaded, true)]
    [InlineData(LlmErrorCategory.ServerError, true)]
    [InlineData(LlmErrorCategory.ConnectionError, true)]
    [InlineData(LlmErrorCategory.AuthError, false)]
    [InlineData(LlmErrorCategory.InvalidRequest, false)]
    [InlineData(LlmErrorCategory.ContextOverflow, false)]
    [InlineData(LlmErrorCategory.Unknown, false)]
    public void IsRetryable_ShouldClassifyCorrectly(LlmErrorCategory category, bool expected)
    {
        LlmErrorClassifier.IsRetryable(category).ShouldBe(expected);
    }
}
