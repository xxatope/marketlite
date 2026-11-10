using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;
using Payment.Api.DTOs;
using Payment.Api.Infrastructure;
using Payment.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("PaymentDb")
    ?? throw new InvalidOperationException("Connection string 'PaymentDb' not configured.");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IPaymentService, PaymentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

var accounts = app.MapGroup("/api/accounts").WithTags("Accounts");

accounts.MapPost("/", async (CreateAccountRequest request, IPaymentService service, CancellationToken ct) =>
{
    var created = await service.CreateAccountAsync(request, ct);
    return Results.Created($"/api/accounts/{created.Id}", created);
});

accounts.MapGet("/{id:guid}", async (Guid id, IPaymentService service, CancellationToken ct) =>
{
    var account = await service.GetAccountByIdAsync(id, ct);
    return account is not null ? Results.Ok(account) : Results.NotFound();
});

accounts.MapGet("/user/{userId:guid}", async (Guid userId, IPaymentService service, CancellationToken ct) =>
{
    var account = await service.GetAccountByUserIdAsync(userId, ct);
    return account is not null ? Results.Ok(account) : Results.NotFound();
});

accounts.MapPost("/{id:guid}/deposit", async (Guid id, DepositRequest request, IPaymentService service, CancellationToken ct) =>
{
    var updated = await service.DepositAsync(id, request, ct);
    return Results.Ok(updated);
});

accounts.MapGet("/{id:guid}/ledger", async (Guid id, IPaymentService service, CancellationToken ct) =>
{
    var ledger = await service.GetLedgerAsync(id, ct);
    return Results.Ok(ledger);
});

var payments = app.MapGroup("/api/payments").WithTags("Payments");

payments.MapPost("/holds", async (CreateHoldRequest request, IPaymentService service, CancellationToken ct) =>
{
    var hold = await service.CreateHoldAsync(request, ct);
    return Results.Ok(hold);
});

payments.MapPost("/holds/capture", async (CaptureHoldRequest request, IPaymentService service, CancellationToken ct) =>
{
    var hold = await service.CaptureHoldAsync(request, ct);
    return Results.Ok(hold);
});

payments.MapPost("/holds/release", async (ReleaseHoldRequest request, IPaymentService service, CancellationToken ct) =>
{
    var hold = await service.ReleaseHoldAsync(request, ct);
    return Results.Ok(hold);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Payment.Api" }));

app.Run();
