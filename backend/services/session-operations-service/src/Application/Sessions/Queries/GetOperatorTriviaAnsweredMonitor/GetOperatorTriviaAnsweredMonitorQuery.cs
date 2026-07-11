using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorTriviaAnsweredMonitor;

// HU-36A: pre-close restricted trivia monitor read. Returns per-team answered/not-answered for the
// session's active synchronized question, gated to the assigned operator by the ownership resolver Proxy.
[Authorize(Roles = "Operator")]
public sealed record GetOperatorTriviaAnsweredMonitorQuery(Guid LiveSessionId)
    : IRequest<TriviaAnsweredMonitorDto>;
