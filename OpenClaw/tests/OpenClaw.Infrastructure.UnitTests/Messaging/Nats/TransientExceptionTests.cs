using Shouldly;

using Weda.Core.Infrastructure.Messaging.Nats.Exceptions;

using Xunit;

namespace OpenClaw.Infrastructure.UnitTests.Messaging.Nats;

public class TransientExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        const string expectedMessage = "Transient error occurred";

        // Act
        var exception = new TransientException(expectedMessage);

        // Assert
        exception.Message.ShouldBe(expectedMessage);
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        const string expectedMessage = "Transient error occurred";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new TransientException(expectedMessage, innerException);

        // Assert
        exception.Message.ShouldBe(expectedMessage);
        exception.InnerException.ShouldBe(innerException);
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TransientException_ShouldInheritFromException()
    {
        // Arrange & Act
        var exception = new TransientException("Test");

        // Assert
        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void TransientException_CanBeCaughtAsException()
    {
        // Arrange
        const string message = "Transient failure";
        Exception? caughtException = null;

        // Act
        try
        {
            throw new TransientException(message);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.ShouldNotBeNull();
        caughtException.ShouldBeOfType<TransientException>();
        caughtException.Message.ShouldBe(message);
    }

    [Fact]
    public void TransientException_CanBeDistinguishedFromOtherExceptions()
    {
        // Arrange
        var transientException = new TransientException("Transient");
        var regularException = new InvalidOperationException("Regular");

        // Act & Assert
        transientException.ShouldBeOfType<TransientException>();
        regularException.ShouldNotBeOfType<TransientException>();
    }
}
