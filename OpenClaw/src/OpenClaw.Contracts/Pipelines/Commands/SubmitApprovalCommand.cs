using ErrorOr;

using Mediator;

namespace OpenClaw.Contracts.Pipelines.Commands;

public record SubmitApprovalCommand(
    string ExecutionId,
    bool Approved) : IRequest<ErrorOr<Success>>;
