using Microsoft.Extensions.Options;

using Shouldly;

using Weda.Core.Infrastructure.Messaging.Nats.Configuration;

using Xunit;

namespace OpenClaw.Infrastructure.UnitTests.Messaging.Nats;

public class JetStreamConsumerOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new JetStreamConsumerOptions();

        // Assert
        options.MaxRedeliveries.ShouldBe(5UL);
        options.NakDelay.ShouldBe(TimeSpan.FromSeconds(5));
        options.EnableDlq.ShouldBeTrue();
        options.DqlStreamSuffix.ShouldBe("-dlq");
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(5UL)]
    [InlineData(10UL)]
    [InlineData(100UL)]
    public void MaxRedeliveries_ShouldBeConfigurable(ulong maxRedeliveries)
    {
        // Arrange & Act
        var options = new JetStreamConsumerOptions { MaxRedeliveries = maxRedeliveries };

        // Assert
        options.MaxRedeliveries.ShouldBe(maxRedeliveries);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    public void NakDelay_ShouldBeConfigurable(int seconds)
    {
        // Arrange
        var delay = TimeSpan.FromSeconds(seconds);

        // Act
        var options = new JetStreamConsumerOptions { NakDelay = delay };

        // Assert
        options.NakDelay.ShouldBe(delay);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableDlq_ShouldBeConfigurable(bool enableDlq)
    {
        // Arrange & Act
        var options = new JetStreamConsumerOptions { EnableDlq = enableDlq };

        // Assert
        options.EnableDlq.ShouldBe(enableDlq);
    }

    [Theory]
    [InlineData("-dlq")]
    [InlineData("-dead")]
    [InlineData(".failures")]
    [InlineData("-errors")]
    public void DqlStreamSuffix_ShouldBeConfigurable(string suffix)
    {
        // Arrange & Act
        var options = new JetStreamConsumerOptions { DqlStreamSuffix = suffix };

        // Assert
        options.DqlStreamSuffix.ShouldBe(suffix);
    }

    [Fact]
    public void Options_ShouldBeIndependent()
    {
        // Arrange & Act
        var options1 = new JetStreamConsumerOptions { MaxRedeliveries = 10 };
        var options2 = new JetStreamConsumerOptions { MaxRedeliveries = 20 };

        // Assert
        options1.MaxRedeliveries.ShouldBe(10UL);
        options2.MaxRedeliveries.ShouldBe(20UL);
    }

    [Fact]
    public void Options_CanBeWrappedWithIOptions()
    {
        // Arrange
        var consumerOptions = new JetStreamConsumerOptions
        {
            MaxRedeliveries = 3,
            NakDelay = TimeSpan.FromSeconds(10),
            EnableDlq = false,
            DqlStreamSuffix = "-failed"
        };

        // Act
        var wrappedOptions = Options.Create(consumerOptions);

        // Assert
        wrappedOptions.Value.ShouldNotBeNull();
        wrappedOptions.Value.MaxRedeliveries.ShouldBe(3UL);
        wrappedOptions.Value.NakDelay.ShouldBe(TimeSpan.FromSeconds(10));
        wrappedOptions.Value.EnableDlq.ShouldBeFalse();
        wrappedOptions.Value.DqlStreamSuffix.ShouldBe("-failed");
    }

    [Fact]
    public void DlqStreamName_ShouldBeDerivedCorrectly()
    {
        // Arrange
        var options = new JetStreamConsumerOptions { DqlStreamSuffix = "-dlq" };
        var streamName = "orders";

        // Act
        var dlqStreamName = $"{streamName}{options.DqlStreamSuffix}";

        // Assert
        dlqStreamName.ShouldBe("orders-dlq");
    }

    [Theory]
    [InlineData("events", "-dlq", "events-dlq")]
    [InlineData("orders", "-dead", "orders-dead")]
    [InlineData("payments", ".errors", "payments.errors")]
    public void DlqStreamName_ShouldFollowPattern(string streamName, string suffix, string expected)
    {
        // Arrange
        var options = new JetStreamConsumerOptions { DqlStreamSuffix = suffix };

        // Act
        var dlqStreamName = $"{streamName}{options.DqlStreamSuffix}";

        // Assert
        dlqStreamName.ShouldBe(expected);
    }
}
