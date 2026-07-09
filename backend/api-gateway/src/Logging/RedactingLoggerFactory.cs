using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Logging;

/// <summary>
/// Wraps the real <see cref="ILoggerFactory"/> so records from the framework categories that echo
/// the raw request query string have their sensitive parameters redacted before any sink (stdout
/// today, OTLP once PR #125 lands) observes them.
/// </summary>
/// <remarks>
/// Interception at the logger — not a middleware that rewrites <c>HttpContext.Request.QueryString</c>
/// — is required: <c>HostingApplication.CreateContext</c> writes the "Request starting" record
/// before the first middleware in the pipeline runs, so no middleware can suppress it. Both target
/// records are obtained by their owners through <c>ILoggerFactory.CreateLogger</c> (the hosting
/// diagnostics logger by category string, YARP's forwarder via <c>ILogger&lt;HttpForwarder&gt;</c>),
/// so decorating the factory catches them at the source.
/// </remarks>
public sealed class RedactingLoggerFactory : ILoggerFactory
{
    // The exact categories whose records carry the query string verbatim (finding: three records,
    // two categories). Everything else passes straight through untouched.
    private static readonly HashSet<string> RedactedCategories = new(StringComparer.Ordinal)
    {
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "Yarp.ReverseProxy.Forwarder.HttpForwarder",
    };

    private readonly ILoggerFactory _inner;

    public RedactingLoggerFactory(ILoggerFactory inner) => _inner = inner;

    public ILogger CreateLogger(string categoryName)
    {
        var logger = _inner.CreateLogger(categoryName);
        return RedactedCategories.Contains(categoryName)
            ? new RedactingLogger(logger)
            : logger;
    }

    public void AddProvider(ILoggerProvider provider) => _inner.AddProvider(provider);

    public void Dispose() => _inner.Dispose();

    /// <summary>
    /// Decorates the host's <see cref="ILoggerFactory"/> with query-parameter redaction. Registers
    /// the concrete <see cref="LoggerFactory"/> so the decorator can resolve and wrap the real one,
    /// then replaces the <see cref="ILoggerFactory"/> registration with the decorator.
    /// </summary>
    public static ILoggingBuilder AddQueryParameterRedaction(ILoggingBuilder logging)
    {
        logging.Services.TryAddSingleton<LoggerFactory>();
        logging.Services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(
            static sp => new RedactingLoggerFactory(sp.GetRequiredService<LoggerFactory>())));
        return logging;
    }
}

/// <summary>
/// Delegating logger that redacts sensitive query-string parameters from a record's formatted
/// message and its structured state before handing it to the underlying logger.
/// </summary>
internal sealed class RedactingLogger : ILogger
{
    private readonly ILogger _inner;

    public RedactingLogger(ILogger inner) => _inner = inner;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = SensitiveQueryLogRedactor.Redact(formatter(state, exception));
        var redactedState = RedactState(state);

        // The redacted message is captured verbatim; the redacted state preserves the original
        // property names (QueryString, targetUrl, ...) so a structured sink never indexes the token.
        _inner.Log(logLevel, eventId, redactedState, exception, (_, _) => message);
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> RedactState<TState>(TState state)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> properties)
        {
            return state is null
                ? Array.Empty<KeyValuePair<string, object?>>()
                : new[] { new KeyValuePair<string, object?>("{OriginalFormat}", state.ToString()) };
        }

        KeyValuePair<string, object?>[]? redacted = null;
        for (var i = 0; i < properties.Count; i++)
        {
            var entry = properties[i];
            if (entry.Value is not string original)
            {
                continue;
            }

            var value = SensitiveQueryLogRedactor.Redact(original);
            if (!string.Equals(value, original, StringComparison.Ordinal))
            {
                redacted ??= properties.ToArray();
                redacted[i] = new KeyValuePair<string, object?>(entry.Key, value);
            }
        }

        return redacted ?? properties;
    }
}
