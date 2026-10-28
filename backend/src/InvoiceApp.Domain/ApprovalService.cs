namespace InvoiceApp.Domain;

/// <summary>
/// Moves invoices through their lifecycle and applies approval authority rules.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        return _repository.UpdateAtomically(invoiceId, invoice =>
        {
            if (invoice.Status != InvoiceStatus.Draft)
            {
                throw new InvalidOperationException(
                    $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
            }

            invoice.Status = InvoiceStatus.PendingApproval;
        });
    }

    public Invoice Approve(string invoiceId, string approver, string role)
    {
        var (approvalRole, limit) = GetSingleApproverPolicy(role);
        return _repository.UpdateAtomically(invoiceId, invoice =>
        {
            EnsurePending(invoice);
            var decidedAt = DateTime.UtcNow;

            if (invoice.Amount <= limit)
            {
                invoice.ApprovalHistory.Add(new ApprovalHistoryEntry(
                    approver, role, decidedAt, ApprovalOutcome.Approved, null));
                FinalizeApproval(invoice, approver, decidedAt);
                return;
            }

            if (invoice.Amount <= 500_000m)
            {
                throw new InvalidOperationException("Manager approval limit is $50,000.");
            }

            var approvals = invoice.ApprovalHistory
                .Where(entry => entry.Outcome == ApprovalOutcome.Approved)
                .ToList();

            if (approvals.Count == 0)
            {
                invoice.ApprovalHistory.Add(new ApprovalHistoryEntry(
                    approver, role, decidedAt, ApprovalOutcome.Approved, null));
                return;
            }

            if (approvals.Count != 1)
            {
                throw new InvalidOperationException("This invoice is not awaiting a second approval.");
            }

            if (approvalRole != "Director")
            {
                throw new InvalidOperationException("A Director must provide the second approval.");
            }

            if (string.Equals(approvals[0].Username, approver, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The second approver must be a different user.");
            }

            invoice.ApprovalHistory.Add(new ApprovalHistoryEntry(
                approver, role, decidedAt, ApprovalOutcome.Approved, null));
            FinalizeApproval(invoice, approver, decidedAt);
        });
    }

    public Invoice Reject(string invoiceId, string approver, string role, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        GetSingleApproverPolicy(role);
        return _repository.UpdateAtomically(invoiceId, invoice =>
        {
            EnsurePending(invoice);
            var decidedAt = DateTime.UtcNow;
            invoice.ApprovalHistory.Add(new ApprovalHistoryEntry(
                approver, role, decidedAt, ApprovalOutcome.Rejected, reason));
            invoice.Status = InvoiceStatus.Rejected;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = decidedAt;
            invoice.DecisionNote = reason;
        });
    }

    private static void FinalizeApproval(Invoice invoice, string approver, DateTime decidedAt)
    {
        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = decidedAt;
        invoice.DecisionNote = null;
    }

    private static (string Role, decimal Limit) GetSingleApproverPolicy(string role) => role switch
    {
        "Manager" or "Approver" => ("Manager", 50_000m),
        "Director" or "Admin" => (role, 500_000m),
        _ => throw new InvalidOperationException($"Role '{role}' is not authorized to approve invoices.")
    };

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
