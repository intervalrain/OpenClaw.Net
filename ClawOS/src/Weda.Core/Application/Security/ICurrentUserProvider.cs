using Weda.Core.Application.Security.Models;

namespace Weda.Core.Application.Security;

public interface ICurrentUserProvider
{
    CurrentUser GetCurrentUser();
}
