namespace SharedConstructs;

using Amazon.CDK.AWS.Events;

public class EventBusResponseTarget : AggregationResponseTarget
{
    public string Source { get; }
    public string DetailType { get; }
    public IEventBus EventBus { get; }

    public EventBusResponseTarget(
        IEventBus eventBus,
        string source,
        string detailType)
    {
        this.Source = source;
        this.DetailType = detailType;
        this.EventBus = eventBus;
    }
}