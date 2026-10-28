using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status, decimal amount = 100m) =>
        new() { Id = id, CustomerName = "Test School", Amount = amount, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Approve("INV-1", "demo.approver", "Approver");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.approver", "Approver"));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.approver", "Approver"));
    }

    [Theory]
    [InlineData("Manager", 50000)]
    [InlineData("Approver", 50000)]
    [InlineData("Director", 500000)]
    [InlineData("Admin", 500000)]
    public void Approve_AllowsEachRoleAtItsInclusiveLimit(string role, decimal amount)
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, amount));

        var result = service.Approve("INV-1", "test.approver", role);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("test.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsManagerAboveLimitAndPreservesInvoice()
    {
        AssertRejectedApprovalPreservesInvoice("Manager", 50000.01m, "Manager approval limit is $50,000.");
    }

    [Fact]
    public void Approve_RejectsDirectorAboveLimitAndPreservesInvoice()
    {
        AssertRejectedApprovalPreservesInvoice(
            "Director", 500000.01m, "An invoice above $500,000 requires a second Director approval.");
    }

    [Fact]
    public void Approve_RejectsAdminAboveLimitAndPreservesInvoice()
    {
        AssertRejectedApprovalPreservesInvoice(
            "Admin", 500000.01m, "An invoice above $500,000 requires a second Director approval.");
    }

    [Fact]
    public void Approve_RejectsUnsupportedRoleAndPreservesInvoice()
    {
        AssertRejectedApprovalPreservesInvoice(
            "Observer", 100m, "Role 'Observer' is not authorized to approve invoices.");
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
    }

    private static void AssertRejectedApprovalPreservesInvoice(string role, decimal amount, string expectedReason)
    {
        var decidedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, amount);
        invoice.DecidedBy = "previous.approver";
        invoice.DecidedAt = decidedAt;
        invoice.DecisionNote = "Existing decision note";
        var service = ServiceFor(invoice);

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "new.approver", role));

        Assert.Equal(expectedReason, exception.Message);
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal(amount, invoice.Amount);
        Assert.Equal("previous.approver", invoice.DecidedBy);
        Assert.Equal(decidedAt, invoice.DecidedAt);
        Assert.Equal("Existing decision note", invoice.DecisionNote);
    }
}
