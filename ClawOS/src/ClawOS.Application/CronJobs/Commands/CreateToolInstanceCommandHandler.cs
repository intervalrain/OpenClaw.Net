using ErrorOr;
using Mediator;
using ClawOS.Contracts.CronJobs.Commands;
using ClawOS.Contracts.CronJobs.Responses;
using ClawOS.Domain.CronJobs.Entities;
using ClawOS.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace ClawOS.Application.CronJobs.Commands;

public class CreateToolInstanceCommandHandler(
    IToolInstanceRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateToolInstanceCommand, ErrorOr<ToolInstanceResponse>>
{
    public async ValueTask<ErrorOr<ToolInstanceResponse>> Handle(
        CreateToolInstanceCommand request,
        CancellationToken ct)
    {
        var instance = ToolInstance.Create(
            request.UserId,
            request.Name,
            request.ToolName,
            request.ArgsJson ?? string.Empty,
            request.Description);

        await repository.AddAsync(instance, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ToResponse(instance);
    }

    internal static ToolInstanceResponse ToResponse(ToolInstance instance) => new()
    {
        Id = instance.Id,
        Name = instance.Name,
        ToolName = instance.ToolName,
        ArgsJson = instance.ArgsJson,
        Description = instance.Description,
        CreatedByUserId = instance.CreatedByUserId,
        CreatedAt = instance.CreatedAt,
        UpdatedAt = instance.UpdatedAt
    };
}
