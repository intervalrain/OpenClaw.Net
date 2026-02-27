using ErrorOr;

using Microsoft.EntityFrameworkCore.Metadata.Internal;

using OpenClaw.Contracts.Setup.Dtos;
using OpenClaw.Domain.Configuration.Repositories;
using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Contracts.Setup.Queries;

public record GetSetupStatusQuery : IQuery<ErrorOr<GetSetupStatusResult>>;

public record GetSetupStatusResult(bool HasUser, bool hasModelProvider, ActiveProviderInfo? ActiveProvider);

public class GetSetupStatusQueryHandler(
    IUserRepository userRepository,
    IModelProviderRepository modelProviderRepository) 
    : Mediator.IRequestHandler<GetSetupStatusQuery, ErrorOr<GetSetupStatusResult>>
{
    public async ValueTask<ErrorOr<GetSetupStatusResult>> Handle(GetSetupStatusQuery request, CancellationToken ct)
    {
        var hasUser = await userRepository.AnyAsync(ct);
        var activeProvider = await modelProviderRepository.GetActiveAsync(ct);

        return new GetSetupStatusResult(
            HasUser: hasUser,
            hasModelProvider: activeProvider is not null,
            ActiveProvider: activeProvider is null ? null : new ActiveProviderInfo(
                activeProvider.Id,
                activeProvider.Type,
                activeProvider.Name,
                activeProvider.ModelName));
    }
}