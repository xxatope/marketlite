using Payment.Api.Domain;

namespace Payment.Tests;

public sealed class AccountDomainTests
{
    [Fact]
    public void Create_InitializesEmptyBalances()
    {
        var userId = Guid.NewGuid();
        var account = Account.Create(userId, "rub");

        Assert.Equal(userId, account.UserId);
        Assert.Equal("RUB", account.Currency);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(0m, account.HeldBalance);
        Assert.Equal(0m, account.AvailableBalance);
        Assert.Empty(account.Holds);
        Assert.Empty(account.LedgerEntries);
    }

    [Fact]
    public void Deposit_IncreasesBalance_AndRecordsLedger()
    {
        var account = Account.Create(Guid.NewGuid(), "USD");

        var entry = account.Deposit(250.75m, "Initial deposit");

        Assert.Equal(250.75m, account.Balance);
        Assert.Equal(0m, account.HeldBalance);
        Assert.Equal(250.75m, account.AvailableBalance);
        Assert.Single(account.LedgerEntries);
        Assert.Equal(250.75m, entry.Amount);
        Assert.Equal(TransactionType.Deposit, entry.Type);
        Assert.Equal(250.75m, entry.BalanceAfter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Deposit_ThrowsWhenAmountIsNotPositive(decimal amount)
    {
        var account = Account.Create(Guid.NewGuid(), "USD");

        Assert.Throws<ArgumentOutOfRangeException>(() => account.Deposit(amount, "Invalid"));
    }

    [Fact]
    public void CreateHold_LocksAvailableBalance_AndRecordsLedger()
    {
        var account = Account.Create(Guid.NewGuid(), "RUB");
        account.Deposit(1000m, "Top up");
        var orderId = Guid.NewGuid();

        var (hold, _) = account.CreateHold(orderId, 300m, "idemp-1");

        Assert.Equal(1000m, account.Balance);
        Assert.Equal(300m, account.HeldBalance);
        Assert.Equal(700m, account.AvailableBalance);
        Assert.Equal(HoldStatus.Active, hold.Status);
        Assert.Equal(orderId, hold.OrderId);
        Assert.Contains(account.LedgerEntries, e => e.Type == TransactionType.HoldCreated && e.Amount == -300m);
    }

    [Fact]
    public void CreateHold_ThrowsWhenInsufficientFunds()
    {
        var account = Account.Create(Guid.NewGuid(), "RUB");
        account.Deposit(100m, "Top up");

        Assert.Throws<InsufficientFundsException>(() => account.CreateHold(Guid.NewGuid(), 101m, "idemp-2"));
    }

    [Fact]
    public void CaptureHold_DecreasesTotalBalanceAndHeldBalance()
    {
        var account = Account.Create(Guid.NewGuid(), "RUB");
        account.Deposit(1000m, "Top up");
        var (hold, _) = account.CreateHold(Guid.NewGuid(), 400m, "idemp-3");

        account.CaptureHold(hold.Id);

        Assert.Equal(600m, account.Balance);
        Assert.Equal(0m, account.HeldBalance);
        Assert.Equal(600m, account.AvailableBalance);
        Assert.Equal(HoldStatus.Captured, hold.Status);
        Assert.NotNull(hold.ResolvedAt);
        Assert.Contains(account.LedgerEntries, e => e.Type == TransactionType.HoldCaptured && e.Amount == -400m);
    }

    [Fact]
    public void ReleaseHold_UnlocksHeldBalance()
    {
        var account = Account.Create(Guid.NewGuid(), "RUB");
        account.Deposit(1000m, "Top up");
        var (hold, _) = account.CreateHold(Guid.NewGuid(), 400m, "idemp-4");

        account.ReleaseHold(hold.Id, "Item out of stock");

        Assert.Equal(1000m, account.Balance);
        Assert.Equal(0m, account.HeldBalance);
        Assert.Equal(1000m, account.AvailableBalance);
        Assert.Equal(HoldStatus.Released, hold.Status);
        Assert.NotNull(hold.ResolvedAt);
        Assert.Contains(account.LedgerEntries, e => e.Type == TransactionType.HoldReleased && e.Amount == 400m);
    }

    [Fact]
    public void Hold_ThrowsWhenCapturingNonActiveHold()
    {
        var account = Account.Create(Guid.NewGuid(), "RUB");
        account.Deposit(1000m, "Top up");
        var (hold, _) = account.CreateHold(Guid.NewGuid(), 200m, "idemp-5");
        account.CaptureHold(hold.Id);

        Assert.Throws<InvalidHoldStateException>(() => account.CaptureHold(hold.Id));
    }
}
