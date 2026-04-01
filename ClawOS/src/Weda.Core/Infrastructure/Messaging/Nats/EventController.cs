using Mediator;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Weda.Core.Infrastructure.Messaging.Nats.Attributes;

namespace Weda.Core.Infrastructure.Messaging.Nats;

[Stream("[controller]_v{version:apiVersion}_[method]_stream")]
[Consumer("[controller]_v{version:apiVersion}_[method]_consumer")]
[Connection("bus")]
public abstract class EventController
{
    public IMediator Mediator { get; internal set; } = null!;
    public ILogger Logger { get; internal set; } = null!;
    public string Subject { get; internal set; } = string.Empty;
    public NatsHeaders? Headers { get; internal set; }

    /// <summary>
    /// Values extracted from subject placeholders (e.g., {id} from "employee.v1.123.get").
    /// </summary>
    public IReadOnlyDictionary<string, string> SubjectValues { get; internal set; } = new Dictionary<string, string>();
}