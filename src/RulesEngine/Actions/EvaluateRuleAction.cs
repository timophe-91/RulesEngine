// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using RulesEngine.ExpressionBuilders;
using RulesEngine.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RulesEngine.Actions;

public class EvaluateRuleAction : ActionBase
{
    private readonly RulesEngine _ruleEngine;
    private readonly RuleExpressionParser _ruleExpressionParser;

    public EvaluateRuleAction(RulesEngine ruleEngine, RuleExpressionParser ruleExpressionParser)
    {
        _ruleEngine = ruleEngine;
        _ruleExpressionParser = ruleExpressionParser;
    }

    internal async override ValueTask<ActionRuleResult> ExecuteAndReturnResultAsync(ActionContext context,
        RuleParameter[] ruleParameters, bool includeRuleResults = false, CancellationToken cancellationToken = default)
    {
        var innerResult =
            await base.ExecuteAndReturnResultAsync(context, ruleParameters, includeRuleResults, cancellationToken);
        var output = innerResult.Output as ActionRuleResult;
        List<RuleResultTree> resultList = null;
        if (includeRuleResults)
        {
            resultList = [..output?.Results ?? []];
            resultList.AddRange(innerResult.Results);
        }

        return new ActionRuleResult {
            Output = output?.Output, Exception = innerResult.Exception, Results = resultList
        };
    }

    async override protected ValueTask<object> Run(ActionContext context, RuleParameter[] ruleParameters,
        CancellationToken cancellationToken = default)
    {
        var workflowName = context.GetContext<string>("workflowName");
        var ruleName = context.GetContext<string>("ruleName");
        var filteredRuleParameters = new List<RuleParameter>(ruleParameters);
        if (context.TryGetContext<List<string>>("inputFilter", out var inputFilter))
        {
            filteredRuleParameters = ruleParameters.Where(c => inputFilter.Contains(c.Name)).ToList();
        }

        if (context.TryGetContext<List<ScopedParam>>("additionalInputs", out var additionalInputs))
        {
            foreach (var additionalInput in additionalInputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic value = _ruleExpressionParser.Evaluate<object>(additionalInput.Expression, ruleParameters);
                filteredRuleParameters.Add(new RuleParameter(additionalInput.Name, value));
            }
        }

        var ruleResult =
            await _ruleEngine.ExecuteActionWorkflowAsync(workflowName, ruleName, cancellationToken,
                filteredRuleParameters.ToArray());
        return ruleResult;
    }
}