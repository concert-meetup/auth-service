using System.Text.Json.Serialization;

namespace auth.API.IntegrationEvents.Events;

public record IntegrationEvent
{
    public IntegrationEvent()
    {
        Id = Guid.NewGuid();
        Created = DateTime.Now;
    }
    [JsonInclude] public Guid Id { get; set; }
    [JsonInclude] public DateTime Created { get; set; }
}