using InvoiceApp.Domain;

namespace InvoiceApp.Tests;

internal class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = new();

    public FakeInvoiceRepository(params Invoice[] seed) => _invoices.AddRange(seed);

    public IReadOnlyList<Invoice> GetAll() => _invoices.ToList();
    public Invoice? GetById(string id) => _invoices.FirstOrDefault(i => i.Id == id);
    public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids) => _invoices.Where(i => ids.Contains(i.Id)).ToList();
    public Invoice Add(Invoice invoice) { _invoices.Add(invoice); return invoice; }
    public void Update(Invoice invoice) { }
    public Invoice UpdateAtomically(string id, Action<Invoice> transition)
    {
        var index = _invoices.FindIndex(invoice => invoice.Id == id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Invoice {id} was not found.");
        }

        var updated = Copy(_invoices[index]);
        transition(updated);
        CopyInto(_invoices[index], updated);
        return Copy(_invoices[index]);
    }
    public bool Remove(string id) => _invoices.RemoveAll(i => i.Id == id) > 0;

    private static Invoice Copy(Invoice invoice) => new()
    {
        Id = invoice.Id,
        CustomerName = invoice.CustomerName,
        Amount = invoice.Amount,
        Status = invoice.Status,
        CreatedAt = invoice.CreatedAt,
        DecidedBy = invoice.DecidedBy,
        DecidedAt = invoice.DecidedAt,
        DecisionNote = invoice.DecisionNote,
        ApprovalHistory = invoice.ApprovalHistory.ToList()
    };

    private static void CopyInto(Invoice destination, Invoice source)
    {
        destination.CustomerName = source.CustomerName;
        destination.Amount = source.Amount;
        destination.Status = source.Status;
        destination.CreatedAt = source.CreatedAt;
        destination.DecidedBy = source.DecidedBy;
        destination.DecidedAt = source.DecidedAt;
        destination.DecisionNote = source.DecisionNote;
        destination.ApprovalHistory = source.ApprovalHistory.ToList();
    }
}
