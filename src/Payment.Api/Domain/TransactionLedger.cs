namespace Payment.Api.Domain;

public sealed class TransactionLedger
{
    private TransactionLedger()
    {
    }

    internal TransactionLedger(
        Guid accountId,
        decimal amount,
        TransactionType type,
        string? referenceId,
        string description,
        decimal balanceAfter)
    {
        Id = Guid.NewGuid();
        AccountId = accountId;
        Amount = amount;
        Type = type;
        ReferenceId = referenceId;
        Description = description;
        BalanceAfter = balanceAfter;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionType Type { get; private set; }
    public string? ReferenceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal BalanceAfter { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
