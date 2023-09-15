using Amazon.CDK;
using Constructs;

namespace CdkDay2023
{
    using System.Collections.Generic;

    using Amazon.CDK.AWS.DynamoDB;
    using Amazon.CDK.AWS.Events;
    using Amazon.CDK.AWS.Events.Targets;
    using Amazon.CDK.AWS.Lambda;
    using Amazon.CDK.AWS.Logs;
    using Amazon.CDK.AWS.SNS;
    using Amazon.CDK.AWS.SQS;
    using Amazon.CDK.AWS.StepFunctions;

    using Cdk.SharedConstructs;

    using SharedConstructs;

    using EventBus = Amazon.CDK.AWS.Events.EventBus;
    using LambdaFunction = Cdk.SharedConstructs.LambdaFunction;
    using LambdaFunctionProps = Cdk.SharedConstructs.LambdaFunctionProps;
    using LogGroupProps = Amazon.CDK.AWS.Logs.LogGroupProps;

    public class CdkDay2023Stack : Stack
    {
        internal CdkDay2023Stack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var orderPricingTopic = new Topic(
                this,
                "PriceOrder");

            var eventBus = new EventBus(
                this,
                "PricingEventBus");

            var queue = new Queue(
                this,
                "PricingResponseQueue");

            var supplierOneFunction = GenerateVendorPricingFunction("SupplierOne", queue);
            var supplierTwoFunction = GenerateVendorPricingFunction("SupplierTwo", queue);
            var supplierThreeFunction = GenerateVendorPricingFunction("SupplierThree", queue);
            
            var sourceTable = new Table(
                this,
                "SourceTable",
                new TableProps()
                {
                    BillingMode = BillingMode.PAY_PER_REQUEST,
                    PartitionKey = new Attribute()
                    {
                        Name = "PK",
                        Type = AttributeType.STRING
                    },
                    SortKey = new Attribute()
                    {
                        Name = "SK",
                        Type = AttributeType.STRING
                    },
                    Stream = StreamViewType.NEW_AND_OLD_IMAGES
                });

            new PointToPointChannel(this, "PricingRequested")
                .From(new DynamoDbSource(sourceTable))
                .WithMessageFilter(
                    "PriceOrder",
                    new Dictionary<string, string[]>(1)
                    {
                        { "dynamodb.NewImage.Type.S", new[] { "PricingRequested" } }
                    })
                .WithMessageTranslation(
                    "GenerateMessage",
                    new Dictionary<string, object>(2)
                    {
                        { "CorrelationId.$", "$.dynamodb.NewImage.SK.S" },
                        { "CustomerId.$", "$.dynamodb.NewImage.PK.S" },
                        { "LoanAmount.$", "$.dynamodb.NewImage.LoanAmount.S" },
                        { "ReturnAddress", queue.QueueUrl }
                    })
                .To(new SnsTarget(orderPricingTopic));

            var recipientList = new[] { supplierOneFunction, supplierTwoFunction, supplierThreeFunction };

            new ScatterGather(
                    this,
                    "PricingCollector")
                .BroadcastOn(new SnsTopicSource(orderPricingTopic))
                .WithRecipientList(recipientList)
                .Build();
            
            new Aggregator(this, "PricingAggregator", AggregationResponseType.FIRST_BEST)
                .ResponseChannel(new SqsResponseChannel(queue))
                .ExpectedResponses(recipientList.Length)
                .PersistStateIn(sourceTable, JsonPath.StringAt("$.message.Request.CustomerId"), "$.message.Request.CorrelationId", new Dictionary<string, string>()
                {
                    {"PK", "$.message.Request.CustomerId"},
                    {"SK", "States.Format('RESULT#{}#SUPPLIER#{}', $.message.Request.CorrelationId, $.message.VendorName)"},
                    {"Type", "PriceReceived"},
                    {"Supplier", "$.message.VendorName"},
                    {"InterestRate", "States.Format('{}', $.message.InterestRate)"},
                })
                .PublishCompletionEventTo(new EventBusCompletionTarget(this, eventBus, "com.vendors.quote-generator", "com.vendors.quote-completed", "$.message.Request.CorrelationId"))
                .Build();

            var logGroup = new LogGroup(
                this,
                "LogResultsLogGroup",
                new LogGroupProps
                {
                    LogGroupName ="/aws/events/PricingResultsLogGroup", 
                    RemovalPolicy = RemovalPolicy.DESTROY,
                    Retention = RetentionDays.ONE_DAY,
                });

            var logCompletionEventRule = new Rule(
                this,
                "LogCompletionEvents",
                new RuleProps()
                {
                    EventBus = eventBus,
                    EventPattern = new EventPattern()
                    {
                        Source = new[] { "com.vendors.quote-generator" },
                        DetailType = new[] { "com.vendors.quote-completed" }
                    },
                    Targets = new[] { new CloudWatchLogGroup(logGroup) }
                });
        }

        private IFunction GenerateVendorPricingFunction(string vendorName, IQueue responseQueue)
        {
            var supplierFunction = new LambdaFunction(this, $"{vendorName}PricingFunction", new LambdaFunctionProps("./application/VendorLoanQuoteGenerator")
            {
                Environment = new Dictionary<string, string>(1){{"VENDOR_NAME", vendorName}, {"QUEUE_URL", responseQueue.QueueUrl}},
                Handler = "VendorLoanQuoteGenerator::VendorLoanQuoteGenerator.Function::FunctionHandler"
            });

            responseQueue.GrantSendMessages(supplierFunction.Function);

            return supplierFunction.Function;
        }
    }
}
