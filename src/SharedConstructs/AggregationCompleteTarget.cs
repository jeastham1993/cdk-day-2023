namespace SharedConstructs;

using Amazon.CDK.AWS.StepFunctions;

public abstract class AggregationCompleteTarget
{
    public abstract IChainable Target { get; }
}