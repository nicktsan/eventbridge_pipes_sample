//Integrates SQS and StepFunctions with Eventbridge Pipes
using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
using Constructs;
using System.Collections.Generic;

namespace EventbridgePipesSample
{
    public class EventbridgePipesSampleStack : Stack
    {
        internal EventbridgePipesSampleStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //Create a new SQS Queue
            var queue = new Queue(scope:this, id: "SourceSqsQueue");

            //Messages passed to the StateMachine will be a batch. This map will handle that
            var mapState = new Map(scope: this, id:"BaseMapState", new MapProps()
            {
                ItemsPath = JsonPath.EntirePayload
            });
            //Iterates over the batch of messages with 5 second wait time
            mapState.Iterator(new Wait(scope: this, id: "5SecondWait", new WaitProps()
            {
                Time = WaitTime.Duration(Duration.Seconds(5))
            }));

            //Create a target step function
            var targetStepFunction = new StateMachine(scope: this, id: "TargetStateMachine", new StateMachineProps()
            {
                StateMachineName = "PipesTargetStateMachine",
                DefinitionBody = DefinitionBody.FromChainable(mapState)
            });
            /*
             * Create a new pipe. The Cloudformation documentation details which properties need to
             * be set. We set the source and targets for the pipe. Optional enrichment can be
             * added but is not needed.
             */
            var pipe = new PipeBuilder(this, "SqsToStepFunctionsPipe")
                .AddSqsSource(queue, 1, 0)
                .AddStepFunctionTarget(targetStepFunction)
                .Build();
        }
    }
}
