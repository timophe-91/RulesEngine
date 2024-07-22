﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RulesEngine.Actions;
using RulesEngine.Models;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace RulesEngine.UnitTest.ActionTests.MockClass;

[ExcludeFromCodeCoverage]
public class ReturnContextAction : ActionBase
{
    override protected ValueTask<object> Run(ActionContext context, RuleParameter[] ruleParameters,
        CancellationToken cancellationToken = default)
    {
        var stringContext = context.GetContext<string>("stringContext");
        var intContext = context.GetContext<int>("intContext");
        var objectContext = context.GetContext<object>("objectContext");

        return new ValueTask<object>(new { stringContext, intContext, objectContext });
    }
}