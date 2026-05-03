namespace B2.Domain.Audit;

public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Cancel = 3,
    UnExit = 4,
    Restore = 5,
    OfficerNameChanged = 6,
}
