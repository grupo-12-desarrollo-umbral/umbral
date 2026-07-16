using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Exceptions;

public sealed class TriviaQuestionResultNotAvailableException : Exception, IErrorMetadata
{
    public ErrorCategory Category => ErrorCategory.Conflict;

    public string ErrorCode => "trivia-question-result-not-available";

    public string? PublicDetail => "The trivia question result is not available until the question closes.";
}
