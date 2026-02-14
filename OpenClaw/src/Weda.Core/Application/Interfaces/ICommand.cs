using Mediator;

namespace Weda.Core.Application.Interfaces;

public interface ICommand<T> : IRequest<T>;