namespace SharedConstructs;

using Amazon.CDK.AWS.SQS;

public class SqsResponseChannel : AggregationResponseTarget
{
    public IQueue Queue { get; }

    public SqsResponseChannel(
        IQueue queue)
    {
        this.Queue = queue;
    }
}