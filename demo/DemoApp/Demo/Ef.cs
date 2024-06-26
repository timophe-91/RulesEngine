﻿// Copyright (c) Microsoft Corporation.
//  Licensed under the MIT License.

using DemoApp.EFDataExample;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RulesEngine.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using static RulesEngine.Extensions.ListofRuleResultTreeExtension;

namespace DemoApp.Demo;

public class Ef
{
    public async Task Run()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Running {nameof(Ef)}....");
        Console.ResetColor();
        const string basicInfo =
            "{\"name\": \"hello\",\"email\": \"abcy@xyz.com\",\"creditHistory\": \"good\",\"country\": \"canada\",\"loyaltyFactor\": 3,\"totalPurchasesToDate\": 10000}";
        const string orderInfo = "{\"totalOrders\": 5,\"recurringItems\": 2}";
        const string telemetryInfo = "{\"noOfVisitsPerMonth\": 10,\"percentageOfBuyingToVisit\": 15}";

        var converter = new ExpandoObjectConverter();

        dynamic input1 = JsonConvert.DeserializeObject<ExpandoObject>(basicInfo, converter);
        dynamic input2 = JsonConvert.DeserializeObject<ExpandoObject>(orderInfo, converter);
        dynamic input3 = JsonConvert.DeserializeObject<ExpandoObject>(telemetryInfo, converter);

        var inputs = new[] { input1, input2, input3 };

        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "Discount.json", SearchOption.AllDirectories);
        if (files == null || files.Length == 0)
        {
            throw new FileNotFoundException("Rules not found.");
        }

        var fileData = await File.ReadAllTextAsync(files[0]);
        var workflow = JsonConvert.DeserializeObject<List<Workflow>>(fileData);

        var db = new RulesEngineDemoContext();
        if (await db.Database.EnsureCreatedAsync())
        {
            await db.Workflows.AddRangeAsync(workflow);
            await db.SaveChangesAsync();
        }

        var wfr = await db.Workflows.Include(i => i.Rules).ThenInclude(i => i.Rules).ToArrayAsync();

        var bre = new RulesEngine.RulesEngine(wfr);

        var discountOffered = "No discount offered.";

        var resultList = await bre.ExecuteAllRulesAsync("Discount", inputs);

        resultList.OnSuccess(eventName => {
            discountOffered = $"Discount offered is {eventName} % over MRP.";
        });

        resultList.OnFail(() => {
            discountOffered = "The user is not eligible for any discount.";
        });

        Console.WriteLine(discountOffered);
    }
}