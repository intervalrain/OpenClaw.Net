namespace Weda.Core.Infrastructure.Messaging.Nats.Exceptions;

/// <summary>
/// Exception that indicates a transient error that should trigger a NAK (retry).
/// Non-transient exceptions will be ACKed and sent to DLQ.
/// </summary>
public class TransientException : Exception
{
    public TransientException(string message) : base(message) { }
    public TransientException(string message, Exception innerException)
        : base(message, innerException) { }
}