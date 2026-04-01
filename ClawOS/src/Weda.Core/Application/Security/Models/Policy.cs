namespace Weda.Core.Application.Security.Models;

public static class Policy
{
    public const string SelfOrAdmin = "SelfOrAdminPolicy";
    public const string SuperAdminOnly = "SuperAdminOnlyPolicy";
    public const string AdminOrAbove = "AdminOrAbovePolicy";
}