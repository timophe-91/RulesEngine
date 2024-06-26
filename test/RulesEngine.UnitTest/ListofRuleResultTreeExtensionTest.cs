﻿// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using RulesEngine.Extensions;
using RulesEngine.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace RulesEngine.UnitTest;

[Trait("Category", "Unit")]
[ExcludeFromCodeCoverage]
public class ListofRuleResultTreeExtensionTest
{
    [Fact]
    public void OnSuccessWithSuccessTest()
    {
        var rulesResultTree = new List<RuleResultTree> {
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = true,
                Rule = new Rule { RuleName = "Test Rule 1" }
            },
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = false,
                Rule = new Rule { RuleName = "Test Rule 2" }
            }
        };

        var successEventName = string.Empty;

        rulesResultTree.OnSuccess(eventName => {
            successEventName = eventName;
        });

        Assert.Equal("Test Rule 1", successEventName);
    }

    [Fact]
    public void OnSuccessWithSuccessWithEventTest()
    {
        var rulesResultTree = new List<RuleResultTree> {
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = true,
                Rule = new Rule { RuleName = "Test Rule 1", SuccessEvent = "Event 1" }
            },
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = false,
                Rule = new Rule { RuleName = "Test Rule 2" }
            }
        };

        var successEventName = string.Empty;

        rulesResultTree.OnSuccess(eventName => {
            successEventName = eventName;
        });

        Assert.Equal("Event 1", successEventName);
    }

    [Fact]
    public void OnSuccessWithouSuccessTest()
    {
        var rulesResultTree = new List<RuleResultTree> {
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = false,
                Rule = new Rule { RuleName = "Test Rule 1" }
            },
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = false,
                Rule = new Rule { RuleName = "Test Rule 2" }
            }
        };

        var successEventName = string.Empty;

        rulesResultTree.OnSuccess(eventName => {
            successEventName = eventName;
        });

        Assert.Equal(successEventName, string.Empty);
    }


    [Fact]
    public void OnFailWithSuccessTest()
    {
        var rulesResultTree = new List<RuleResultTree> {
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = true,
                Rule = new Rule { RuleName = "Test Rule 1" }
            },
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = false,
                Rule = new Rule { RuleName = "Test Rule 2" }
            }
        };

        var successEventName = true;

        rulesResultTree.OnFail(() => {
            successEventName = false;
        });

        Assert.True(successEventName);
    }

    [Fact]
    public void OnFailWithoutSuccessTest()
    {
        var rulesResultTree = new List<RuleResultTree> {
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = false,
                Rule = new Rule { RuleName = "Test Rule 1" }
            },
            new() {
                ChildResults = null,
                ExceptionMessage = string.Empty,
                Inputs = new Dictionary<string, object>(),
                IsSuccess = false,
                Rule = new Rule { RuleName = "Test Rule 2" }
            }
        };

        var successEventName = true;

        rulesResultTree.OnFail(() => {
            successEventName = false;
        });

        Assert.False(successEventName);
    }
}