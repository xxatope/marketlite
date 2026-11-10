using Payment.Api.Domain;

namespace Payment.Api.DTOs;

public sealed record CreateAccountRequest(Guid UserId, string Currency = "RUB");

public sealed record AccountResponse(
    Guid Id,
    Guid UserId,
    string Currency,
    decimal Balance,
    decimal HeldBalance,
    decimal AvailableBalance,
    DateTime CreatedAt);

public sealed record DepositRequest(decimal Amount, string Description);

public sealed record CreateHoldRequest(
    Guid AccountId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);

public sealed record HoldResponse(
    Guid Id,
    Guid AccountId,
    Guid OrderId,
    decimal Amount,
    HoldStatus Status,
    string IdempotencyKey,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public sealed record CaptureHoldRequest(Guid HoldId);

public sealed record ReleaseHoldRequest(Guid HoldId, string Reason);

public sealed record LedgerEntryResponse(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    TransactionType Type,
    string? ReferenceId,
    string Description,
    decimal BalanceAfter,
    DateTime CreatedAt);
