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
             Creates a new IAM policy with permissions for receiving messages, deleting messages,
             and getting attributes from SQS Queue.
             */
            var sourcePolicy = new PolicyDocument(
                new PolicyDocumentProps
                {
                    Statements = new[]
                    {
                        new PolicyStatement
                        (
                            new PolicyStatementProps
                            {
                                Resources = new[] { queue.QueueArn },
                                Actions = new[] { "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes" },
                                Effect = Effect.ALLOW
                            }
                        )
                    }
                }
            );

            /*
             * Creates new policy to allow starting the execution of the state machine,
             * essentially allowing for Eventbridge Pipes to start the execution of the workflow.
             */
            var targetPolicy = new PolicyDocument(
                new PolicyDocumentProps
                {
                    Statements = new[]
                    {
                        new PolicyStatement
                        (
                            new PolicyStatementProps
                            {
                                Resources = new[] { targetStepFunction.StateMachineArn },
                                Actions = new[] { "states:StartExecution" },
                                Effect = Effect.ALLOW
                            }
                        )
                    }
                }
            );

            //Create a new role to use Eventbridge Pipes
            var pipeRole = new Role
            (
                scope:this,
                id: "PipeRole",
                new RoleProps
                {
                    AssumedBy = new ServicePrincipal("pipes.amazonaws.com"),
                    InlinePolicies = new Dictionary<string, PolicyDocument>(capacity:2)
                    {
                        { "SourcePolicy", sourcePolicy },
                        { "TargetPolicy", targetPolicy }
                    } 
                }
            );

            /*
             * Create a new pipe. The Cloudformation documentation details which properties need to
             * be set. We set the source and targets for the pipe. Optional enrichment can be
             * added but is not needed.
             */
            var pipe = new CfnPipe(scope: this, "MyNewPipe", new CfnPipeProps()
            {
                RoleArn = pipeRole.RoleArn,
                Source = queue.QueueArn,
                SourceParameters = new CfnPipe.PipeSourceParametersProperty()
                {
                    SqsQueueParameters = new CfnPipe.PipeSourceSqsQueueParametersProperty() 
                    {
                        BatchSize = 5,
                        MaximumBatchingWindowInSeconds = 10
                    }
                },
                Target = targetStepFunction.StateMachineArn, 
                TargetParameters = new CfnPipe.PipeTargetParametersProperty()
                {
                    StepFunctionStateMachineParameters = new CfnPipe.PipeTargetStateMachineParametersProperty()
                    {
                        InvocationType = "FIRE_AND_FORGET"
                    }
                }
                //Optional Enrichment step can be included but is not needed.
            });
        }
    }
}
