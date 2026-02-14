namespace Weda.Core.Application.Security;

public interface IJwtTokenGenerator
{
    string GenerateToken(
        Guid id,
        string name,
        string email,
        List<string> permissions,
        List<string> roles);
}