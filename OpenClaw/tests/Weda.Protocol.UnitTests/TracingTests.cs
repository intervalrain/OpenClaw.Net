using Shouldly;
using NATS.Client.Core;
using Weda.Core.Infrastructure.Audit;

namespace Weda.Protocol.UnitTests;

public class TracingTests
{
    #region ShortId Tests

    [Fact]
    public void ShortId_Generate_ReturnsCorrectLength()
    {
        var id = ShortId.Generate();
        id.Length.ShouldBe(12);
    }

    [Fact]
    public void ShortId_Generate_WithCustomLength_ReturnsCorrectLength()
    {
        var id = ShortId.Generate(16);
        id.Length.ShouldBe(16);
    }

    [Fact]
    public void ShortId_Generate_ReturnsBase62Characters()
    {
        var id = ShortId.Generate();
        id.All(c => char.IsLetterOrDigit(c)).ShouldBeTrue();
    }

    [Fact]
    public void ShortId_Generate_ReturnsUniqueIds()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => ShortId.Generate()).ToList();
        ids.Count.ShouldBe(ids.Distinct().Count());
    }

    #endregion

    #region TraceContext Tests

    [Fact]
    public void TraceContext_Create_GeneratesNewIds()
    {
        var ctx = TraceContext.Create();

        ctx.TraceId.ShouldNotBeNullOrEmpty();
        ctx.RequestId.ShouldNotBeNullOrEmpty();
        ctx.TraceId.Length.ShouldBe(32);  // 128-bit = 16 bytes = 32 hex chars
        ctx.RequestId.Length.ShouldBe(16); // RequestIdGenerator default length
        ctx.Timestamp.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TraceContext_Create_PreservesExistingTraceId()
    {
        var existingTraceId = "existing123x";
        var ctx = TraceContext.Create(existingTraceId);

        ctx.TraceId.ShouldBe(existingTraceId);
        ctx.RequestId.ShouldNotBe(existingTraceId);
    }

    [Fact]
    public void TraceContext_Create_GeneratesValidTimestamp()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ctx = TraceContext.Create();
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ctx.Timestamp.ShouldBeInRange(before, after);
    }

    #endregion

    #region NatsHeadersExtensions Tests

    [Fact]
    public void NatsHeaders_GetTraceContext_FromNullHeaders_CreatesNewContext()
    {
        NatsHeaders? headers = null;
        var ctx = headers.GetTraceContext();

        ctx.TraceId.ShouldNotBeNullOrEmpty();
        ctx.RequestId.ShouldNotBeNullOrEmpty();
        ctx.Timestamp.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void NatsHeaders_GetTraceContext_FromEmptyHeaders_CreatesNewContext()
    {
        var headers = new NatsHeaders();
        var ctx = headers.GetTraceContext();

        ctx.TraceId.ShouldNotBeNullOrEmpty();
        ctx.RequestId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void NatsHeaders_WithTraceContext_SetsAllHeaders()
    {
        var ctx = TraceContext.Create();
        var headers = new NatsHeaders().WithTraceContext(ctx);

        headers[TraceConstants.TraceIdHeader].First().ShouldBe(ctx.TraceId);
        headers[TraceConstants.RequestIdHeader].First().ShouldBe(ctx.RequestId);
        headers[TraceConstants.TimestampHeader].First().ShouldBe(ctx.Timestamp.ToString());
    }

    [Fact]
    public void NatsHeaders_Roundtrip_PreservesContext()
    {
        var original = TraceContext.Create();
        var headers = new NatsHeaders().WithTraceContext(original);
        var restored = headers.GetTraceContext();

        restored.TraceId.ShouldBe(original.TraceId);
        restored.RequestId.ShouldBe(original.RequestId);
        restored.Timestamp.ShouldBe(original.Timestamp);
    }

    [Fact]
    public void CreateWithTraceContext_ReturnsHeadersWithContext()
    {
        var ctx = TraceContext.Create();
        var headers = NatsHeadersExtensions.CreateWithTraceContext(ctx);

        headers[TraceConstants.TraceIdHeader].First().ShouldBe(ctx.TraceId);
        headers[TraceConstants.RequestIdHeader].First().ShouldBe(ctx.RequestId);
        headers[TraceConstants.TimestampHeader].First().ShouldBe(ctx.Timestamp.ToString());
    }

    [Fact]
    public void NatsHeaders_GetTraceContext_ExtractsExistingValues()
    {
        var headers = new NatsHeaders
        {
            { TraceConstants.TraceIdHeader, "trace123" },
            { TraceConstants.RequestIdHeader, "request456" },
            { TraceConstants.TimestampHeader, "1234567890" }
        };

        var ctx = headers.GetTraceContext();

        ctx.TraceId.ShouldBe("trace123");
        ctx.RequestId.ShouldBe("request456");
        ctx.Timestamp.ShouldBe(1234567890);
    }

    [Fact]
    public void NatsHeaders_GetTraceContext_PartialHeaders_GeneratesMissingValues()
    {
        var headers = new NatsHeaders
        {
            { TraceConstants.TraceIdHeader, "trace123" }
        };

        var ctx = headers.GetTraceContext();

        ctx.TraceId.ShouldBe("trace123");
        ctx.RequestId.ShouldNotBeNullOrEmpty();
        ctx.RequestId.Length.ShouldBe(16);
    }

    #endregion
}
