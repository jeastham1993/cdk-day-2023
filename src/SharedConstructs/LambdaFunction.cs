using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Constructs;
using XaasKit.CDK.AWS.Lambda.DotNet;

namespace Cdk.SharedConstructs;

using Amazon.CDK;
using Amazon.CDK.AWS.Lambda.Destinations;
using Amazon.CDK.AWS.SQS;

public class LambdaFunctionProps : FunctionProps
{
    public LambdaFunctionProps(string codePath)
    {
        CodePath = codePath;
    }
    
    public string CodePath { get; set; }
}

public class LambdaFunction : Construct
{
    public Function Function { get; }

    public LambdaFunction(Construct scope, string id, LambdaFunctionProps props) : base(scope, id)
    {
        this.Function = new DotNetFunction(this, id, new DotNetFunctionProps()
        {
            Timeout = Duration.Seconds(15),
            FunctionName = id,
            Runtime = Runtime.DOTNET_6,
            MemorySize = props.MemorySize ?? 1024,
            LogRetention = RetentionDays.ONE_DAY,
            Handler = props.Handler,
            Environment = props.Environment,
            Tracing = Tracing.ACTIVE,
            ProjectDir = props.CodePath,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? Architecture.ARM_64 : Architecture.X86_64,
            OnFailure = new SqsDestination(new Queue(this, $"{id}FunctionDLQ")),
        });   
    }
}