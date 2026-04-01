using Weda.Core.Application.Interfaces;

namespace Weda.Core.Application.Security;

public interface IAuthorizeableQuery<T> : IQuery<T>
{
    Guid UserId { get; }
}
