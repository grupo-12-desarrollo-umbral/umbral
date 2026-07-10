using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// The QR-validated treasure-hunt objective owned by a <see cref="Substage"/> in
/// <c>TreasureHunt</c> play mode. Treasure-hunt progress is target-based: a target
/// is resolved when its QR code is validated. A target may reference at most one
/// <see cref="Clue"/> from its own substage as optional guidance; that reference
/// never advances the target.
/// </summary>
public sealed class Target : BaseEntity
{
    private Target()
    {
        Name = string.Empty;
        QrCode = string.Empty;
    }

    private Target(string name, string qrCode, int sequenceOrder, ScoreValue score, bool isActive)
    {
        Name = name;
        QrCode = qrCode;
        SequenceOrder = sequenceOrder;
        Score = score;
        IsActive = isActive;
    }

    public string Name { get; private set; }

    public string QrCode { get; private set; }

    public int SequenceOrder { get; private set; }

    public ScoreValue Score { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public int? ClueId { get; private set; }

    public static Target Create(string name, string qrCode, int sequenceOrder, int score, bool isActive = true)
    {
        return new Target(
            ValidateName(name),
            ValidateQrCode(qrCode),
            ValidateSequenceOrder(sequenceOrder),
            ScoreValue.Create(score),
            isActive);
    }

    public void UpdateDetails(string name, string qrCode, int sequenceOrder, bool isActive, int? score = null)
    {
        Name = ValidateName(name);
        QrCode = ValidateQrCode(qrCode);
        SequenceOrder = ValidateSequenceOrder(sequenceOrder);
        IsActive = isActive;

        if (score is not null)
        {
            Score = ScoreValue.Create(score.Value);
        }
    }

    // Re-derives the score from the owning mission's difficulty. Called by the
    // aggregate when difficulty changes; score is never authored directly.
    internal void Reprice(int score)
    {
        Score = ScoreValue.Create(score);
    }

    internal void AssociateClue(int clueId)
    {
        // Guidance only: associating a clue never resolves or advances the target.
        if (ClueId is not null && ClueId != clueId)
        {
            throw new TargetMayReferenceAtMostOneClueException();
        }

        ClueId = clueId;
    }

    internal void ClearClue()
    {
        ClueId = null;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new TargetNameRequiredException();
        }

        return name.Trim();
    }

    private static string ValidateQrCode(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
        {
            throw new TargetQrCodeRequiredException();
        }

        return qrCode.Trim();
    }

    private static int ValidateSequenceOrder(int sequenceOrder)
    {
        if (sequenceOrder <= 0)
        {
            throw new TargetSequenceOrderMustBePositiveException();
        }

        return sequenceOrder;
    }
}
