// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentValidation;
using FluentValidation.Results;
using System.Collections.Generic;

namespace RulesEngine.Exceptions;

public class RuleValidationException(string message, IEnumerable<ValidationFailure> errors)
    : ValidationException(message, errors);
