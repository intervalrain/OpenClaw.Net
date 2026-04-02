using ErrorOr;
using Mediator;
using OpenClaw.Contracts.ToolStore.Responses;

namespace OpenClaw.Contracts.ToolStore.Commands;

public record InstallToolPackageCommand(Guid PackageId, Guid UserId) : IRequest<ErrorOr<ToolPackageResponse>>;

public record UninstallToolPackageCommand(Guid PackageId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record UpgradeToolPackageCommand(Guid PackageId, Guid UserId) : IRequest<ErrorOr<ToolPackageResponse>>;
