using ErrorOr;
using Mediator;
using OpenClaw.Contracts.ToolStore.Queries;
using OpenClaw.Contracts.ToolStore.Responses;
using OpenClaw.Domain.ToolStore.Entities;
using OpenClaw.Domain.ToolStore.Repositories;

namespace OpenClaw.Application.ToolStore.Queries;

public class GetToolPackagesQueryHandler(IToolPackageRepository repository)
    : IRequestHandler<GetToolPackagesQuery, ErrorOr<IReadOnlyList<ToolPackageResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<ToolPackageResponse>>> Handle(
        GetToolPackagesQuery request, CancellationToken ct)
    {
        var packages = request.InstalledOnly == true
            ? await repository.GetInstalledAsync(ct)
            : await repository.GetAllAsync(ct);

        return packages.Select(ToResponse).ToList();
    }

    internal static ToolPackageResponse ToResponse(ToolPackage p) => new()
    {
        Id = p.Id,
        PackageId = p.PackageId,
        Name = p.Name,
        Description = p.Description,
        Author = p.Author,
        CurrentVersion = p.CurrentVersion,
        InstalledVersion = p.InstalledVersion,
        Status = p.Status.ToString(),
        IconUrl = p.IconUrl,
        RepositoryUrl = p.RepositoryUrl,
        Category = p.Category,
        InstalledAt = p.InstalledAt,
        CreatedAt = p.CreatedAt,
    };
}

public class GetToolPackageQueryHandler(IToolPackageRepository repository)
    : IRequestHandler<GetToolPackageQuery, ErrorOr<ToolPackageResponse>>
{
    public async ValueTask<ErrorOr<ToolPackageResponse>> Handle(
        GetToolPackageQuery request, CancellationToken ct)
    {
        var package = await repository.GetByIdAsync(request.Id, ct);
        if (package is null)
            return Error.NotFound("Tool package not found.");

        return GetToolPackagesQueryHandler.ToResponse(package);
    }
}
