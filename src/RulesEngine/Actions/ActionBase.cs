// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using RulesEngine.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RulesEngine.Actions;

public abstract class ActionBase
{
    internal async virtual ValueTask<ActionRuleResult> ExecuteAndReturnResultAsync(ActionContext context,
        RuleParameter[] ruleParameters, bool includeRuleResults = false, CancellationToken cancellationToken = default)
    {
        var result = new ActionRuleResult();
        try
        {
            result.Output = await Run(context, ruleParameters, cancellationToken);
        }
        catch (Exception ex)
        {
            result.Exception = new Exception($"Exception while executing {GetType().Name}: {ex.Message}", ex);
        }
        finally
        {
            if (includeRuleResults)
            {
                result.Results = [context.GetParentRuleResult()];
            }
        }

        return result;
    }

    protected abstract ValueTask<object> Run(ActionContext context, RuleParameter[] ruleParameters,
        CancellationToken cancellationToken = default);
}