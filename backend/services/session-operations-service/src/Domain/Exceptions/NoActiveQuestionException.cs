namespace umbral_backend.Domain.Exceptions;

public sealed class NoActiveQuestionException : Exception
{
    public NoActiveQuestionException()
        : base("A live session must have an active question before the question can be closed.")
    {
    }
}
