//Integrates SNS, SQS and StepFunctions with Eventbridge Pipes
//TODO: Add filtering and enrichment in pipe
using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
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
            //Create a topic for SNS
            var topic = new Topic(scope: this, id: "SourceTopic");

            //Create a new SQS Queue
            var queue = new Queue(scope: this, id: "SourceSqsQueue");

            //Subscribe Sqs to SNS. Set RawMessageDelivery to true to send just the message body.
            topic.AddSubscription(
                new SqsSubscription(queue, new SqsSubscriptionProps
                {
                    RawMessageDelivery = true
                })
            );

            //Create an event bus for eventbridge pipes to target
            var targetBus = new EventBus(this, "bus", new EventBusProps
            {
                EventBusName = "MyCustomEventBus"
            });

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

            //Create a step function for the event bus to target
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
            //var pipe = new PipeBuilder(this, "SqsToStepFunctionsPipe")
            var pipe = new PipeBuilder(this, "SqsToEventBusPipe")
                .AddSqsSource(queue, 1, 0)
                .AddEventBusTarget(targetBus)
                //.AddStepFunctionTarget(targetStepFunction)
                .Build();
        }
    }
}
