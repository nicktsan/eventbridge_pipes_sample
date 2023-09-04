using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Pipes;
using Amazon.CDK.AWS.StepFunctions;
using Constructs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EventbridgePipesSample
{
    public class StateMachineBuilder
    {
        private Construct _scope;
        private string _name;

        public StateMachineBuilder(Construct scope, string name)
        {
            this._scope = scope;
            this._name = name;
        }

        public StateMachineBuilder BuildStateMachine()
        {
            //Build the CloudWatch LogGroup
            var stepFunctionlogGroup = new LogGroup(_scope, "StepFunctionCloudWatchLogs", 
                new LogGroupProps
                {
                    LogGroupName = "/aws/vendedlogs/states/sample-state-machine",
                }
            );
            //Create a role for the state machine
            var stateMachineRole = new Role(_scope, $"{_name}StateMachineRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("states.us-east-1.amazonaws.com"),
            });

            //Create a flow for the stateMachine
            var flow = new Succeed(_scope, "DefaultSucceed");

            //Create a step function for the event bus to target
            var targetStepFunction = new StateMachine(scope: _scope, id: "StateMachineConstruct", new StateMachineProps()
            {
                Role = stateMachineRole,
                StateMachineName = "EventBridgeTargetStateMachine",
                DefinitionBody = DefinitionBody.FromChainable(flow),
                StateMachineType = StateMachineType.EXPRESS,
                Timeout = Duration.Seconds(30),
                Logs = new LogOptions
                {
                    Destination = stepFunctionlogGroup,
                    Level = LogLevel.ALL,
                    IncludeExecutionData = true
                }
            });
            return this;
        }
    }
}
