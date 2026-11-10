namespace Order.Api.Domain;

public abstract class OrderDomainException : Exception
{
    protected OrderDomainException(string message) : base(message)
    {
    }
}

public sealed class OrderNotFoundException : OrderDomainException
{
    public OrderNotFoundException(Guid orderId)
        : base($"Order with identifier '{orderId}' was not found.")
    {
    }
}

public sealed class InvalidOrderStateException : OrderDomainException
{
    public InvalidOrderStateException(Guid orderId, OrderStatus currentStatus, string targetAction)
        : base($"Cannot {targetAction} order '{orderId}' because it is in '{currentStatus}' status.")
    {
    }
}

public sealed class EmptyOrderException : OrderDomainException
{
    public EmptyOrderException()
        : base("An order must contain at least one item.")
    {
    }
}
