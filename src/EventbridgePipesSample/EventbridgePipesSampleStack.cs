//Integrates SNS, SQS, EventBridge, and StepFunctions with Eventbridge Pipes
//TODO: Add filtering and enrichment in pipe
using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;

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
                EventBusName = "MovieEventBus"
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
                .Build();

            //Create a statemachine using step functions that event bridge will target
            var stateMachine = new StateMachineBuilder(this, "EventBridgeToStepFunctions")
                .BuildStateMachine();

            //Create the EventBridge rule to trigger the state machine
            new EventBridgeRuleBuilder(this, "EventBridgeRuleBuilder")
                .BuildEventBridge(targetBus, stateMachine.getStateMachine());
        }
    }
}
