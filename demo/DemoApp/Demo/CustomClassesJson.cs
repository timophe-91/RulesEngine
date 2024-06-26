using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RulesEngine.Extensions;
using RulesEngine.Interfaces;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace DemoApp.Demo;

public class CustomClassesJson
{
    /// <summary>
    ///     When using custom classes, you can still serialize the workflows directly in the JSON format as before.
    /// </summary>
    public async Task Run()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Running {nameof(CustomClassesJson)}....");
        Console.ResetColor();
        var workflowJson = """
                           {
                               "WorkflowRulesToInject": null,
                               "Rules": [
                                   {
                                       "ThisIsAmazingRule": [
                                           {
                                               "Id": "CustomRule1",
                                               "RuleExpressionType": 0,
                                               "Expression": "input1.x > 10",
                                           },
                                           {
                                               "Id": "CustomRule2",
                                               "RuleExpressionType": 0,
                                               "Expression": "input1.x > 10",
                                           }
                                       ],
                                       "Id": "CustomRule",
                                       "Operator": "And",
                                       "RuleExpressionType": 0,
                                       "RandomProperty": "Whatever"
                                   }
                               ],
                               "WorkflowName": "CustomWorkflow",
                               "RuleExpressionType": 0,
                               "GlobalParams": null
                           }
                           """;

        var bre = new RulesEngine.RulesEngine([workflowJson], typeof(CustomWorkflow));

        var converter = new ExpandoObjectConverter();
        const string basicInfo = "{\"x\": 50}";
        var inputs = JsonConvert.DeserializeObject<ExpandoObject>(basicInfo, converter);

        var resultList = await bre.ExecuteAllRulesAsync("CustomWorkflow", inputs);

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

        var reTypedRule = resultList[0].ResultRule as CustomRule;
        Console.WriteLine($"Added Property from ResultTree still exists: {reTypedRule?.RandomProperty}");
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
