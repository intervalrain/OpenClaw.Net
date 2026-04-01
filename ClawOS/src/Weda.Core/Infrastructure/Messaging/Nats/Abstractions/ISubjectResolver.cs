namespace Weda.Core.Infrastructure.Messaging.Nats.Abstractions;

public interface ISubjectResolver
{
    bool CanResolve(string subject);
    SubjectInfo Parse(string subject);
    string Build(SubjectInfo info);
}