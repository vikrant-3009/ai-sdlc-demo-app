namespace InvoiceApp.Domain;

public enum ApprovalOutcome
{
    Approved,
    Rejected
}

public sealed record ApprovalHistoryEntry(
    string Username,
    string Role,
    DateTime DecidedAt,
    ApprovalOutcome Outcome,
    string? Reason);