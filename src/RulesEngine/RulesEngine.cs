﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentValidation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RulesEngine.Actions;
using RulesEngine.Exceptions;
using RulesEngine.ExpressionBuilders;
using RulesEngine.Extensions;
using RulesEngine.HelperFunctions;
using RulesEngine.Interfaces;
using RulesEngine.Models;
using RulesEngine.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RulesEngine;

/// <summary>
///     The Rules Engine itself
/// </summary>
/// <seealso cref="IRulesEngine" />
public class RulesEngine : IRulesEngineExtended
{
    #region Variables

    private readonly ReSettings _reSettings;
    private readonly RulesCache _rulesCache;
    private readonly RuleExpressionParser _ruleExpressionParser;
    private readonly RuleCompiler _ruleCompiler;
    private readonly ActionFactory _actionFactory;
    private readonly static Regex ParamParseRegex = new("(\\$\\(.*?\\))", RegexOptions.Compiled);

    #endregion

    #region Constructor

    /// <summary>
    ///     Initializes a new instance of the <see cref="RulesEngine" /> class.
    /// </summary>
    /// <param name="jsonConfig">The json configuration must be converted to <see cref="Workflow" />, NOT the Interface</param>
    /// <param name="reSettings"></param>
    public RulesEngine(string[] jsonConfig, ReSettings reSettings = null) : this(reSettings)
    {
        var workflows = jsonConfig.Select(JsonConvert.DeserializeObject<Workflow>).OfType<IWorkflow>().ToArray();
        AddWorkflow(workflows);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RulesEngine" /> class.
    ///     This constructor is used when the rules are in another json format.
    /// </summary>
    /// <param name="jsonConfig">The json configuration.</param>
    /// <param name="type">The type to deserialize the json to, must implement <see cref="IWorkflow" /></param>
    /// <param name="settings">The <see cref="JsonSerializerSettings" /> to use for deserialization</param>
    /// <param name="reSettings">The <see cref="ReSettings" /> to use for the rules engine</param>
    public RulesEngine(string[] jsonConfig, Type type, JsonSerializerSettings settings = null,
        ReSettings reSettings = null) : this(reSettings)
    {
        var workflow = jsonConfig.Select(item => JsonConvert.DeserializeObject(item, type, settings))
            .OfType<IWorkflow>().ToArray();
        AddWorkflow(workflow);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RulesEngine" /> class.
    /// </summary>
    /// <param name="workflows">The workflows.</param>
    /// <param name="reSettings">The <see cref="ReSettings" /> to use for the rules engine</param>
    public RulesEngine(IWorkflow[] workflows, ReSettings reSettings = null) : this(reSettings)
    {
        AddWorkflow(workflows);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RulesEngine" /> class.
    /// </summary>
    /// <param name="workflows">The workflows.</param>
    /// <param name="reSettings">The <see cref="ReSettings" /> to use for the rules engine</param>
    public RulesEngine(Workflow[] workflows, ReSettings reSettings = null) : this(reSettings)
    {
        AddWorkflow(workflows);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RulesEngine" /> class.
    /// </summary>
    /// <param name="reSettings">The <see cref="ReSettings" /> to use for the rules engine</param>
    public RulesEngine(ReSettings reSettings = null)
    {
        _reSettings = reSettings == null ? new ReSettings() : new ReSettings(reSettings);
        _reSettings.CacheConfig ??= new MemCacheConfig();

        _rulesCache = new RulesCache(_reSettings);
        _ruleExpressionParser = new RuleExpressionParser(_reSettings);
        _ruleCompiler = new RuleCompiler(new RuleExpressionBuilderFactory(_reSettings, _ruleExpressionParser),
            _reSettings);
        _actionFactory = new ActionFactory(GetActionRegistry(_reSettings));
    }

    private IDictionary<string, Func<ActionBase>> GetActionRegistry(ReSettings reSettings)
    {
        var actionDictionary = GetDefaultActionRegistry();
        var customActions = reSettings.CustomActions ?? new Dictionary<string, Func<ActionBase>>();
        foreach (var customAction in customActions)
        {
            actionDictionary.Add(customAction);
        }

        return actionDictionary;
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName, params object[] inputs)
    {
        return await ExecuteAllRulesAsync(workflowName, CancellationToken.None, inputs);
    }


    /// <inheritdoc />
    public async ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName,
        CancellationToken cancellationToken,
        params object[] inputs)
    {
        var ruleParams = new RuleParameter[inputs.Length];
        for (var i = 0; i < inputs.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = inputs[i];
            ruleParams[i] = new RuleParameter($"input{i + 1}", input);
        }

        return await ExecuteAllRulesAsync(workflowName, cancellationToken, ruleParams);
    }

    /// <inheritdoc />
    public async ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName,
        params RuleParameter[] ruleParams)
    {
        return await ExecuteAllRulesAsync(workflowName, CancellationToken.None, ruleParams);
    }

    /// <inheritdoc />
    public async ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName,
        CancellationToken cancellationToken, params RuleParameter[] ruleParams)
    {
        Array.Sort(ruleParams, (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        var ruleResultList = ValidateWorkflowAndExecuteRule(workflowName, ruleParams, cancellationToken);
        await ExecuteActionAsync(ruleResultList);
        return ruleResultList;
    }

    private async ValueTask ExecuteActionAsync(List<RuleResultTree> ruleResultList,
        CancellationToken cancellationToken = default)
    {
        foreach (var ruleResult in ruleResultList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ruleResult.ChildResults is not null && ruleResult.ChildResults.Any())
            {
                await ExecuteActionAsync(ruleResult.ChildResults, cancellationToken);
            }

            var actionResult = await ExecuteActionForRuleResult(ruleResult);
            ruleResult.ActionResult = new ActionResult {
                Output = actionResult.Output, Exception = actionResult.Exception
            };
        }
    }

    /// <inheritdoc />
    public async ValueTask<ActionRuleResult> ExecuteActionWorkflowAsync(string workflowName, string ruleName,
        RuleParameter[] ruleParameters)
    {
        return await ExecuteActionWorkflowAsync(workflowName, ruleName, CancellationToken.None, ruleParameters);
    }

    /// <inheritdoc />
    public async ValueTask<ActionRuleResult> ExecuteActionWorkflowAsync(string workflowName, string ruleName,
        CancellationToken cancellationToken,
        RuleParameter[] ruleParameters)
    {
        var compiledRule = CompileRule(workflowName, ruleName, ruleParameters);
        var resultTree = compiledRule(ruleParameters);
        return await ExecuteActionForRuleResult(resultTree, true, cancellationToken);
    }

    /// <summary>
    ///     Executes the action for rule result.
    /// </summary>
    /// <param name="resultTree">The result tree to execute action on.</param>
    /// <param name="includeRuleResults">if set to <c>true</c> [include rule results].</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="ActionRuleResult" />.</returns>
    private async ValueTask<ActionRuleResult> ExecuteActionForRuleResult(RuleResultTree resultTree,
        bool includeRuleResults = false, CancellationToken cancellationToken = default)
    {
        var ruleActions = resultTree?.ResultRule?.Actions;
        var actionInfo = resultTree?.IsSuccess == true ? ruleActions?.OnSuccess : ruleActions?.OnFailure;

        if (actionInfo is not null)
        {
            var action = _actionFactory.Get(actionInfo.Name);
            var ruleParameters = resultTree.Inputs.Select(kv => new RuleParameter(kv.Key, kv.Value)).ToArray();
            return await action.ExecuteAndReturnResultAsync(new ActionContext(actionInfo.Context, resultTree),
                ruleParameters, includeRuleResults, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        //If there is no action,return output as null and return the result for rule
        return new ActionRuleResult { Output = null, Results = includeRuleResults ? [resultTree] : null };
    }

    #endregion

    #region Private Methods

    /// <inheritdoc />
    public void AddWorkflow(params IWorkflow[] workflows)
    {
        try
        {
            foreach (var workflow in workflows)
            {
                var validator = new WorkflowsValidator();
                validator.ValidateAndThrow(workflow);
                if (!_rulesCache.ContainsWorkflows(workflow.WorkflowName))
                {
                    _rulesCache.AddOrUpdateWorkflows(workflow.WorkflowName, workflow);
                }
                else
                {
                    throw new ValidationException(
                        $"Cannot add workflow `{workflow.WorkflowName}` as it already exists. Use `AddOrUpdateWorkflow` to update existing workflow");
                }
            }
        }
        catch (ValidationException ex)
        {
            throw new RuleValidationException(ex.Message, ex.Errors);
        }
    }

    /// <inheritdoc />
    public void AddWorkflow(params Workflow[] workflows)
    {
        var iWorkflows = workflows.OfType<IWorkflow>().ToArray();
        AddWorkflow(iWorkflows);
    }

    /// <inheritdoc />
    public void AddOrUpdateWorkflow(params IWorkflow[] workflows)
    {
        try
        {
            foreach (var workflow in workflows)
            {
                var validator = new WorkflowsValidator();
                validator.ValidateAndThrow(workflow);
                _rulesCache.AddOrUpdateWorkflows(workflow.WorkflowName, workflow);
            }
        }
        catch (ValidationException ex)
        {
            throw new RuleValidationException(ex.Message, ex.Errors);
        }
    }

    /// <inheritdoc />
    public void AddOrUpdateWorkflow(params Workflow[] workflows)
    {
        var iWorkflows = workflows.OfType<IWorkflow>().ToArray();
        AddOrUpdateWorkflow(iWorkflows);
    }

    public List<string> GetAllRegisteredWorkflowNames()
    {
        return _rulesCache.GetAllWorkflowNames();
    }

    /// <inheritdoc />
    public bool ContainsWorkflow(string workflowName)
    {
        return _rulesCache.ContainsWorkflows(workflowName);
    }

    /// <inheritdoc />
    public void ClearWorkflows()
    {
        _rulesCache.Clear();
    }

    /// <inheritdoc />
    public void RemoveWorkflow(params string[] workflowNames)
    {
        foreach (var workflowName in workflowNames)
        {
            _rulesCache.Remove(workflowName);
        }
    }

    /// <summary>
    ///     This will validate workflow rules then call execute method
    /// </summary>
    /// <param name="workflowName">workflow name</param>
    /// <param name="ruleParams">The rule parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>list of rule result set</returns>
    private List<RuleResultTree> ValidateWorkflowAndExecuteRule(string workflowName, RuleParameter[] ruleParams,
        CancellationToken cancellationToken)
    {
        List<RuleResultTree> result;

        if (RegisterRule(workflowName, ruleParams))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = ExecuteAllRuleByWorkflow(workflowName, cancellationToken, ruleParams);
        }
        else
        {
            // if rules are not registered with Rules Engine
            throw new ArgumentException($"Rule config file is not present for the {workflowName} workflow");
        }

        return result;
    }

    /// <summary>
    ///     This will compile the rules and store them to dictionary
    /// </summary>
    /// <param name="workflowName">workflow name</param>
    /// <param name="ruleParams">The rule parameters.</param>
    /// <returns>
    ///     bool result
    /// </returns>
    private bool RegisterRule(string workflowName, params RuleParameter[] ruleParams)
    {
        var compileRulesKey = GetCompiledRulesKey(workflowName, ruleParams);
        if (_rulesCache.AreCompiledRulesUpToDate(compileRulesKey, workflowName))
        {
            return true;
        }

        var workflow = _rulesCache.GetWorkflow(workflowName);
        if (workflow == null)
        {
            return false;
        }

        if (_reSettings.AutoRegisterInputType)
        {
            _reSettings.CustomTypes =
                _reSettings.CustomTypes.Safe().Union(ruleParams.Select(c => c.Type)).ToArray();
        }
        // add separate compilation for global params

        var globalParamExp = new Lazy<RuleExpressionParameter[]>(
            () => _ruleCompiler.GetRuleExpressionParameters(workflow.RuleExpressionType, workflow.GlobalParams,
                ruleParams)
        );

        var dictFunc = workflow.GetRules().Where(c => c.Enabled).ToDictionary(rule => rule.RuleName,
            rule => CompileRule(rule, workflow.RuleExpressionType, ruleParams, globalParamExp));

        _rulesCache.AddOrUpdateCompiledRule(compileRulesKey, dictFunc);
        return true;
    }


    private RuleFunc<RuleResultTree> CompileRule(string workflowName, string ruleName, RuleParameter[] ruleParameters)
    {
        var workflow = _rulesCache.GetWorkflow(workflowName);
        if (workflow == null)
        {
            throw new ArgumentException($"Workflow `{workflowName}` is not found");
        }

        var currentRule = workflow.GetRules()?.SingleOrDefault(c => c.RuleName == ruleName && c.Enabled);
        if (currentRule == null)
        {
            throw new ArgumentException($"Workflow `{workflowName}` does not contain any rule named `{ruleName}`");
        }

        var globalParamExp = new Lazy<RuleExpressionParameter[]>(
            () => _ruleCompiler.GetRuleExpressionParameters(workflow.RuleExpressionType, workflow.GlobalParams,
                ruleParameters)
        );
        return CompileRule(currentRule, workflow.RuleExpressionType, ruleParameters, globalParamExp);
    }

    private RuleFunc<RuleResultTree> CompileRule(IRule rule, RuleExpressionType ruleExpressionType,
        RuleParameter[] ruleParams, Lazy<RuleExpressionParameter[]> scopedParams)
    {
        return _ruleCompiler.CompileRule(rule, ruleExpressionType, ruleParams, scopedParams);
    }


    /// <summary>
    ///     This will execute the compiled rules
    /// </summary>
    /// <param name="workflowName">The workflow to execute rules for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="ruleParameters">The rule parameters, for the rules to execute.</param>
    /// <returns>list of rule result set</returns>
    private List<RuleResultTree> ExecuteAllRuleByWorkflow(string workflowName, CancellationToken cancellationToken,
        RuleParameter[] ruleParameters)
    {
        var result = new List<RuleResultTree>();
        var compiledRulesCacheKey = GetCompiledRulesKey(workflowName, ruleParameters);
        foreach (var compiledRule in _rulesCache.GetCompiledRules(compiledRulesCacheKey)?.Values ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultTree = compiledRule(ruleParameters);
            result.Add(resultTree);
        }

        result = FormatErrorMessages(result);
        return result;
    }

    private string GetCompiledRulesKey(string workflowName, RuleParameter[] ruleParams)
    {
        var ruleParamsKey = string.Join("-", ruleParams.Select(c => $"{c.Name}_{c.Type.Name}"));
        var key = $"{workflowName}-" + ruleParamsKey;
        return key;
    }

    private IDictionary<string, Func<ActionBase>> GetDefaultActionRegistry()
    {
        return new Dictionary<string, Func<ActionBase>> {
            { "OutputExpression", () => new OutputExpressionAction(_ruleExpressionParser) },
            { "EvaluateRule", () => new EvaluateRuleAction(this, _ruleExpressionParser) }
        };
    }

    /// <summary>
    ///     The result
    /// </summary>
    /// <param name="ruleResultList">The result.</param>
    /// <returns>Updated error message.</returns>
    private List<RuleResultTree> FormatErrorMessages(List<RuleResultTree> ruleResultList)
    {
        if (!_reSettings.EnableFormattedErrorMessage || ruleResultList is null)
        {
            return ruleResultList;
        }

        var formatErrorMessages = ruleResultList;
        foreach (var ruleResult in formatErrorMessages)
        {
            if (!ruleResult.IsSuccess)
            {
                continue;
            }

            var errorMessage = ruleResult.Rule?.ErrorMessage;
            if (!string.IsNullOrWhiteSpace(ruleResult.ExceptionMessage) || errorMessage is null)
            {
                continue;
            }

            var errorParameters = ParamParseRegex.Matches(errorMessage);

            var inputs = ruleResult.Inputs;
            foreach (var param in errorParameters)
            {
                var paramVal = param?.ToString();
                var property = paramVal?.Substring(2, paramVal.Length - 3);
                if (property?.Split('.').Length > 1)
                {
                    var typeName = property.Split('.')[0];
                    var propertyName = property.Split('.')[1];
                    errorMessage = UpdateErrorMessage(errorMessage, inputs, property, typeName, propertyName);
                }
                else
                {
                    var arrParams = inputs?.Select(c => new { Name = c.Key, c.Value });
                    var model = arrParams?.FirstOrDefault(a => string.Equals(a.Name, property));
                    var value = model?.Value != null ? JsonConvert.SerializeObject(model.Value) : null;
                    errorMessage = errorMessage?.Replace($"$({property})", value ?? $"$({property})");
                }
            }

            ruleResult.ExceptionMessage = errorMessage;
        }

        return formatErrorMessages;
    }

    /// <summary>
    ///     Updates the error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="inputs"></param>
    /// <param name="property">The property.</param>
    /// <param name="typeName">Name of the type.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns>Updated error message.</returns>
    private static string UpdateErrorMessage(string errorMessage, IDictionary<string, object> inputs, string property,
        string typeName, string propertyName)
    {
        var arrParams = inputs?.Select(c => new { Name = c.Key, c.Value });
        var model = arrParams?.FirstOrDefault(a => string.Equals(a.Name, typeName));
        if (model != null)
        {
            var modelJson = JsonConvert.SerializeObject(model.Value);
            var jObj = JObject.Parse(modelJson);
            JToken jToken;
            _ = jObj.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out jToken);
            errorMessage =
                errorMessage.Replace($"$({property})", jToken != null ? jToken.ToString() : $"({property})");
        }

        return errorMessage;
    }

    #endregion
}