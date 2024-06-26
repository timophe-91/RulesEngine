// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Dynamic.Core;
using Xunit;

namespace RulesEngine.UnitTest;

/// <inheritdoc />
[Trait("Category", "Unit")]
[ExcludeFromCodeCoverage]
public sealed class CustomTypeProviderTests : IDisposable
{
    private bool _disposed;


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    private CustomTypeProvider CreateProvider()
    {
        return new CustomTypeProvider(ParsingConfig.Default, null);
    }

    [Fact]
    public void GetCustomTypes_StateUnderTest_ExpectedBehavior()
    {
        // Arrange
        var unitUnderTest = CreateProvider();

        // Act
        var result = unitUnderTest.GetCustomTypes();

        // Assert
        Assert.NotEmpty(result);
    }
}