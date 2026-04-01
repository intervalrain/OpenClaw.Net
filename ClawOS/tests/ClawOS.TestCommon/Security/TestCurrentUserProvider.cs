using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;

namespace ClawOS.TestCommon.Security;

public class TestCurrentUserProvider : ICurrentUserProvider
{
    private CurrentUser? _currentUser;

    public void Returns(CurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public CurrentUser GetCurrentUser() => _currentUser ?? CurrentUserFactory.CreateCurrentUser();
}
