using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;
using Payment.Api.Domain;
using Payment.Api.DTOs;

namespace Payment.Api.Services;

public sealed class PaymentService : IPaymentService
{
    private const int MaxConcurrencyRetries = 3;
    private readonly PaymentDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(PaymentDbContext dbContext, TimeProvider timeProvider, ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default)
    {
        var account = Account.Create(request.UserId, request.Currency);

        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Account {AccountId} created for user {UserId}", account.Id, account.UserId);

        return MapToResponse(account);
    }

    public async Task<AccountResponse?> GetAccountByIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        return account is null ? null : MapToResponse(account);
    }

    public async Task<AccountResponse?> GetAccountByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

        return account is null ? null : MapToResponse(account);
    }

    public async Task<AccountResponse> DepositAsync(Guid accountId, DepositRequest request, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var account = await _dbContext.Accounts
                .FirstOrDefaultAsync(a => a.Id == accountId, ct)
                ?? throw new AccountNotFoundException(accountId);

            var entry = account.Deposit(request.Amount, request.Description);
            _dbContext.LedgerEntries.Add(entry);

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Deposited {Amount} to account {AccountId}", request.Amount, account.Id);

            return MapToResponse(account);
        });
    }

    public async Task<HoldResponse> CreateHoldAsync(CreateHoldRequest request, CancellationToken ct = default)
    {
        var existingHold = await _dbContext.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdempotencyKey == request.IdempotencyKey, ct);

        if (existingHold is not null)
        {
            ValidateIdempotencyPayload(existingHold, request);

            _logger.LogWarning("Duplicate hold request with key {Key}, returning existing hold {HoldId}",
                request.IdempotencyKey, existingHold.Id);

            return MapToResponse(existingHold);
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

                    var account = await _dbContext.Accounts
                        .Include(a => a.Holds)
                        .FirstOrDefaultAsync(a => a.Id == request.AccountId, ct)
                        ?? throw new AccountNotFoundException(request.AccountId);

                    var (hold, entry) = account.CreateHold(request.OrderId, request.Amount, request.Currency, request.IdempotencyKey);
                    _dbContext.Holds.Add(hold);
                    _dbContext.LedgerEntries.Add(entry);

                    await _dbContext.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);

                    _logger.LogInformation("Created hold {HoldId} for order {OrderId} on account {AccountId}",
                        hold.Id, hold.OrderId, account.Id);

                    return MapToResponse(hold);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict on hold creation for account {AccountId}, attempt {Attempt} of {MaxRetries}",
                        request.AccountId, attempt, MaxConcurrencyRetries);

                    _dbContext.ChangeTracker.Clear();

                    var freshAccount = await _dbContext.Accounts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Id == request.AccountId, ct);

                    if (freshAccount is not null && freshAccount.AvailableBalance < request.Amount)
                    {
                        throw new InsufficientFundsException(request.Amount, freshAccount.AvailableBalance, freshAccount.Currency);
                    }

                    if (attempt == MaxConcurrencyRetries)
                    {
                        throw;
                    }
                }
                catch (DbUpdateException)
                {
                    var concurrentHold = await _dbContext.Holds
                        .AsNoTracking()
                        .FirstOrDefaultAsync(h => h.IdempotencyKey == request.IdempotencyKey, ct);

                    if (concurrentHold is not null)
                    {
                        ValidateIdempotencyPayload(concurrentHold, request);
                        return MapToResponse(concurrentHold);
                    }

                    throw;
                }
            }

            throw new InvalidOperationException("Unreachable code after concurrency retry loop.");
        });
    }

    public async Task<HoldResponse> CaptureHoldAsync(CaptureHoldRequest request, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var hold = await _dbContext.Holds
                .FirstOrDefaultAsync(h => h.Id == request.HoldId, ct)
                ?? throw new HoldNotFoundException(request.HoldId);

            var account = await _dbContext.Accounts
                .Include(a => a.Holds)
                .FirstOrDefaultAsync(a => a.Id == hold.AccountId, ct)
                ?? throw new AccountNotFoundException(hold.AccountId);

            var entry = account.CaptureHold(hold.Id);
            _dbContext.LedgerEntries.Add(entry);

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Captured hold {HoldId} for order {OrderId}", hold.Id, hold.OrderId);

            return MapToResponse(hold);
        });
    }

    public async Task<HoldResponse> ReleaseHoldAsync(ReleaseHoldRequest request, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var hold = await _dbContext.Holds
                .FirstOrDefaultAsync(h => h.Id == request.HoldId, ct)
                ?? throw new HoldNotFoundException(request.HoldId);

            var account = await _dbContext.Accounts
                .Include(a => a.Holds)
                .FirstOrDefaultAsync(a => a.Id == hold.AccountId, ct)
                ?? throw new AccountNotFoundException(hold.AccountId);

            var entry = account.ReleaseHold(hold.Id, request.Reason);
            _dbContext.LedgerEntries.Add(entry);

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Released hold {HoldId} for order {OrderId}", hold.Id, hold.OrderId);

            return MapToResponse(hold);
        });
    }

    public async Task<IReadOnlyList<LedgerEntryResponse>> GetLedgerAsync(Guid accountId, CancellationToken ct = default)
    {
        var exists = await _dbContext.Accounts.AnyAsync(a => a.Id == accountId, ct);
        if (!exists)
        {
            throw new AccountNotFoundException(accountId);
        }

        return await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new LedgerEntryResponse(
                e.Id,
                e.AccountId,
                e.Amount,
                e.Type,
                e.ReferenceId,
                e.Description,
                e.BalanceAfter,
                e.CreatedAt))
            .ToListAsync(ct);
    }

    private static void ValidateIdempotencyPayload(Hold existingHold, CreateHoldRequest request)
    {
        if (existingHold.OrderId != request.OrderId || existingHold.Amount != request.Amount)
        {
            throw new IdempotencyConflictException(request.IdempotencyKey);
        }
    }

    private static AccountResponse MapToResponse(Account a) =>
        new(a.Id, a.UserId, a.Currency, a.Balance, a.HeldBalance, a.AvailableBalance, a.CreatedAt);

    private static HoldResponse MapToResponse(Hold h) =>
        new(h.Id, h.AccountId, h.OrderId, h.Amount, h.Status, h.IdempotencyKey, h.CreatedAt, h.ResolvedAt);
}
