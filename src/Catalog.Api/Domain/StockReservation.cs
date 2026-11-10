namespace Catalog.Api.Domain;

public sealed class StockReservation
{
    private StockReservation()
    {
    }

    internal StockReservation(Guid orderId, Guid productId, int quantity)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        Status = ReservationStatus.Reserved;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public void Confirm()
    {
        if (Status != ReservationStatus.Reserved)
        {
            throw new InvalidReservationStateException(Id, Status);
        }

        Status = ReservationStatus.Confirmed;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        if (Status != ReservationStatus.Reserved)
        {
            throw new InvalidReservationStateException(Id, Status);
        }

        Status = ReservationStatus.Released;
        ResolvedAt = DateTime.UtcNow;
    }
}
