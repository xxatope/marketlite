namespace Shared.Contracts.Messaging;

public static class KafkaTopics
{
    public const string OrdersCreated = "orders.created";
    public const string OrdersConfirmed = "orders.confirmed";
    public const string OrdersCancelled = "orders.cancelled";
    public const string StockReserved = "stock.reserved";
    public const string StockReservationFailed = "stock.reservation-failed";
    public const string PaymentsAuthorized = "payments.authorized";
    public const string PaymentsFailed = "payments.failed";
}
