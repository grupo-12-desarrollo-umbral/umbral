using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common.Lifecycle;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;

[Authorize(Roles = Roles.Administrator)]
public sealed record ArchiveTriviaQuizCommand(int Id) : IRequest<TriviaQuizDto>, ITriviaQuizLifecycleCommand;
