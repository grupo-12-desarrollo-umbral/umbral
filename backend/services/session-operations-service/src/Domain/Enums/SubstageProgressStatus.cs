namespace umbral_backend.Domain.Enums;

// Ordered-sequence position of a substage relative to the live-substage pointer (#171):
// already advanced past (Completed), the active one (Active), or still ahead (Upcoming).
public enum SubstageProgressStatus
{
    Completed,
    Active,
    Upcoming
}
