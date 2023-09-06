using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK;
using Constructs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.CodeStarNotifications;

namespace EventbridgePipesSample;

public class PipeBuilder
{
    private List<PolicyStatement> _policies;
    private CfnPipe.PipeSourceParametersProperty _sourceParametersProperty;
    private CfnPipe.PipeTargetParametersProperty _targetEventBusParametersProperty;
    private CfnPipe.PipeTargetParametersProperty _targetStepFunctionParametersProperty;
    private CfnPipe.PipeEnrichmentParametersProperty _enrichmentParametersProperty;
    private string _source;
    private string _targetStepFunction;
    private string _targetEventBus;
    private string _enrichment;

    private Construct _scope;
    private string _name;
    private static string[] SQS_ACTIONS;
    private static string[] SF_ACTIONS;
    private static string[] EVENTBUS_ACTIONS;

    public PipeBuilder(Construct scope, string name)
    {
        this._scope = scope;
        this._name = name;
        _policies = new List<PolicyStatement>();
    }

    static PipeBuilder()
    {
        SQS_ACTIONS = new[] { "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes" };
        SF_ACTIONS = new[] { "states:StartExecution" };
        EVENTBUS_ACTIONS = new[] { "events:PutEvents" };
    }

    public PipeBuilder AddSqsSource(Queue queue, int batchSize, int batchWindowSizeInSeconds)
    {
        _source = queue.QueueArn;
        /*
         * Add ReceiveMessage, DeleteMessage, and GetQueueAttributes permissions and IAM policies
         * for the source
         */
        _policies.Add(new PolicyStatement(
            new PolicyStatementProps
            {
                Resources = new[] { queue.QueueArn },
                Actions = SQS_ACTIONS,
                Effect = Effect.ALLOW
            }));

        _sourceParametersProperty = new CfnPipe.PipeSourceParametersProperty()
        {
            SqsQueueParameters = new CfnPipe.PipeSourceSqsQueueParametersProperty()
            {
                BatchSize = batchSize,
                MaximumBatchingWindowInSeconds = batchWindowSizeInSeconds
            },
            FilterCriteria = new CfnPipe.FilterCriteriaProperty
            {
                //Add a filter that only processes SNS messages with eventType "SampleEvent"
                Filters = new[] { new CfnPipe.FilterProperty
                {
                    Pattern = "{ \"body\": { \"eventType\": [\"SampleEvent\"] }}"
                }}
            }
        };

        return this;
    }

    public PipeBuilder AddHttpEnrichment(string http)
    {
        var apiDestination = new ApiDestination(_scope, $"{_name}ApiDestination", new ApiDestinationProps()
        {
            HttpMethod = HttpMethod.GET,
            Endpoint = http,
            Connection = new Connection(_scope, "Connection", new ConnectionProps()
            {
                ConnectionName = "ApiConnection",
                Authorization = Authorization.ApiKey("test", new SecretValue("test"))
            })
        });

        _enrichment = apiDestination.ApiDestinationArn;

        _policies.Add(new PolicyStatement(
            new PolicyStatementProps
            {
                Resources = new[] { apiDestination.ApiDestinationArn },
                Actions = new[] { "events:InvokeApiDestination" },
                Effect = Effect.ALLOW
            }));

        _enrichmentParametersProperty = new CfnPipe.PipeEnrichmentParametersProperty()
        {
            HttpParameters = new CfnPipe.PipeEnrichmentHttpParametersProperty()
            {
            },
        };

        return this;
    }

    public PipeBuilder AddEventBusTarget(EventBus bus)
    {
        _targetEventBus = bus.EventBusArn;
        //Add PutEvents IAM policies and permissions for target
        _policies.Add(new PolicyStatement(
            new PolicyStatementProps
            {
                Resources = new[] { bus.EventBusArn },
                Actions = EVENTBUS_ACTIONS,
                Effect = Effect.ALLOW
            }));

        _targetEventBusParametersProperty = new CfnPipe.PipeTargetParametersProperty()
        {
            EventBridgeEventBusParameters = new CfnPipe.PipeTargetEventBridgeEventBusParametersProperty()
            {
                DetailType = "SampleEventTriggered",
                Source = "com.binaryheap.sample-source"
            },
            //Transforms how SQS will post data to the target Event Bus
            /*I might change the hardcoded InputTemplate to use StringBuilder in the future, but this doesn't seem like a 
            scenario where it is necessary. Also, underscores do not work for JSONs*/
            InputTemplate = "{\"metaBody\": {\"correlationId\": <$.messageId>}, " +
                            "\"messageBody\": {" +
                                "\"MovieTitle\": <$.body.MovieTitle>, " +
                                "\"CustomerID\": <$.body.CustomerID>, " +
                                "\"Date\": <$.body.Date>}" +
                            "}"
        };
        return this;
    }

    public PipeBuilder AddStepFunctionTarget(StateMachine stepFunction)
    {
        _targetStepFunction = stepFunction.StateMachineArn;
        //Add StartExecution IAM policies and permissions for target
        _policies.Add(new PolicyStatement(
            new PolicyStatementProps
            {
                Resources = new[] { stepFunction.StateMachineArn },
                Actions = SF_ACTIONS,
                Effect = Effect.ALLOW
            }));

        _targetStepFunctionParametersProperty = new CfnPipe.PipeTargetParametersProperty()
        {
            StepFunctionStateMachineParameters = new CfnPipe.PipeTargetStateMachineParametersProperty()
            {
                InvocationType = "FIRE_AND_FORGET"
            }
        };

        return this;
    }

    public CfnPipe Build()
    {
        var pipesPolicy = new PolicyDocument(
            new PolicyDocumentProps
            {
                Statements = _policies.ToArray()
            });
        //Create a new role for Eventbridge Pipes
        var pipeRole = new Role(_scope, $"{_name}PipeRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("pipes.amazonaws.com"),
            InlinePolicies = new Dictionary<string, PolicyDocument>(2)
            {
                {"Policy", pipesPolicy},
            }
        });

        var pipe = new CfnPipe(_scope, $"{_name}MyNewPipe", new CfnPipeProps()
        {
            RoleArn = pipeRole.RoleArn,
            Source = _source,
            SourceParameters = _sourceParametersProperty,
            //Target = _targetStepFunction,
            //TargetParameters = _targetStepFunctionParametersProperty,
            Target = _targetEventBus,
            TargetParameters = _targetEventBusParametersProperty
        });

        if (!string.IsNullOrEmpty(_enrichment))
        {
            pipe.Enrichment = _enrichment;
            pipe.EnrichmentParameters = _enrichmentParametersProperty;
        }

        return pipe;
    }
}
