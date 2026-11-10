namespace Shared.Contracts.Events;

public record PaymentHoldRequestedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);

public record PaymentAuthorizedEvent(
    Guid OrderId,
    Guid PaymentId,
    Guid AccountId,
    decimal Amount,
    string Currency,
    DateTime AuthorizedAt);

public record PaymentAuthorizationFailedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Reason,
    DateTime FailedAt);

public record PaymentCapturedEvent(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    DateTime CapturedAt);

public record PaymentReleasedEvent(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string Reason,
    DateTime ReleasedAt);
