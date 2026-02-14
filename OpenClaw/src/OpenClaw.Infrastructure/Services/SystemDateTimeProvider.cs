using Weda.Core.Application.Interfaces;

namespace OpenClaw.Infrastructure.Services;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
