﻿// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using Newtonsoft.Json;
using RulesEngine.Extensions;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DemoApp.Demo;

internal class ListItem
{
    public int Id { get; set; }
    public string Value { get; set; }
}

public class NestedInput
{
    public async Task Run()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Running {nameof(NestedInput)}....");
        Console.ResetColor();
        var nestedInput = new {
            SimpleProp = "simpleProp",
            NestedProp = new {
                SimpleProp = "nestedSimpleProp",
                ListProp = new List<ListItem> { new() { Id = 1, Value = "first" }, new() { Id = 2, Value = "second" } }
            }
        };

        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "NestedInputDemo.json",
            SearchOption.AllDirectories);
        if (files == null || files.Length == 0)
        {
            throw new FileNotFoundException("Rules not found.");
        }

        var fileData = await File.ReadAllTextAsync(files[0]);
        var workflows = JsonConvert.DeserializeObject<List<Workflow>>(fileData);

        var bre = new RulesEngine.RulesEngine(workflows.ToArray());
        foreach (var workflowName in workflows.Select(w => w.WorkflowName))
        {
            var resultList = await bre.ExecuteAllRulesAsync(workflowName, nestedInput);

            resultList.OnSuccess(eventName => {
                Console.WriteLine($"{workflowName} evaluation resulted in success - {eventName}");
            }).OnFail(() => {
                Console.WriteLine($"{workflowName} evaluation resulted in failure");
            });
        }
    }
}