namespace Payment.Api.Domain;

public enum TransactionType
{
    Deposit = 1,
    HoldCreated = 2,
    HoldCaptured = 3,
    HoldReleased = 4,
    Refund = 5
}
