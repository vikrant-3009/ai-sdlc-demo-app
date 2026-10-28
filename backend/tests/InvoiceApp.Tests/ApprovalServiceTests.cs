using InvoiceApp.Domain;
using InvoiceApp.Api;
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
        Assert.Single(result.ApprovalHistory);
    }

    [Fact]
    public void Approve_RejectsManagerAboveLimitAndPreservesInvoice()
    {
        AssertRejectedApprovalPreservesInvoice("Manager", 50000.01m, "Manager approval limit is $50,000.");
    }

    [Fact]
    public void Approve_RecordsDirectorFirstApprovalAboveSoloLimit()
    {
        AssertFirstApprovalPending("Director");
    }

    [Fact]
    public void Approve_RecordsAdminFirstApprovalAboveSoloLimit()
    {
        AssertFirstApprovalPending("Admin");
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Approver")]
    public void Approve_RejectsManagerRoleAboveLimitAndPreservesInvoice(string role)
    {
        AssertRejectedApprovalPreservesInvoice(role, 50000.01m, "Manager approval limit is $50,000.");
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

        var result = service.Reject("INV-1", "demo.approver", "Approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Collection(result.ApprovalHistory, entry =>
        {
            Assert.Equal(ApprovalOutcome.Rejected, entry.Outcome);
            Assert.Equal("demo.approver", entry.Username);
            Assert.Equal("Duplicate invoice", entry.Reason);
        });
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "Approver", "  "));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Director")]
    [InlineData("Admin")]
    public void Approve_RecordsFirstHighValueApprovalAndLeavesInvoicePending(string role)
    {
        AssertFirstApprovalPending(role);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Director")]
    [InlineData("Admin")]
    public void Approve_DistinctDirectorCompletesEachFirstApproverPath(string firstRole)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000.01m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "first.approver", firstRole);

        var result = service.Approve("INV-1", "second.director", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("second.director", result.DecidedBy);
        Assert.Collection(result.ApprovalHistory,
            first => { Assert.Equal("first.approver", first.Username); Assert.Equal(ApprovalOutcome.Approved, first.Outcome); },
            second => { Assert.Equal("second.director", second.Username); Assert.Equal("Director", second.Role); });
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Admin")]
    public void Approve_RejectsNonDirectorSecondApproverWithoutMutation(string secondRole)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "manager.user", "Manager");
        var firstEntry = Assert.Single(invoice.ApprovalHistory);

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "second.user", secondRole));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Single(invoice.ApprovalHistory);
        Assert.Same(firstEntry, invoice.ApprovalHistory[0]);
    }

    [Fact]
    public void Reject_AfterFirstHighValueApprovalRetainsFirstEntryAndAppendsRejection()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "manager.user", "Manager");
        var firstEntry = Assert.Single(invoice.ApprovalHistory);

        var result = service.Reject("INV-1", "admin.user", "Admin", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("admin.user", result.DecidedBy);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Collection(result.ApprovalHistory,
            first => Assert.Same(firstEntry, first),
            rejection =>
            {
                Assert.Equal(ApprovalOutcome.Rejected, rejection.Outcome);
                Assert.Equal("admin.user", rejection.Username);
                Assert.Equal("Duplicate invoice", rejection.Reason);
            });
    }

    [Fact]
    public async Task Approve_ConcurrentFirstApprovalsRecordOnlyOneFirstEntry()
    {
        var repository = RepositoryWithHighValueInvoice();
        var service = new ApprovalService(repository);

        var outcomes = await Task.WhenAll(
            TryApproveAsync(service, "INV-1", "manager.user", "Manager"),
            TryApproveAsync(service, "INV-1", "admin.user", "Admin"));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        var invoice = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Single(invoice.ApprovalHistory);
        Assert.Equal(ApprovalOutcome.Approved, invoice.ApprovalHistory[0].Outcome);
    }

    [Fact]
    public async Task Approve_ConcurrentSecondDirectorsFinalizeOnce()
    {
        var repository = RepositoryWithHighValueInvoice();
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "manager.user", "Manager");

        var outcomes = await Task.WhenAll(
            TryApproveAsync(service, "INV-1", "demo.director", "Director"),
            TryApproveAsync(service, "INV-1", "demo.director2", "Director"));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        var invoice = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.Approved, invoice.Status);
        Assert.Equal(2, invoice.ApprovalHistory.Count);
        Assert.Equal(2, invoice.ApprovalHistory.Select(entry => entry.Username).Distinct().Count());
    }

    [Fact]
    public async Task Approve_ConcurrentRequestsFromFirstDirectorCannotCountTwice()
    {
        var repository = RepositoryWithHighValueInvoice();
        var service = new ApprovalService(repository);

        var outcomes = await Task.WhenAll(
            TryApproveAsync(service, "INV-1", "demo.director", "Director"),
            TryApproveAsync(service, "INV-1", "demo.director", "Director"));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        var invoice = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        var firstApproval = Assert.Single(invoice.ApprovalHistory);
        Assert.Equal("demo.director", firstApproval.Username);
    }

    [Fact]
    public void Approve_RejectsSameDirectorAsSecondApproverWithoutMutatingStoredInvoice()
    {
        var repository = RepositoryWithHighValueInvoice();
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "demo.director", "Director");
        var before = repository.GetById("INV-1")!;

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.director", "Director"));

        var after = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.PendingApproval, after.Status);
        Assert.Equal(before.ApprovalHistory, after.ApprovalHistory);
        Assert.Null(after.DecidedBy);
        Assert.Null(after.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsRepeatedRequestAfterCompletionWithoutMutation()
    {
        var repository = RepositoryWithHighValueInvoice();
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "manager.user", "Manager");
        service.Approve("INV-1", "demo.director", "Director");
        var before = repository.GetById("INV-1")!;

        Assert.Throws<InvalidOperationException>(
            () => service.Approve("INV-1", "demo.director2", "Director"));

        var after = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.Approved, after.Status);
        Assert.Equal(before.ApprovalHistory, after.ApprovalHistory);
        Assert.Equal(before.DecidedBy, after.DecidedBy);
        Assert.Equal(before.DecidedAt, after.DecidedAt);
    }

    [Fact]
    public void Reject_RejectsRepeatedRequestAfterCompletionWithoutMutation()
    {
        var repository = RepositoryWithHighValueInvoice();
        var service = new ApprovalService(repository);
        service.Reject("INV-1", "admin.user", "Admin", "Duplicate invoice");
        var before = repository.GetById("INV-1")!;

        Assert.Throws<InvalidOperationException>(
            () => service.Reject("INV-1", "admin.user", "Admin", "Another reason"));

        var after = repository.GetById("INV-1")!;
        Assert.Equal(InvoiceStatus.Rejected, after.Status);
        Assert.Equal(before.ApprovalHistory, after.ApprovalHistory);
        Assert.Equal(before.DecidedBy, after.DecidedBy);
        Assert.Equal(before.DecidedAt, after.DecidedAt);
        Assert.Equal("Duplicate invoice", after.DecisionNote);
    }

    [Fact]
    public async Task ApproveAndReject_ConcurrentSecondDecisionHasOneSerializedWinner()
    {
        var repository = RepositoryWithHighValueInvoice();
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "manager.user", "Manager");

        var outcomes = await Task.WhenAll(
            TryApproveAsync(service, "INV-1", "demo.director", "Director"),
            TryRejectAsync(service, "INV-1", "admin.user", "Admin", "Duplicate invoice"));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        var invoice = repository.GetById("INV-1")!;
        Assert.Contains(invoice.Status, new[] { InvoiceStatus.Approved, InvoiceStatus.Rejected });
        Assert.Equal(2, invoice.ApprovalHistory.Count);
        Assert.Equal(invoice.Status == InvoiceStatus.Approved
            ? ApprovalOutcome.Approved
            : ApprovalOutcome.Rejected, invoice.ApprovalHistory[^1].Outcome);
    }

    [Fact]
    public void Repository_AbortedTransitionDoesNotCommitWorkingCopy()
    {
        var repository = RepositoryWithHighValueInvoice();

        Assert.Throws<InvalidOperationException>(() => repository.UpdateAtomically("INV-1", invoice =>
        {
            invoice.Amount = 1m;
            throw new InvalidOperationException("Abort transition");
        }));

        Assert.Equal(600000m, repository.GetById("INV-1")!.Amount);
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

    private static void AssertFirstApprovalPending(string role)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000.01m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "first.approver", role);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        var entry = Assert.Single(result.ApprovalHistory);
        Assert.Equal("first.approver", entry.Username);
        Assert.Equal(role, entry.Role);
        Assert.Equal(ApprovalOutcome.Approved, entry.Outcome);
    }

    private static InMemoryInvoiceRepository RepositoryWithHighValueInvoice()
    {
        var repository = new InMemoryInvoiceRepository();
        repository.Add(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 600000m));
        return repository;
    }

    private static async Task<Exception?> TryApproveAsync(
        ApprovalService service, string invoiceId, string username, string role)
    {
        try
        {
            await Task.Run(() => service.Approve(invoiceId, username, role));
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<Exception?> TryRejectAsync(
        ApprovalService service, string invoiceId, string username, string role, string reason)
    {
        try
        {
            await Task.Run(() => service.Reject(invoiceId, username, role, reason));
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
