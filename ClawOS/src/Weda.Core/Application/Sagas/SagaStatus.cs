namespace Weda.Core.Application.Sagas;

public enum SagaStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Compensating,
    Compensated
}