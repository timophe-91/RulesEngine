using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RulesEngine.Interfaces;
using RulesEngine.Models;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RulesEngine.UnitTest.CustomClasses;

/// <summary>
///     Test with custom rules and workflows classes
/// </summary>
public class CustomRuleAndWorkflowTest
{
    [Fact]
    public async Task RulesEngine_WithCustomRulesAndWorkflows_RunsSuccessfully()
    {
        var customRule = new CustomRule {
            RuleName = "CustomRule",
            Operator = "And",
            Enabled = true,
            ThisIsAmazingRule = new[] {
                new CustomRule {
                    RuleName = "CustomRule1",
                    Enabled = true,
                    RuleExpressionType = RuleExpressionType.LambdaExpression,
                    Expression = "input1.x > 10"
                },
                new CustomRule {
                    RuleName = "CustomRule2",
                    Enabled = true,
                    RuleExpressionType = RuleExpressionType.LambdaExpression,
                    Expression = "input1.x > 10"
                }
            }
        };

        var customWorkflow = new CustomWorkflow {
            WorkflowName = "CustomWorkflow",
            RuleExpressionType = RuleExpressionType.LambdaExpression,
            Rules = new[] { customRule }
        };


        var re = new RulesEngine([customWorkflow]);
        var input1 = GetInput1();
        List<RuleResultTree> result = await re.ExecuteAllRulesAsync("CustomWorkflow", input1);
        Assert.NotNull(result);
        Assert.IsType<List<RuleResultTree>>(result);
        Assert.Contains(result, c => c.IsSuccess);
    }

    [Fact]
    public async Task RulesEngine_WithCustomRulesAndWorkflowsAsJsonArray_RunsSuccessfully()
    {
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

        var re = new RulesEngine([workflowJson], typeof(CustomWorkflow));
        var input1 = GetInput1();
        List<RuleResultTree> result = await re.ExecuteAllRulesAsync("CustomWorkflow", input1);
        Assert.NotNull(result);
        Assert.IsType<List<RuleResultTree>>(result);
        Assert.Contains(result, c => c.IsSuccess);

        var firstResult = (CustomRule)result[0].ResultRule;
        Assert.NotNull(firstResult);
        Assert.IsType<CustomRule>(firstResult);
        Assert.Equal("CustomRule", firstResult.RuleName);
        Assert.Equal("And", firstResult.Operator);
        Assert.Equal(2, firstResult.ThisIsAmazingRule.Count());
        Assert.Contains(firstResult.ThisIsAmazingRule, c => c.RuleName == "CustomRule1");
        Assert.Equal("Whatever", firstResult.RandomProperty);
    }


    private dynamic GetInput1()
    {
        var converter = new ExpandoObjectConverter();
        const string basicInfo =
            "{\"x\": 50}";
        return JsonConvert.DeserializeObject<ExpandoObject>(basicInfo, converter);
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
