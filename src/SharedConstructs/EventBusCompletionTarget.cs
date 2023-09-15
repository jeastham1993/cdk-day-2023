namespace SharedConstructs;

using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;

using Constructs;

public class EventBusCompletionTarget : AggregationCompleteTarget
{
    private readonly Construct scope;
    private readonly string correlationIdPath;
    public string Source { get; }
    public string DetailType { get; }
    public IEventBus EventBus { get; }

    public EventBusCompletionTarget(
        Construct scope,
        IEventBus eventBus,
        string source,
        string detailType,
        string correlationIdPath)
    {
        this.scope = scope;
        this.correlationIdPath = correlationIdPath;
        this.Source = source;
        this.DetailType = detailType;
        this.EventBus = eventBus;
    }

    /// <inheritdoc />
    public override IChainable Target => new EventBridgePutEvents(
        this.scope, 
        "PublishCompletionEvent",
        new EventBridgePutEventsProps()
        {
            Entries = new[]
            {
                new EventBridgePutEventsEntry()
                {
                    EventBus = this.EventBus,
                    Source = this.Source,
                    DetailType = this.DetailType,
                    Detail = TaskInput.FromObject(new Dictionary<string, object>(1)
                    {
                        {"CorrelationId", JsonPath.StringAt(this.correlationIdPath)}
                    })
                }
            }
        });
}