using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Payment.Api.Data;
using Payment.Api.Domain;
using Payment.Api.DTOs;
using Payment.Api.Services;
using Testcontainers.PostgreSql;

namespace Payment.Tests;

public sealed class PostgreSqlConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private PaymentDbContext CreatePostgreSqlDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task ParallelHolds_OnRealPostgreSql_StrictlyPreventsOverdraft()
    {
        await using var initDb = CreatePostgreSqlDbContext();
        await initDb.Database.EnsureCreatedAsync();

        var service = new PaymentService(initDb, TimeProvider.System, NullLogger<PaymentService>.Instance);
        var account = await service.CreateAccountAsync(new CreateAccountRequest(Guid.NewGuid(), "RUB"));
        await service.DepositAsync(account.Id, new DepositRequest(3000m, "Initial deposit"));

        var totalConcurrentRequests = 10;
        var holdAmount = 1000m;

        var tasks = Enumerable.Range(0, totalConcurrentRequests).Select(async i =>
        {
            await using var taskDb = CreatePostgreSqlDbContext();
            var taskService = new PaymentService(taskDb, TimeProvider.System, NullLogger<PaymentService>.Instance);

            try
            {
                var hold = await taskService.CreateHoldAsync(new CreateHoldRequest(
                    account.Id,
                    Guid.NewGuid(),
                    holdAmount,
                    "RUB",
                    $"concurrent-hold-key-{i}"));

                return (Success: true, HoldId: hold.Id, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, HoldId: Guid.Empty, Error: ex);
            }
        });

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Assert.Equal(3, successCount);
        Assert.Equal(7, failureCount);

        foreach (var failure in results.Where(r => !r.Success))
        {
            Assert.IsType<InsufficientFundsException>(failure.Error);
        }

        await using var verifyDb = CreatePostgreSqlDbContext();
        var finalAccount = await verifyDb.Accounts.AsNoTracking().FirstAsync(a => a.Id == account.Id);
        var finalLedger = await verifyDb.LedgerEntries.AsNoTracking().Where(l => l.AccountId == account.Id).ToListAsync();

        Assert.Equal(3000m, finalAccount.Balance);
        Assert.Equal(3000m, finalAccount.HeldBalance);
        Assert.Equal(0m, finalAccount.AvailableBalance);
        Assert.Equal(finalAccount.Balance, finalLedger.Sum(l => l.Amount));
    }
}
