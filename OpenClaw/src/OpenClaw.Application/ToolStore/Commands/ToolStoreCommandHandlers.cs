using ErrorOr;
using Mediator;
using OpenClaw.Application.ToolStore.Queries;
using OpenClaw.Contracts.ToolStore.Commands;
using OpenClaw.Contracts.ToolStore.Responses;
using OpenClaw.Domain.ToolStore.Enums;
using OpenClaw.Domain.ToolStore.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.ToolStore.Commands;

public class InstallToolPackageCommandHandler(IToolPackageRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<InstallToolPackageCommand, ErrorOr<ToolPackageResponse>>
{
    public async ValueTask<ErrorOr<ToolPackageResponse>> Handle(
        InstallToolPackageCommand request, CancellationToken ct)
    {
        var package = await repository.GetByIdAsync(request.PackageId, ct);
        if (package is null)
            return Error.NotFound("Tool package not found.");

        if (package.Status == ToolPackageStatus.Installed)
            return Error.Conflict("Tool package is already installed.");

        package.Install(request.UserId);
        await repository.UpdateAsync(package, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return GetToolPackagesQueryHandler.ToResponse(package);
    }
}

public class UninstallToolPackageCommandHandler(IToolPackageRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UninstallToolPackageCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        UninstallToolPackageCommand request, CancellationToken ct)
    {
        var package = await repository.GetByIdAsync(request.PackageId, ct);
        if (package is null)
            return Error.NotFound("Tool package not found.");

        if (package.Status == ToolPackageStatus.Available)
            return Error.Validation("Tool package is not installed.");

        package.Uninstall();
        await repository.UpdateAsync(package, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class UpgradeToolPackageCommandHandler(IToolPackageRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpgradeToolPackageCommand, ErrorOr<ToolPackageResponse>>
{
    public async ValueTask<ErrorOr<ToolPackageResponse>> Handle(
        UpgradeToolPackageCommand request, CancellationToken ct)
    {
        var package = await repository.GetByIdAsync(request.PackageId, ct);
        if (package is null)
            return Error.NotFound("Tool package not found.");

        if (package.Status != ToolPackageStatus.UpdateAvailable)
            return Error.Validation("No update available for this tool package.");

        package.UpgradeToLatest(request.UserId);
        await repository.UpdateAsync(package, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return GetToolPackagesQueryHandler.ToResponse(package);
    }
}
