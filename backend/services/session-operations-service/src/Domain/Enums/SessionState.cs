namespace umbral_backend.Domain.Enums;

public enum SessionState
{
    Scheduled = 1,
    Preparing = 2,
    Active = 3,
    Paused = 4,
    Finished = 5,
    Cancelled = 6
}
