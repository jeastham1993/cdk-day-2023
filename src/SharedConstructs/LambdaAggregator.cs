namespace SharedConstructs;

using Amazon.CDK.AWS.Lambda;

public class LambdaAggregator : Aggregation
{
    public IFunction Function { get; }

    public LambdaAggregator(IFunction function)
    {
        this.Function = function;
    }

    /// <inheritdoc />
    public override string AggregationArn => this.Function.FunctionArn;
}