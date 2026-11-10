namespace Payment.Api.Domain;

public abstract class PaymentDomainException : Exception
{
    protected PaymentDomainException(string message) : base(message)
    {
    }
}

public sealed class InsufficientFundsException : PaymentDomainException
{
    public InsufficientFundsException(decimal requested, decimal available, string currency)
        : base($"Insufficient funds. Requested: {requested} {currency}, available: {available} {currency}.")
    {
    }
}

public sealed class HoldNotFoundException : PaymentDomainException
{
    public HoldNotFoundException(Guid holdId)
        : base($"Hold with identifier '{holdId}' was not found.")
    {
    }
}

public sealed class InvalidHoldStateException : PaymentDomainException
{
    public InvalidHoldStateException(Guid holdId, HoldStatus currentStatus)
        : base($"Hold '{holdId}' cannot be processed because it is in '{currentStatus}' state.")
    {
    }
}

public sealed class AccountNotFoundException : PaymentDomainException
{
    public AccountNotFoundException(Guid accountId)
        : base($"Account with identifier '{accountId}' was not found.")
    {
    }
}
