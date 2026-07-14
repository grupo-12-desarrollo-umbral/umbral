namespace umbral_backend.Application.Users.Commands.ForgotPassword;

// Anonymous by design (ADR-0016 §1): a person who forgot their password has no session to authenticate
// with, exactly as login and register are unauthenticated. Carries only the email — the handler decides
// silently whether to send a reset link, so this request can never be used to probe account existence.
public sealed record ForgotPasswordCommand(string Email) : IRequest;
