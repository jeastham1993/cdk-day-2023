using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.XRay.Recorder.Core;
using Amazon.Lambda.CloudWatchEvents;
using System;
using VendorLoanQuoteGenerator.DataTransfer;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;

namespace VendorLoanQuoteGenerator
{
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.SNSEvents;
    using Amazon.SQS;

    public class Function
    {
        private readonly Random randomGenerator;
        private readonly AmazonSQSClient sqsClient;

        public Function() : this(null, null)
        {
        }

        internal Function(Random random, AmazonSQSClient sqsClient)
        {
            this.randomGenerator = random ?? new Random();
            this.sqsClient = sqsClient ?? new AmazonSQSClient();
        }

        [LambdaFunction]
        public async Task FunctionHandler(SNSEvent inputEvent, ILambdaContext context)
        {
            var artificialDelay = this.randomGenerator.Next(
                1,
                10);

            await Task.Delay(TimeSpan.FromSeconds(artificialDelay));
            
            foreach (var evt in inputEvent.Records)
            {
                var multiplier = this.randomGenerator.Next(50, 100) / 10.00;

                var request = JsonSerializer.Deserialize<GenerateLoanQuoteRequest>(evt.Sns.Message);

                var loanQuoteResult = new LoanQuoteResult
                {
                    Request = request,
                    InterestRate = multiplier,
                    VendorName = Environment.GetEnvironmentVariable("VENDOR_NAME")
                };

                await this.sqsClient.SendMessageAsync(request.ReturnAddress, JsonSerializer.Serialize(loanQuoteResult));   
            }
        }
    }
}
