using Amazon.CDK.AWS.SQS;

namespace Cdk.SharedConstructs;

using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.SNS;

public class SnsTopicSource : ChannelSource
{
    public ITopic Topic { get; }

    public SnsTopicSource(ITopic topic)
    {
        this.Topic = topic;
    }

    /// <inheritdoc />
    public override string SourceArn => this.Topic.TopicArn;

    /// <inheritdoc />
    public override CfnPipe.PipeSourceParametersProperty SourceParameters { get; }
}