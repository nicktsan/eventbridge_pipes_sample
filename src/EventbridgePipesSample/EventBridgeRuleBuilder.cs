using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Amazon.CDK.AWS.CodeStarNotifications;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.SES.Actions;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
using Constructs;
using eBus = Amazon.CDK.AWS.Events.EventBus;

namespace EventbridgePipesSample;
public class EventBridgeRuleBuilder
{
    private Construct _scope;
    private string _name;
    public EventBridgeRuleBuilder(Construct scope, string name)
    {
        this._scope = scope;
        this._name = name;
    }
    public EventBridgeRuleBuilder BuildEventBridge(eBus bus, StateMachine stateMachine)
    {
        //Create a new EventBridge Rule which filters based on DetailType
        var rule = new Rule(_scope, "MyEventRule", new RuleProps
        {
            EventPattern = new EventPattern
            {
                DetailType = new[]{ "SampleEventTriggered" }
            },
            RuleName = "sample-event-triggered-rule",
            EventBus = bus
        });

        //Create a role that the target can use to trigger the state machine
        var role = new Role(_scope, "SameEventTriggered-Role", new RoleProps{
            AssumedBy = new ServicePrincipal("events.amazonaws.com"),
        });
        
        //Create a new dead letter queue in case calling the state machine goes wrong. The message will end up in this queue.
        var dlq = new Queue(_scope, "SameEventTriggered-DLQ");
        

        rule.AddTarget(
            new SfnStateMachine(stateMachine, new SfnStateMachineProps{
                Input = RuleTargetInput.FromEventPath("$.detail"),
                DeadLetterQueue = dlq,
                Role = role,
            })
        );
        return this;
    }
}

