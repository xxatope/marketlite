using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Payment.Api.Data;
using Payment.Api.Domain;
using Payment.Api.DTOs;
using Payment.Api.Services;

namespace Payment.Tests;

public sealed class PaymentServiceTests
{
    private static PaymentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task CreateAccountAsync_PersistsAndReturnsAccount()
    {
        await using var db = CreateDbContext();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var userId = Guid.NewGuid();

        var response = await service.CreateAccountAsync(new CreateAccountRequest(userId, "RUB"));

        Assert.NotNull(response);
        Assert.Equal(userId, response.UserId);
        Assert.Equal(0m, response.Balance);
        Assert.Equal("RUB", response.Currency);

        var persisted = await db.Accounts.FindAsync(response.Id);
        Assert.NotNull(persisted);
        Assert.Equal(userId, persisted.UserId);
    }

    [Fact]
    public async Task DepositAsync_PersistsBalanceAndLedger()
    {
        await using var db = CreateDbContext();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var account = await service.CreateAccountAsync(new CreateAccountRequest(Guid.NewGuid(), "RUB"));

        var response = await service.DepositAsync(account.Id, new DepositRequest(500m, "Top up"));
        var ledger = await service.GetLedgerAsync(account.Id);

        Assert.Equal(500m, response.Balance);
        Assert.Equal(500m, response.AvailableBalance);
        Assert.Single(ledger);
        Assert.Equal(500m, ledger[0].Amount);
        Assert.Equal(TransactionType.Deposit, ledger[0].Type);
    }

    [Fact]
    public async Task CreateHoldAsync_IdempotentOnDuplicateKey()
    {
        await using var db = CreateDbContext();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var account = await service.CreateAccountAsync(new CreateAccountRequest(Guid.NewGuid(), "RUB"));
        await service.DepositAsync(account.Id, new DepositRequest(1000m, "Top up"));

        var orderId = Guid.NewGuid();
        var request = new CreateHoldRequest(account.Id, orderId, 250m, "RUB", "idem-unique-key-999");

        var first = await service.CreateHoldAsync(request);
        var duplicate = await service.CreateHoldAsync(request);

        Assert.Equal(first.Id, duplicate.Id);
        Assert.Equal(HoldStatus.Active, first.Status);

        var refreshedAccount = await service.GetAccountByIdAsync(account.Id);
        Assert.NotNull(refreshedAccount);
        Assert.Equal(250m, refreshedAccount.HeldBalance);
        Assert.Equal(750m, refreshedAccount.AvailableBalance);
    }

    [Fact]
    public async Task CreateHoldAsync_ThrowsConflict_WhenPayloadMismatches()
    {
        await using var db = CreateDbContext();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var account = await service.CreateAccountAsync(new CreateAccountRequest(Guid.NewGuid(), "RUB"));
        await service.DepositAsync(account.Id, new DepositRequest(1000m, "Top up"));

        var orderId = Guid.NewGuid();
        var initialRequest = new CreateHoldRequest(account.Id, orderId, 250m, "RUB", "idem-conflict-key");
        await service.CreateHoldAsync(initialRequest);

        var conflictingRequest = new CreateHoldRequest(account.Id, orderId, 350m, "RUB", "idem-conflict-key");

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => service.CreateHoldAsync(conflictingRequest));
    }

    [Fact]
    public async Task CaptureHoldAsync_UpdatesDatabaseState()
    {
        await using var db = CreateDbContext();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var account = await service.CreateAccountAsync(new CreateAccountRequest(Guid.NewGuid(), "RUB"));
        await service.DepositAsync(account.Id, new DepositRequest(1000m, "Top up"));
        var hold = await service.CreateHoldAsync(new CreateHoldRequest(account.Id, Guid.NewGuid(), 300m, "RUB", "idem-cap-1"));

        var captured = await service.CaptureHoldAsync(new CaptureHoldRequest(hold.Id));

        Assert.Equal(HoldStatus.Captured, captured.Status);
        var refreshedAccount = await service.GetAccountByIdAsync(account.Id);
        Assert.NotNull(refreshedAccount);
        Assert.Equal(700m, refreshedAccount.Balance);
        Assert.Equal(0m, refreshedAccount.HeldBalance);
        Assert.Equal(700m, refreshedAccount.AvailableBalance);
    }

    [Fact]
    public async Task ReleaseHoldAsync_RestoresHeldBalance()
    {
        await using var db = CreateDbContext();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var account = await service.CreateAccountAsync(new CreateAccountRequest(Guid.NewGuid(), "RUB"));
        await service.DepositAsync(account.Id, new DepositRequest(1000m, "Top up"));
        var hold = await service.CreateHoldAsync(new CreateHoldRequest(account.Id, Guid.NewGuid(), 300m, "RUB", "idem-rel-1"));

        var released = await service.ReleaseHoldAsync(new ReleaseHoldRequest(hold.Id, "Cancelled by user"));

        Assert.Equal(HoldStatus.Released, released.Status);
        var refreshedAccount = await service.GetAccountByIdAsync(account.Id);
        Assert.NotNull(refreshedAccount);
        Assert.Equal(1000m, refreshedAccount.Balance);
        Assert.Equal(0m, refreshedAccount.HeldBalance);
        Assert.Equal(1000m, refreshedAccount.AvailableBalance);
    }

    [Fact]
    public async Task LedgerReconciliation_StrictlyMatchesAccountBalance_AfterComplexSequence()
    {
        await using var db = CreateDbContext();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var account = await service.CreateAccountAsync(new CreateAccountRequest(Guid.NewGuid(), "RUB"));

        await service.DepositAsync(account.Id, new DepositRequest(2000m, "Initial topup"));
        await service.DepositAsync(account.Id, new DepositRequest(500m, "Salary bonus"));

        var hold1 = await service.CreateHoldAsync(new CreateHoldRequest(account.Id, Guid.NewGuid(), 700m, "RUB", "seq-hold-1"));
        var hold2 = await service.CreateHoldAsync(new CreateHoldRequest(account.Id, Guid.NewGuid(), 400m, "RUB", "seq-hold-2"));

        await service.CaptureHoldAsync(new CaptureHoldRequest(hold1.Id));
        await service.ReleaseHoldAsync(new ReleaseHoldRequest(hold2.Id, "Item canceled"));

        var refreshedAccount = await service.GetAccountByIdAsync(account.Id);
        var ledger = await service.GetLedgerAsync(account.Id);

        Assert.NotNull(refreshedAccount);
        Assert.Equal(1800m, refreshedAccount.Balance);
        Assert.Equal(0m, refreshedAccount.HeldBalance);
        Assert.Equal(1800m, refreshedAccount.AvailableBalance);

        var ledgerSum = ledger.Sum(l => l.Amount);
        Assert.Equal(refreshedAccount.Balance, ledgerSum);
    }
}
