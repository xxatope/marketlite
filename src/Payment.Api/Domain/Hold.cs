namespace Payment.Api.Domain;

public sealed class Hold
{
    private Hold()
    {
    }

    internal Hold(Guid accountId, Guid orderId, decimal amount, string idempotencyKey)
    {
        Id = Guid.NewGuid();
        AccountId = accountId;
        OrderId = orderId;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
        Status = HoldStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public HoldStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    internal void Capture()
    {
        if (Status != HoldStatus.Active)
        {
            throw new InvalidHoldStateException(Id, Status);
        }

        Status = HoldStatus.Captured;
        ResolvedAt = DateTime.UtcNow;
    }

    internal void Release()
    {
        if (Status != HoldStatus.Active)
        {
            throw new InvalidHoldStateException(Id, Status);
        }

        Status = HoldStatus.Released;
        ResolvedAt = DateTime.UtcNow;
    }
}
