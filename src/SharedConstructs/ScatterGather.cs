namespace SharedConstructs;

using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.SNS.Subscriptions;

using Cdk.SharedConstructs;

using Constructs;

public class ScatterGather : Construct
{
    private ChannelSource source;
    private IFunction[] recipients;
    
    /// <inheritdoc />
    public ScatterGather(Construct scope, string id) : base(scope,
        id)
    {
    }

    public ScatterGather BroadcastOn(ChannelSource source)
    {
        this.source = source;

        return this;
    }

    public ScatterGather WithRecipientList(IFunction[] recipients)
    {
        this.recipients = recipients;
        return this;
    }

    public void Build()
    {
        switch (this.source.GetType().Name)
        {
            case nameof(SnsTopicSource):
                var snsSource = this.source as SnsTopicSource;
                
                foreach (var recipient in this.recipients)
                {
                    snsSource.Topic.AddSubscription(new LambdaSubscription(recipient));
                }

                break;
        }
    }
}