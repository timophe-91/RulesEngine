using RulesEngine.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RulesEngine.Interfaces;

/// <summary>
///     Extended Rules Engine with IRule and IWorkflowSupport
/// </summary>
public interface IRulesEngineExtended : IRulesEngine
{
    /// <summary>
    ///     Adds new workflows to RulesEngine
    /// </summary>
    /// <param name="workflows">The workflows to add</param>
    void AddWorkflow(params IWorkflow[] workflows);


    /// <summary>
    ///     Adds or updates the workflow.
    /// </summary>
    /// <param name="workflows">The workflows.</param>
    void AddOrUpdateWorkflow(params IWorkflow[] workflows);

    /// <summary>
    ///     This will execute all the rules of the specified workflow
    /// </summary>
    /// <param name="workflowName">The name of the workflow with rules to execute against the inputs</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <param name="inputs">A variable number of inputs</param>
    /// <returns>List of rule results</returns>
    ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName, CancellationToken cancellationToken, params object[] inputs);

    /// <summary>
    ///     This will execute all the rules of the specified workflow
    /// </summary>
    /// <param name="workflowName">The name of the workflow with rules to execute against the inputs</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <param name="ruleParams">A variable number of rule parameters</param>
    /// <returns>List of rule results</returns>
    ValueTask<List<RuleResultTree>> ExecuteAllRulesAsync(string workflowName, CancellationToken cancellationToken, params RuleParameter[] ruleParams);

    /// <summary>
    ///     This will execute all the rules of the specified workflow
    /// </summary>
    /// <param name="workflowName">The name of the workflow with rules to execute against the inputs</param>
    /// <param name="ruleName">The name of the rule to execute</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <param name="ruleParameters">The rule parameters</param>
    /// <returns></returns>
    ValueTask<ActionRuleResult> ExecuteActionWorkflowAsync(string workflowName, string ruleName, CancellationToken cancellationToken,
        RuleParameter[] ruleParameters);
}
