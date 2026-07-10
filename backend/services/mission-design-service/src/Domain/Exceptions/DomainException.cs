using System.Text;

namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Base type for every domain exception. The abstract <see cref="Category"/> forces each
/// concrete exception to classify itself, making it structurally impossible for a new
/// exception to fall through to an unmapped HTTP 500.
/// </summary>
public abstract class DomainException : Exception, IErrorMetadata
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc />
    public abstract ErrorCategory Category { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Suppressed by default so a domain message — which routinely interpolates identifiers —
    /// never reaches the client. A concrete exception overrides this with a curated,
    /// identifier-free sentence when its explanation is safe to expose.
    /// </remarks>
    public virtual string? PublicDetail => null;

    /// <inheritdoc />
    /// <remarks>
    /// Derived from the concrete type name by default
    /// (<c>TargetNotFoundException</c> → <c>target-not-found</c>); override to preserve an
    /// existing client-facing slug.
    /// </remarks>
    public virtual string ErrorCode => DeriveErrorCode(GetType().Name);

    /// <summary>
    /// Converts a PascalCase type name into a kebab-case error code, dropping a trailing
    /// <c>Exception</c> suffix. e.g. <c>TargetNotFoundException</c> → <c>target-not-found</c>.
    /// </summary>
    public static string DeriveErrorCode(string typeName)
    {
        const string suffix = "Exception";
        if (typeName.EndsWith(suffix, StringComparison.Ordinal) && typeName.Length > suffix.Length)
        {
            typeName = typeName[..^suffix.Length];
        }

        var builder = new StringBuilder(typeName.Length + 8);
        for (var i = 0; i < typeName.Length; i++)
        {
            var c = typeName[i];
            if (char.IsUpper(c) && i > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
