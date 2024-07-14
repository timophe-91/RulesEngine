// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RulesEngine.ExpressionBuilders;
using RulesEngine.Models;
using System;

namespace RulesEngine;

internal class RuleExpressionBuilderFactory
{
    private readonly LambdaExpressionBuilder _lambdaExpressionBuilder;
    private readonly ReSettings _reSettings;

    public RuleExpressionBuilderFactory(ReSettings reSettings, RuleExpressionParser expressionParser)
    {
        _reSettings = reSettings;
        _lambdaExpressionBuilder = new LambdaExpressionBuilder(_reSettings, expressionParser);
    }

    public RuleExpressionBuilderBase RuleGetExpressionBuilder(RuleExpressionType ruleExpressionType)
    {
        return ruleExpressionType switch {
            RuleExpressionType.LambdaExpression => _lambdaExpressionBuilder,
            _ => throw new InvalidOperationException($"{nameof(ruleExpressionType)} has not been supported yet.")
        };
    }
}