namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionIndexOutOfRangeException : Exception
{
    public QuestionIndexOutOfRangeException(int questionIndex)
        : base($"Question index '{questionIndex}' is outside the trivia snapshot question range.")
    {
    }
}
