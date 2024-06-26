using Newtonsoft.Json;
using RulesEngine.Extensions;
using RulesEngine.Interfaces;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace DemoApp.Demo;

public class CustomClasses
{
    public async Task Run()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Running {nameof(Basic)}....");
        Console.ResetColor();
        var workflows = new List<IWorkflow>();
        var workflow = new CustomWorkflow { WorkflowName = "Test Workflow Rule 1" };

        var rules = new List<CustomRule>();

        var rule = new CustomRule {
            RuleName = "Test Rule",
            SuccessEvent = "Count is within tolerance.",
            ErrorMessage = "Over expected.",
            Expression = "count < 3",
            RuleExpressionType = RuleExpressionType.LambdaExpression
        };

        rules.Add(rule);

        workflow.Rules = rules;

        workflows.Add(workflow);

        var bre = new RulesEngine.RulesEngine(workflows.ToArray());

        dynamic datas = new ExpandoObject();
        datas.count = 1;
        var inputs = new[] { datas };

        var resultList = await bre.ExecuteAllRulesAsync("Test Workflow Rule 1", inputs);

        bool outcome;

        //Different ways to show test results:
        outcome = resultList.TrueForAll(r => r.IsSuccess);

        resultList.OnSuccess(eventName => {
            Console.WriteLine($"Result '{eventName}' is as expected.");
            outcome = true;
        });

        resultList.OnFail(() => {
            outcome = false;
        });

        Console.WriteLine($"Test outcome: {outcome}.");
    }

    /// <summary>
    ///     Class to implement a custom rule version
    /// </summary>
    public class CustomRule : IRule
    {
        /// <summary>
        ///     This is how the nested rules could be defined.
        /// </summary>
        public IEnumerable<CustomRule> ThisIsAmazingRule { get; set; }

        /// <summary>
        ///     Add needed properties as usual if needed
        /// </summary>
        public string RandomProperty { get; set; } = "RandomProperty";

        /// <summary>
        ///     Rule name for the Rule, should be unique within the workflow.
        ///     With tag to save it as ID.
        /// </summary>
        [JsonProperty("Id")]
        public string RuleName { get; set; }

        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        ///     The Operator to be used to combine the <see cref="ThisIsAmazingRule" /> or nested rules.
        /// </summary>
        public string Operator { get; set; }

        public string ErrorMessage { get; set; }
        public bool Enabled { get; set; } = true;
        public RuleExpressionType RuleExpressionType { get; set; }
        public IEnumerable<string> WorkflowsToInject { get; set; }

        /// <summary>
        ///     This is how the nested rules could be defined.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IRule> GetNestedRules()
        {
            return ThisIsAmazingRule;
        }

        /// <summary>
        ///     Set the rules in the correct format.
        ///     Sometimes it's good to use OfType to get the correct type of rule.
        /// </summary>
        /// <param name="rules"></param>
        public void SetRules(IEnumerable<IRule> rules)
        {
            ThisIsAmazingRule = rules.OfType<CustomRule>().ToArray();
        }

        public IEnumerable<ScopedParam> LocalParams { get; set; }
        public string Expression { get; set; }
        public RuleActions Actions { get; set; }
        public string SuccessEvent { get; set; }
    }

    public class CustomWorkflow : IWorkflow
    {
        public IEnumerable<CustomRule> Rules { get; set; }
        public string WorkflowName { get; set; }
        public IEnumerable<string> WorkflowsToInject { get; set; }
        public RuleExpressionType RuleExpressionType { get; set; }
        public IEnumerable<ScopedParam> GlobalParams { get; set; }

        public IEnumerable<IRule> GetRules()
        {
            return Rules;
        }

        public void SetRules(IEnumerable<IRule> rules)
        {
            Rules = rules.OfType<CustomRule>().ToArray();
        }
    }
}