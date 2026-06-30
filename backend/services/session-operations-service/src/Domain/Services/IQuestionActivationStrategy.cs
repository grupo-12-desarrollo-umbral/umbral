using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Services;

// ponytail: single impl, no selector — kept only because HU-33A/B matrix-names it (siblings coming).
// Add the selector/factory when the 2nd activation policy lands; if it never does, inline and delete this.
public interface IQuestionActivationStrategy
{
    int? Next(LiveSession session);
}
