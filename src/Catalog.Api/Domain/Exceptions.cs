namespace Catalog.Api.Domain;

public abstract class CatalogDomainException : Exception
{
    protected CatalogDomainException(string message) : base(message)
    {
    }
}

public sealed class ProductNotFoundException : CatalogDomainException
{
    public ProductNotFoundException(Guid productId)
        : base($"Product with identifier '{productId}' was not found.")
    {
    }
}

public sealed class InsufficientStockException : CatalogDomainException
{
    public InsufficientStockException(Guid productId, int requested, int available)
        : base($"Insufficient stock for product '{productId}'. Requested: {requested}, available: {available}.")
    {
    }
}

public sealed class InvalidReservationStateException : CatalogDomainException
{
    public InvalidReservationStateException(Guid reservationId, ReservationStatus currentStatus)
        : base($"Stock reservation '{reservationId}' cannot be modified because it is in '{currentStatus}' state.")
    {
    }
}

public sealed class FractionalCentsException : CatalogDomainException
{
    public FractionalCentsException(decimal price)
        : base($"Price '{price}' cannot have more than 2 decimal places.")
    {
    }
}
