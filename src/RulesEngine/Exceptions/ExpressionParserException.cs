// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace RulesEngine.Exceptions;

public sealed class ExpressionParserException : Exception
{
    public ExpressionParserException(string message, string expression) : base(message)
    {
        Data.Add("Expression", expression);
    }
}
