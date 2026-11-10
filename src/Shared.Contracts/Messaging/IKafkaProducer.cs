namespace Shared.Contracts.Messaging;

public interface IKafkaProducer
{
    Task ProduceAsync<T>(string topic, string key, T message, CancellationToken ct = default);
}
