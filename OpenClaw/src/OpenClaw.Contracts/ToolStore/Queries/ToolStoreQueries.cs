using ErrorOr;
using Mediator;
using OpenClaw.Contracts.ToolStore.Responses;

namespace OpenClaw.Contracts.ToolStore.Queries;

public record GetToolPackagesQuery(bool? InstalledOnly = null) : IRequest<ErrorOr<IReadOnlyList<ToolPackageResponse>>>;

public record GetToolPackageQuery(Guid Id) : IRequest<ErrorOr<ToolPackageResponse>>;
