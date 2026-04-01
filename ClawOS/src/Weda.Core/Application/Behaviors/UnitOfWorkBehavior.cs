using Mediator;
using Weda.Core.Application.Interfaces;

namespace Weda.Core.Application.Behaviors;

/// <summary>
/// Pipeline behavior that automatically saves changes after command execution.
/// Only triggers SaveChanges for Command requests (not Queries).
/// </summary>
public class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);

        // Only save changes for Commands, not Queries
        if (IsCommand())
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return response;
    }

    private static bool IsCommand()
    {
        // Check if TRequest implements ICommand<TResponse>
        return typeof(TRequest).GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Interfaces.ICommand<>));
    }
}