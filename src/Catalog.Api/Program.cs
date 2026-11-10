using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Infrastructure;
using Catalog.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' not configured.");

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ICatalogService, CatalogService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

var products = app.MapGroup("/api/products").WithTags("Products");

products.MapPost("/", async (CreateProductRequest request, ICatalogService service, CancellationToken ct) =>
{
    var created = await service.CreateProductAsync(request, ct);
    return Results.Created($"/api/products/{created.Id}", created);
});

products.MapGet("/", async (int? skip, int? take, ICatalogService service, CancellationToken ct) =>
{
    var list = await service.GetProductsAsync(skip ?? 0, take ?? 50, ct);
    return Results.Ok(list);
});

products.MapGet("/{id:guid}", async (Guid id, ICatalogService service, CancellationToken ct) =>
{
    var product = await service.GetProductByIdAsync(id, ct);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

products.MapPost("/{id:guid}/stock", async (Guid id, AddStockRequest request, ICatalogService service, CancellationToken ct) =>
{
    var updated = await service.AddStockAsync(id, request, ct);
    return Results.Ok(updated);
});

var stock = app.MapGroup("/api/stock").WithTags("Stock");

stock.MapPost("/reserve", async (ReserveStockRequest request, ICatalogService service, CancellationToken ct) =>
{
    var response = await service.ReserveStockAsync(request, ct);
    return Results.Ok(response);
});

stock.MapPost("/confirm", async (ConfirmStockRequest request, ICatalogService service, CancellationToken ct) =>
{
    var response = await service.ConfirmStockAsync(request, ct);
    return Results.Ok(response);
});

stock.MapPost("/release", async (ReleaseStockRequest request, ICatalogService service, CancellationToken ct) =>
{
    var response = await service.ReleaseStockAsync(request, ct);
    return Results.Ok(response);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Catalog.Api" }));

app.Run();
