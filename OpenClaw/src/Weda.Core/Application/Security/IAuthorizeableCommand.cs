using Weda.Core.Application.Interfaces;

namespace Weda.Core.Application.Security;

public interface IAuthorizeableCommand<T> : ICommand<T>
{
    Guid UserId { get; }
}
