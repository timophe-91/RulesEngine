﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RulesEngine.Exceptions;
using RulesEngine.ExpressionBuilders;
using RulesEngine.HelperFunctions;
using RulesEngine.Interfaces;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace RulesEngine;

/// <summary>
///     Rule compilers
/// </summary>
internal class RuleCompiler
{
    /// <summary>
    ///     The expression builder factory
    /// </summary>
    private readonly RuleExpressionBuilderFactory _expressionBuilderFactory;

    /// <summary>
    ///     The nested operators
    /// </summary>
    private readonly ExpressionType[] _nestedOperators = {
        ExpressionType.And, ExpressionType.AndAlso, ExpressionType.Or, ExpressionType.OrElse
    };

    private readonly ReSettings _reSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RuleCompiler" /> class.
    /// </summary>
    /// <param name="expressionBuilderFactory">The <see cref="RuleExpressionBuilderFactory" />.</param>
    /// <param name="reSettings">The <see cref="ReSettings" />.</param>
    /// <exception cref="ArgumentNullException">expressionBuilderFactory</exception>
    internal RuleCompiler(RuleExpressionBuilderFactory expressionBuilderFactory, ReSettings reSettings)
    {
        _expressionBuilderFactory = expressionBuilderFactory ??
                                    throw new ArgumentNullException(nameof(expressionBuilderFactory),
                                        $"The {nameof(expressionBuilderFactory)} can't be null.");
        _reSettings = reSettings;
    }

    /// <summary>
    ///     Compiles the rule
    /// </summary>
    /// <param name="rule">The rule to compile</param>
    /// <param name="ruleExpressionType">The rule expression type</param>
    /// <param name="ruleParams">The <see cref="RuleParameter" />[] for the rule</param>
    /// <param name="globalParams">The global <see cref="T:Lazy{RuleExpressionParameter[]}" /> parameters</param>
    /// <returns>Compiled func delegate</returns>
    internal RuleFunc<RuleResultTree> CompileRule(IRule rule, RuleExpressionType ruleExpressionType,
        RuleParameter[] ruleParams, Lazy<RuleExpressionParameter[]> globalParams)
    {
        if (rule is null)
        {
            var ex = new ArgumentNullException(nameof(rule));
            throw ex;
        }

        try
        {
            var globalParamExp = globalParams.Value;
            var extendedRuleParams = ruleParams.Concat(globalParamExp.Select(c =>
                    new RuleParameter(c.ParameterExpression.Name, c.ParameterExpression.Type)))
                .ToArray();
            var ruleExpression = GetDelegateForRule(rule, extendedRuleParams);


            return GetWrappedRuleFunc(rule, ruleExpression, ruleParams, globalParamExp);
        }
        catch (Exception ex)
        {
            var message = $"Error while compiling rule `{rule.RuleName}`: {ex.Message}";
            return Helpers.ToRuleExceptionResult(_reSettings, rule, new RuleException(message, ex));
        }
    }


    /// <summary>
    ///     Gets the expression for rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="ruleParams">The rule params </param>
    /// <returns></returns>
    private RuleFunc<RuleResultTree> GetDelegateForRule(IRule rule, RuleParameter[] ruleParams)
    {
        var scopedParamList = GetRuleExpressionParameters(rule.RuleExpressionType, rule.LocalParams, ruleParams);

        var extendedRuleParams = ruleParams.Concat(scopedParamList.Select(c =>
                new RuleParameter(c.ParameterExpression.Name, c.ParameterExpression.Type)))
            .ToArray();

        RuleFunc<RuleResultTree> ruleFn;

        if (Enum.TryParse(rule.Operator, out ExpressionType nestedOperator) &&
            _nestedOperators.Contains(nestedOperator) &&
            rule.GetNestedRules() is not null && rule.GetNestedRules().Any())
        {
            ruleFn = BuildNestedRuleFunc(rule, nestedOperator, extendedRuleParams);
        }
        else
        {
            ruleFn = BuildRuleFunc(rule, extendedRuleParams);
        }

        return GetWrappedRuleFunc(rule, ruleFn, ruleParams, scopedParamList);
    }

    internal RuleExpressionParameter[] GetRuleExpressionParameters(RuleExpressionType ruleExpressionType,
        IEnumerable<ScopedParam> localParams, RuleParameter[] ruleParams)
    {
        if (!_reSettings.EnableScopedParams)
        {
            return [];
        }

        var ruleExpParams = new List<RuleExpressionParameter>();

        if (localParams is null)
        {
            return ruleExpParams.ToArray();
        }

        var scopedParams = localParams as ScopedParam[] ?? localParams.ToArray();
        if (scopedParams.Length <= 0)
        {
            return ruleExpParams.ToArray();
        }

        var parameters = ruleParams.Select(c => c.ParameterExpression)
            .ToList();

        var expressionBuilder = GetExpressionBuilder(ruleExpressionType);

        foreach (var lp in scopedParams)
        {
            try
            {
                var lpExpression = expressionBuilder.Parse(lp.Expression, parameters.ToArray(), null);
                var ruleExpParam = new RuleExpressionParameter {
                    ParameterExpression = Expression.Parameter(lpExpression.Type, lp.Name),
                    ValueExpression = lpExpression
                };
                parameters.Add(ruleExpParam.ParameterExpression);
                ruleExpParams.Add(ruleExpParam);
            }
            catch (Exception ex)
            {
                var message = $"{ex.Message}, in ScopedParam: {lp.Name}";
                throw new RuleException(message);
            }
        }

        return ruleExpParams.ToArray();
    }

    /// <summary>
    ///     Builds the expression.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="ruleParams"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private RuleFunc<RuleResultTree> BuildRuleFunc(IRule rule, RuleParameter[] ruleParams)
    {
        var ruleExpressionBuilder = GetExpressionBuilder(rule.RuleExpressionType);

        var ruleFunc = ruleExpressionBuilder.BuildDelegateForRule(rule, ruleParams);

        return ruleFunc;
    }

    /// <summary>
    ///     Builds the nested expression.
    /// </summary>
    /// <param name="parentRule">The parent rule.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="ruleParams"></param>
    /// <returns>Expression of func delegate</returns>
    /// <exception cref="InvalidCastException"></exception>
    private RuleFunc<RuleResultTree> BuildNestedRuleFunc(IRule parentRule, ExpressionType operation,
        RuleParameter[] ruleParams)
    {
        var ruleFuncList = new List<RuleFunc<RuleResultTree>>();
        foreach (var r in parentRule.GetNestedRules().Where(c => c.Enabled))
        {
            ruleFuncList.Add(GetDelegateForRule(r, ruleParams));
        }

        return paramArray => {
            var (isSuccess, resultList) = ApplyOperation(paramArray, ruleFuncList, operation);

            var result = Helpers.ToResultTree(_reSettings, parentRule, resultList, IsSuccessFn);
            return result(paramArray);

            bool IsSuccessFn(object[] p)
            {
                return isSuccess;
            }
        };
    }


    private (bool isSuccess, List<RuleResultTree> result) ApplyOperation(RuleParameter[] paramArray,
        List<RuleFunc<RuleResultTree>> ruleFuncList, ExpressionType operation)
    {
        if (ruleFuncList is null)
        {
            return (false, new List<RuleResultTree>());
        }

        if (ruleFuncList.Count <= 0)
        {
            return (false, new List<RuleResultTree>());
        }

        var resultList = new List<RuleResultTree>();
        var isSuccess = operation is ExpressionType.And or ExpressionType.AndAlso;

        foreach (var ruleResult in ruleFuncList.Select(ruleFunc => ruleFunc(paramArray)))
        {
            resultList.Add(ruleResult);
            switch (operation)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    isSuccess = isSuccess && ruleResult.IsSuccess;
                    if (_reSettings.NestedRuleExecutionMode == NestedRuleExecutionMode.Performance && !isSuccess)
                    {
                        return (false, resultList);
                    }

                    break;

                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    isSuccess = isSuccess || ruleResult.IsSuccess;
                    if (_reSettings.NestedRuleExecutionMode == NestedRuleExecutionMode.Performance && isSuccess)
                    {
                        return (true, resultList);
                    }

                    break;
            }
        }

        return (isSuccess, resultList);
    }

    internal Func<object[], Dictionary<string, object>> CompileScopedParams(RuleExpressionType ruleExpressionType,
        RuleParameter[] ruleParameters, RuleExpressionParameter[] ruleExpParams)
    {
        return GetExpressionBuilder(ruleExpressionType).CompileScopedParams(ruleParameters, ruleExpParams);
    }

    private RuleFunc<RuleResultTree> GetWrappedRuleFunc(IRule rule, RuleFunc<RuleResultTree> ruleFunc,
        RuleParameter[] ruleParameters, RuleExpressionParameter[] ruleExpParams)
    {
        if (ruleExpParams.Length == 0)
        {
            return ruleFunc;
        }

        var paramDelegate = CompileScopedParams(rule.RuleExpressionType, ruleParameters, ruleExpParams);

        return ruleParams => {
            var inputs = ruleParams.Select(c => c.Value).ToArray();
            IEnumerable<RuleParameter> scopedParams;
            try
            {
                var scopedParamsDict = paramDelegate(inputs);
                scopedParams = scopedParamsDict.Select(c => new RuleParameter(c.Key, c.Value));
            }
            catch (Exception ex)
            {
                var message = $"Error while executing scoped params for rule `{rule.RuleName}` - {ex}";
                var resultFn = Helpers.ToRuleExceptionResult(_reSettings, rule, new RuleException(message, ex));
                return resultFn(ruleParams);
            }

            var extendedInputs = ruleParams.Concat(scopedParams);
            var result = ruleFunc(extendedInputs.ToArray());
            return result;
        };
    }

    private RuleExpressionBuilderBase GetExpressionBuilder(RuleExpressionType expressionType)
    {
        return _expressionBuilderFactory.RuleGetExpressionBuilder(expressionType);
    }
}
