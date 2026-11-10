using Payment.Api.DTOs;

namespace Payment.Api.Services;

public interface IPaymentService
{
    Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
    Task<AccountResponse?> GetAccountByIdAsync(Guid accountId, CancellationToken ct = default);
    Task<AccountResponse?> GetAccountByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<AccountResponse> DepositAsync(Guid accountId, DepositRequest request, CancellationToken ct = default);
    Task<HoldResponse> CreateHoldAsync(CreateHoldRequest request, CancellationToken ct = default);
    Task<HoldResponse> CaptureHoldAsync(CaptureHoldRequest request, CancellationToken ct = default);
    Task<HoldResponse> ReleaseHoldAsync(ReleaseHoldRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntryResponse>> GetLedgerAsync(Guid accountId, CancellationToken ct = default);
}
