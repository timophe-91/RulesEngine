﻿// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using Newtonsoft.Json;
using RulesEngine.Models;
using System;
using System.Collections.Generic;

namespace RulesEngine.Actions;

public class ActionContext
{
    private readonly Dictionary<string, string> _context;
    private readonly RuleResultTree _parentResult;

    public ActionContext(IDictionary<string, object> context, RuleResultTree parentResult)
    {
        _context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in context)
        {
            var key = kv.Key;
            var value = kv.Value.GetType().Name switch {
                "String" or "JsonElement" => kv.Value.ToString(),
                _ => JsonConvert.SerializeObject(kv.Value)
            };

            _context.Add(key, value);
        }

        _parentResult = parentResult;
    }

    public RuleResultTree GetParentRuleResult()
    {
        return _parentResult;
    }

    public bool TryGetContext<T>(string name, out T output)
    {
        try
        {
            output = GetContext<T>(name);
            return true;
        }
        catch (ArgumentException)
        {
            output = default;
            return false;
        }
    }

    public T GetContext<T>(string name)
    {
        try
        {
            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(_context[name], typeof(T));
            }

            return JsonConvert.DeserializeObject<T>(_context[name]);
        }
        catch (KeyNotFoundException)
        {
            throw new ArgumentException($"Argument `{name}` was not found in the action context");
        }
        catch (JsonException)
        {
            throw new ArgumentException(
                $"Failed to convert argument `{name}` to type `{typeof(T).Name}` in the action context");
        }
    }
}