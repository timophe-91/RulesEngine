﻿// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using RulesEngine.ExpressionBuilders;
using RulesEngine.Models;
using System.Threading;
using System.Threading.Tasks;

namespace RulesEngine.Actions;

public class OutputExpressionAction : ActionBase
{
    private readonly RuleExpressionParser _ruleExpressionParser;

    public OutputExpressionAction(RuleExpressionParser ruleExpressionParser)
    {
        _ruleExpressionParser = ruleExpressionParser;
    }

    override protected ValueTask<object> Run(ActionContext context, RuleParameter[] ruleParameters,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expression = context.GetContext<string>("expression");
        return new ValueTask<object>(_ruleExpressionParser.Evaluate<object>(expression, ruleParameters));
    }
}
