namespace umbral_backend.Domain.Enums;

// Discriminates the concrete evidence form under the EvidenceSubmission umbrella base.
// HU-34 introduces the trivia answer form; the QR/treasure form lands with the treasure-hunt runtime.
public enum EvidenceSubmissionType
{
    TreasureHuntQrScan = 1,
    TriviaAnswer = 2
}
