﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RulesEngine.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RulesEngine.UnitTest;

[ExcludeFromCodeCoverage]
public class RulesEnabledTests
{
    [Theory]
    [InlineData("RuleEnabledFeatureTest", new[] { true, true })]
    [InlineData("RuleEnabledNestedFeatureTest", new[] { true, true, false })]
    public async Task RulesEngine_ShouldOnlyExecuteEnabledRules(string workflowName, bool[] expectedRuleResults)
    {
        var workflow = GetWorkflows();
        var rulesEngine = new RulesEngine(workflow, new ReSettings { EnableExceptionAsErrorMessage = false });
        var input1 = new { TrueValue = true };
        var result = await rulesEngine.ExecuteAllRulesAsync(workflowName, input1);
        Assert.NotNull(result);
        Assert.True(NestedEnabledCheck(result));

        Assert.Equal(expectedRuleResults.Length, result.Count);
        for (var i = 0; i < expectedRuleResults.Length; i++)
        {
            Assert.Equal(expectedRuleResults[i], result[i].IsSuccess);
        }
    }


    [Theory]
    [InlineData("RuleEnabledFeatureTest", new[] { true, true })]
    [InlineData("RuleEnabledNestedFeatureTest", new[] { true, true, false })]
    public async Task WorkflowUpdatedRuleEnabled_ShouldReflect(string workflowName, bool[] expectedRuleResults)
    {
        var workflow = GetWorkflows().Single(c => c.WorkflowName == workflowName);
        var rulesEngine = new RulesEngine(new ReSettings { EnableExceptionAsErrorMessage = false });
        rulesEngine.AddWorkflow(workflow);
        var input1 = new { TrueValue = true };
        var result = await rulesEngine.ExecuteAllRulesAsync(workflowName, input1);
        Assert.NotNull(result);
        Assert.True(NestedEnabledCheck(result));

        Assert.Equal(expectedRuleResults.Length, result.Count);
        for (var i = 0; i < expectedRuleResults.Length; i++)
        {
            Assert.Equal(expectedRuleResults[i], result[i].IsSuccess);
        }

        rulesEngine.RemoveWorkflow(workflowName);

        var firstRule = workflow.Rules.First();

        firstRule.Enabled = false;
        rulesEngine.AddWorkflow(workflow);

        var expectedLength = workflow.Rules.Count(c => c.Enabled);

        var result2 = await rulesEngine.ExecuteAllRulesAsync(workflowName, input1);
        Assert.Equal(expectedLength, result2.Count);

        Assert.DoesNotContain(result2, c => c.Rule.RuleName == firstRule.RuleName);
    }

    private bool NestedEnabledCheck(IEnumerable<RuleResultTree> ruleResults)
    {
        var areAllRulesEnabled = ruleResults.All(c => c.Rule.Enabled);
        if (areAllRulesEnabled)
        {
            foreach (var ruleResult in ruleResults)
            {
                if (ruleResult.ChildResults?.Any() == true)
                {
                    var areAllChildRulesEnabled = NestedEnabledCheck(ruleResult.ChildResults);
                    if (areAllChildRulesEnabled == false)
                    {
                        return false;
                    }
                }
            }
        }

        return areAllRulesEnabled;
    }

    private Workflow[] GetWorkflows()
    {
        return new[] {
            new Workflow {
                WorkflowName = "RuleEnabledFeatureTest",
                Rules =
                    new List<Rule> {
                        new() {
                            RuleName = "RuleWithoutEnabledFieldMentioned", Expression = "input1.TrueValue == true"
                        },
                        new() {
                            RuleName = "RuleWithEnabledSetToTrue",
                            Expression = "input1.TrueValue == true",
                            Enabled = true
                        },
                        new() {
                            RuleName = "RuleWithEnabledSetToFalse",
                            Expression = "input1.TrueValue == true",
                            Enabled = false
                        }
                    }
            },
            new Workflow {
                WorkflowName = "RuleEnabledNestedFeatureTest",
                Rules = new List<Rule> {
                    new() {
                        RuleName = "RuleWithoutEnabledFieldMentioned",
                        Operator = "And",
                        Rules =
                            new List<Rule> {
                                new() { RuleName = "RuleWithoutEnabledField", Expression = "input1.TrueValue" }
                            }
                    },
                    new() {
                        RuleName = "RuleWithOneChildSetToFalse",
                        Expression = "input1.TrueValue == true",
                        Operator = "And",
                        Rules = new List<Rule> {
                            new() {
                                RuleName = "RuleWithEnabledFalse", Expression = "input1.TrueValue", Enabled = false
                            },
                            new() { RuleName = "RuleWithEnabledTrue", Expression = "input1.TrueValue", Enabled = true }
                        }
                    },
                    new() {
                        RuleName = "RuleWithParentSetToFalse",
                        Operator = "And",
                        Enabled = false,
                        Rules = new List<Rule> {
                            new() { RuleName = "RuleWithEnabledTrue", Expression = "input1.TrueValue", Enabled = true }
                        }
                    },
                    new() {
                        RuleName = "RuleWithAllChildSetToFalse",
                        Operator = "And",
                        Enabled = true,
                        Rules = new List<Rule> {
                            new() {
                                RuleName = "ChildRuleWithEnabledFalse", Expression = "input1.TrueValue", Enabled = false
                            }
                        }
                    }
                }
            }
        };
    }
}