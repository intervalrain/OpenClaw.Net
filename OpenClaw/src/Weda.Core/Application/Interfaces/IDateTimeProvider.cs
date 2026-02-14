namespace Weda.Core.Application.Interfaces;

public interface IDateTimeProvider
{
    public DateTime UtcNow { get; }
}
