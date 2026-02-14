using Weda.Core.Application.Behaviors;
using Weda.Core.Application.Security;

using Mediator;

namespace OpenClaw.Application.UnitTests.Common.Behaviors;

public class AuthorizationBehaviorTests
{
    private readonly IAuthorizationService _mockAuthorizationService;

    public AuthorizationBehaviorTests()
    {
        _mockAuthorizationService = Substitute.For<IAuthorizationService>();
    }

    [Fact]
    public async Task InvokeAuthorizationBehavior_WhenNoAuthorizationAttribute_ShouldInvokeNextBehavior()
    {
        // Arrange
        var request = new RequestWithNoAuthorizationAttribute(Guid.NewGuid());

        var authorizationBehavior = new AuthorizationBehavior<RequestWithNoAuthorizationAttribute, ErrorOr<Response>>(_mockAuthorizationService);

        MessageHandlerDelegate<RequestWithNoAuthorizationAttribute, ErrorOr<Response>> next =
            (_, _) => new ValueTask<ErrorOr<Response>>(Response.Instance);

        // Act
        var result = await authorizationBehavior.Handle(request, next, default);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(Response.Instance);
    }

    [Fact]
    public async Task InvokeAuthorizationBehavior_WhenSingleAuthorizationAttributeAndUserIsAuthorized_ShouldInvokeNextBehavior()
    {
        // Arrange
        var request = new RequestWithSingleAuthorizationAttribute(Guid.NewGuid());

        _mockAuthorizationService
            .AuthorizeCurrentUser(
                request,
                Must.BeEmptyList<string>(),
                Must.BeListWith(["Permission"]),
                Must.BeEmptyList<string>())
            .Returns(Result.Success);

        var authorizationBehavior = new AuthorizationBehavior<RequestWithSingleAuthorizationAttribute, ErrorOr<Response>>(_mockAuthorizationService);

        MessageHandlerDelegate<RequestWithSingleAuthorizationAttribute, ErrorOr<Response>> next =
            (_, _) => new ValueTask<ErrorOr<Response>>(Response.Instance);

        // Act
        var result = await authorizationBehavior.Handle(request, next, default);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(Response.Instance);
    }

    [Fact]
    public async Task InvokeAuthorizationBehavior_WhenSingleAuthorizationAttributeAndUserIsNotAuthorized_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new RequestWithSingleAuthorizationAttribute(Guid.NewGuid());

        var error = Error.Unauthorized(code: "bad.user", description: "bad user");

        _mockAuthorizationService
            .AuthorizeCurrentUser(
                request,
                Must.BeEmptyList<string>(),
                Must.BeListWith(["Permission"]),
                Must.BeEmptyList<string>())
            .Returns(error);

        var authorizationBehavior = new AuthorizationBehavior<RequestWithSingleAuthorizationAttribute, ErrorOr<Response>>(_mockAuthorizationService);

        MessageHandlerDelegate<RequestWithSingleAuthorizationAttribute, ErrorOr<Response>> next =
            (_, _) => new ValueTask<ErrorOr<Response>>(Response.Instance);

        // Act
        var result = await authorizationBehavior.Handle(request, next, default);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(error);
    }

    [Fact]
    public async Task InvokeAuthorizationBehavior_WhenTonsOfAuthorizationAttributesAndUserIsAuthorized_ShouldInvokeNextBehavior()
    {
        // Arrange
        var request = new RequestWithTonsOfAuthorizationAttribute(Guid.NewGuid());

        _mockAuthorizationService
            .AuthorizeCurrentUser(
                request,
                Must.BeListWith(["Role1", "Role2", "Role3"]),
                Must.BeListWith(["Permission1", "Permission2", "Permission3"]),
                Must.BeListWith(["Policy1", "Policy2", "Policy3"]))
            .Returns(Result.Success);

        var authorizationBehavior = new AuthorizationBehavior<RequestWithTonsOfAuthorizationAttribute, ErrorOr<Response>>(_mockAuthorizationService);

        MessageHandlerDelegate<RequestWithTonsOfAuthorizationAttribute, ErrorOr<Response>> next =
            (_, _) => new ValueTask<ErrorOr<Response>>(Response.Instance);

        // Act
        var result = await authorizationBehavior.Handle(request, next, default);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(Response.Instance);
    }

    [Fact]
    public async Task InvokeAuthorizationBehavior_WhenTonsOfAuthorizationAttributesAndUserIsNotAuthorized_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new RequestWithTonsOfAuthorizationAttribute(Guid.NewGuid());

        var error = Error.Unauthorized(code: "bad.user", description: "bad user");

        _mockAuthorizationService
            .AuthorizeCurrentUser(
                request,
                Must.BeListWith(["Role1", "Role2", "Role3"]),
                Must.BeListWith(["Permission1", "Permission2", "Permission3"]),
                Must.BeListWith(["Policy1", "Policy2", "Policy3"]))
            .Returns(error);

        var authorizationBehavior = new AuthorizationBehavior<RequestWithTonsOfAuthorizationAttribute, ErrorOr<Response>>(_mockAuthorizationService);

        MessageHandlerDelegate<RequestWithTonsOfAuthorizationAttribute, ErrorOr<Response>> next =
            (_, _) => new ValueTask<ErrorOr<Response>>(Response.Instance);

        // Act
        var result = await authorizationBehavior.Handle(request, next, default);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(error);
    }
}

public record Response
{
    public static readonly Response Instance = new();
}

public record RequestWithNoAuthorizationAttribute(Guid UserId) : IAuthorizeableQuery<ErrorOr<Response>>;

[Authorize(Permissions = "Permission")]
public record RequestWithSingleAuthorizationAttribute(Guid UserId) : IAuthorizeableQuery<ErrorOr<Response>>;

[Authorize(Permissions = "Permission1,Permission2")]
[Authorize(Roles = "Role1,Role2")]
[Authorize(Policies = "Policy1,Policy2")]
[Authorize(Permissions = "Permission3", Roles = "Role3", Policies = "Policy3")]
public record RequestWithTonsOfAuthorizationAttribute(Guid UserId) : IAuthorizeableQuery<ErrorOr<Response>>;
