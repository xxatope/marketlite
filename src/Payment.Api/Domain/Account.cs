namespace Payment.Api.Domain;

public sealed class Account
{
    private readonly List<Hold> _holds = new();
    private readonly List<TransactionLedger> _ledgerEntries = new();

    private Account()
    {
    }

    private Account(Guid userId, string currency)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Currency = currency;
        Balance = 0m;
        HeldBalance = 0m;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public decimal HeldBalance { get; private set; }
    public decimal AvailableBalance => Balance - HeldBalance;
    public DateTime CreatedAt { get; private set; }
    public uint Version { get; private set; }

    public IReadOnlyCollection<Hold> Holds => _holds.AsReadOnly();
    public IReadOnlyCollection<TransactionLedger> LedgerEntries => _ledgerEntries.AsReadOnly();

    public static Account Create(Guid userId, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        return new Account(userId, currency.Trim().ToUpperInvariant());
    }

    public TransactionLedger Deposit(decimal amount, string description)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be strictly positive.");
        }

        Balance += amount;

        var entry = new TransactionLedger(
            Id,
            amount,
            TransactionType.Deposit,
            null,
            string.IsNullOrWhiteSpace(description) ? "Balance deposit" : description,
            Balance);

        _ledgerEntries.Add(entry);
        return entry;
    }

    public Hold CreateHold(Guid orderId, decimal amount, string idempotencyKey)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Hold amount must be strictly positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (AvailableBalance < amount)
        {
            throw new InsufficientFundsException(amount, AvailableBalance, Currency);
        }

        HeldBalance += amount;

        var hold = new Hold(Id, orderId, amount, idempotencyKey);
        _holds.Add(hold);

        var ledgerEntry = new TransactionLedger(
            Id,
            -amount,
            TransactionType.HoldCreated,
            orderId.ToString(),
            $"Hold created for order {orderId}",
            Balance);

        _ledgerEntries.Add(ledgerEntry);
        return hold;
    }

    public void CaptureHold(Guid holdId)
    {
        var hold = _holds.FirstOrDefault(h => h.Id == holdId)
            ?? throw new HoldNotFoundException(holdId);

        hold.Capture();

        Balance -= hold.Amount;
        HeldBalance -= hold.Amount;

        var ledgerEntry = new TransactionLedger(
            Id,
            -hold.Amount,
            TransactionType.HoldCaptured,
            hold.OrderId.ToString(),
            $"Captured hold for order {hold.OrderId}",
            Balance);

        _ledgerEntries.Add(ledgerEntry);
    }

    public void ReleaseHold(Guid holdId, string reason)
    {
        var hold = _holds.FirstOrDefault(h => h.Id == holdId)
            ?? throw new HoldNotFoundException(holdId);

        hold.Release();

        HeldBalance -= hold.Amount;

        var ledgerEntry = new TransactionLedger(
            Id,
            hold.Amount,
            TransactionType.HoldReleased,
            hold.OrderId.ToString(),
            string.IsNullOrWhiteSpace(reason) ? $"Released hold for order {hold.OrderId}" : reason,
            Balance);

        _ledgerEntries.Add(ledgerEntry);
    }
}
