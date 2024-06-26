// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DemoApp.Demo;
using System;
using System.Threading.Tasks;

namespace DemoApp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("RulesEngine Demo stating...");
        Console.ResetColor();
        await new Basic().Run();
        await new CustomClasses().Run();
        await new CustomClassesJson().Run();
        await new Json().Run();
        await new NestedInput().Run();
        await new Ef().Run();
    }
}