namespace SharedConstructs;

using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;

using Constructs;

public enum AggregationResponseType
{
    WAIT_FOR_ALL,
    TIME_OUT,
    FIRST_BEST,
}

public class Aggregator : Construct
{
    private readonly Construct scope;
    private readonly string id;
    private readonly AggregationResponseType aggregationResponseType;
    private AggregationResponseTarget target;
    private ITable tableTarget;
    private readonly Dictionary<string, DynamoAttributeValue> targetItem;
    private double expectedResponses;
    private string partition;
    private AggregationCompleteTarget completeTarget;
    private string correlationIdPath;

    public Aggregator(
        Construct scope,
        string id,
        AggregationResponseType aggregationResponseType) : base(
        scope,
        id)
    {
        this.scope = scope;
        this.id = id;
        this.aggregationResponseType = aggregationResponseType;
        this.targetItem = new Dictionary<string, DynamoAttributeValue>();
    }

    public Aggregator ExpectedResponses(double expectedResponses)
    {
        this.expectedResponses = expectedResponses;

        return this;
    }

    public Aggregator ResponseChannel(AggregationResponseTarget target)
    {
        this.target = target;

        return this;
    }

    public Aggregator PersistStateIn(
        ITable table,
        string partition,
        string correlationIdPath,
        Dictionary<string, string> targetItem)
    {
        this.tableTarget = table;

        foreach (var item in targetItem)
        {
            if (item.Value.StartsWith("$") || item.Value.StartsWith("States."))
            {
                this.targetItem.Add(
                    item.Key,
                    DynamoAttributeValue.FromString(JsonPath.StringAt(item.Value)));
            }
            else
            {
                this.targetItem.Add(
                    item.Key,
                    DynamoAttributeValue.FromString(item.Value));
            }
        }

        this.partition = partition;
        this.correlationIdPath = correlationIdPath;
        return this;
    }

    public Aggregator PublishCompletionEventTo(AggregationCompleteTarget completeTarget)
    {
        this.completeTarget = completeTarget;

        return this;
    }

    public void Build()
    {
        Chain? chain = null;

        switch (this.aggregationResponseType)
        {
            case AggregationResponseType.WAIT_FOR_ALL:
                chain = Chain.Start(
                    new Pass(
                            this,
                            "FormatMessageAsJson",
                            new PassProps
                            {
                                Parameters = new Dictionary<string, object>(1)
                                {
                                    { "message.$", "States.StringToJson($.body)" }
                                }
                            })
                        .Next(
                            new DynamoPutItem(
                                this,
                                "StoreInput",
                                new DynamoPutItemProps
                                {
                                    Table = this.tableTarget,
                                    Item = this.targetItem,
                                    ResultPath = JsonPath.DISCARD
                                }))
                        .Next(
                            new CallAwsService(
                                    this.scope,
                                    "QueryStockItems",
                                    new CallAwsServiceProps
                                    {
                                        Action = "query",
                                        IamResources = new[]
                                        {
                                            this.tableTarget.TableArn
                                        },
                                        Service = "dynamodb",
                                        Parameters = new Dictionary<string, object>
                                        {
                                            { "TableName", this.tableTarget.TableName },
                                            { "KeyConditionExpression", "PK = :pk and begins_with(SK, :sk)" },
                                            {
                                                "ExpressionAttributeValues", new Dictionary<string, object>
                                                {
                                                    {
                                                        ":pk", new Dictionary<string, object>
                                                        {
                                                            { "S", this.partition }
                                                        }
                                                    },
                                                    {
                                                        ":sk", new Dictionary<string, object>
                                                        {
                                                            { "S", JsonPath.StringAt($"States.Format('RESULT#{{}}', {this.correlationIdPath})") }
                                                        }
                                                    }
                                                }
                                            }
                                        },
                                        ResultPath = "$.queryResult"
                                    })
                                .Next(
                                    new Choice(
                                            this,
                                            "CheckResultCount",
                                            new ChoiceProps())
                                        .When(
                                            Condition.NumberEquals(
                                                "$.queryResult.Count",
                                                this.expectedResponses),
                                            this.completeTarget.Target)
                                        .Otherwise(
                                            new Succeed(
                                                this,
                                                "Success")))));

                break;
            case AggregationResponseType.FIRST_BEST:
                chain = Chain.Start(
                    new Pass(
                            this,
                            "FormatMessageAsJson",
                            new PassProps
                            {
                                Parameters = new Dictionary<string, object>(1)
                                {
                                    { "message.$", "States.StringToJson($.body)" }
                                }
                            })
                        .Next(
                            new CallAwsService(
                                this.scope,
                                "QueryStockItems",
                                new CallAwsServiceProps
                                {
                                    Action = "query",
                                    IamResources = new[]
                                    {
                                        this.tableTarget.TableArn
                                    },
                                    Service = "dynamodb",
                                    Parameters = new Dictionary<string, object>
                                    {
                                        { "TableName", this.tableTarget.TableName },
                                        { "KeyConditionExpression", "PK = :pk and begins_with(SK, :sk)" },
                                        {
                                            "ExpressionAttributeValues", new Dictionary<string, object>
                                            {
                                                {
                                                    ":pk", new Dictionary<string, object>
                                                    {
                                                        { "S", this.partition }
                                                    }
                                                },
                                                {
                                                    ":sk", new Dictionary<string, object>
                                                    {
                                                        { "S", JsonPath.StringAt($"States.Format('RESULT#{{}}', {this.correlationIdPath})") }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                    ResultPath = "$.queryResult"
                                }))
                        .Next(
                            new Choice(
                                    this,
                                    "CheckResultCount",
                                    new ChoiceProps())
                                .When(
                                    Condition.NumberEquals(
                                        "$.queryResult.Count",
                                        0),
                                    new DynamoPutItem(
                                        this,
                                        "StoreInput",
                                        new DynamoPutItemProps
                                        {
                                            Table = this.tableTarget,
                                            Item = this.targetItem,
                                            ResultPath = JsonPath.DISCARD
                                        }).Next(this.completeTarget.Target))
                                .Otherwise(
                                    new Succeed(
                                        this,
                                        "Success"))));

                break;
        }

        var gatherResponses = new SqsSourcedWorkflow(
            this.scope,
            $"{this.id}GatherResponses",
            chain);

        this.tableTarget.GrantReadWriteData(gatherResponses.Workflow);

        switch (this.target.GetType().Name)
        {
            case nameof(EventBusResponseTarget):
                var responseTarget = this.target as EventBusResponseTarget;

                var rule = new Rule(
                    this,
                    $"{this.id}AggregationRule",
                    new RuleProps
                    {
                        EventBus = responseTarget.EventBus,
                        EventPattern = new EventPattern
                        {
                            Source = new[] { responseTarget.Source },
                            DetailType = new[] { responseTarget.DetailType }
                        },
                        Targets = new[] { new SfnStateMachine(gatherResponses.Workflow) }
                    });
                break;
            case nameof(SqsResponseChannel):
                var sqsResponseTarget = this.target as SqsResponseChannel;

                var pipeRole = new Role(
                    this,
                    $"{this.id}ResponseProcessorRole",
                    new RoleProps
                    {
                        AssumedBy = new ServicePrincipal("pipes.amazonaws.com")
                    });

                sqsResponseTarget.Queue.GrantConsumeMessages(pipeRole);
                gatherResponses.Workflow.GrantStartExecution(pipeRole);
                gatherResponses.Workflow.GrantStartSyncExecution(pipeRole);

                var pipe = new CfnPipe(
                    this,
                    $"{this.id}ResponseProcessor",
                    new CfnPipeProps
                    {
                        RoleArn = pipeRole.RoleArn,
                        Name = $"{this.id}ResponseProcessor",
                        Source = sqsResponseTarget.Queue.QueueArn,
                        SourceParameters = new CfnPipe.PipeSourceParametersProperty
                        {
                            SqsQueueParameters = new CfnPipe.PipeSourceSqsQueueParametersProperty
                            {
                                BatchSize = 1
                            }
                        },
                        Target = gatherResponses.Workflow.StateMachineArn,
                        TargetParameters = new CfnPipe.PipeTargetParametersProperty
                        {
                            StepFunctionStateMachineParameters = new CfnPipe.PipeTargetStateMachineParametersProperty
                            {
                                InvocationType = "REQUEST_RESPONSE"
                            }
                        }
                    });
                break;
        }
    }
}