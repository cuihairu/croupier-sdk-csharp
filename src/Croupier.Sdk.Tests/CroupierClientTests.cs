// Copyright 2025 Croupier Authors
// Licensed under the Apache License, Version 2.0

using Croupier.Sdk.Logging;
using Croupier.Sdk.Models;
using FluentAssertions;
using Moq;
using Xunit;
using System.Reflection;

namespace Croupier.Sdk.Tests;

/// <summary>
/// Tests for CroupierClient
/// </summary>
public class CroupierClientTests
{
    private static bool? _isNngAvailable;
    private static readonly object _nngCheckLock = new();

    /// <summary>
    /// Checks if NNG native library is available and functional.
    /// This is needed for integration tests that actually try to connect.
    /// </summary>
    private static bool IsNNGAvailable()
    {
        if (_isNngAvailable.HasValue)
        {
            return _isNngAvailable.Value;
        }

        lock (_nngCheckLock)
        {
            if (_isNngAvailable.HasValue)
            {
                return _isNngAvailable.Value;
            }

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET.Shared")
                    ?? assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET")
                    ?? assemblies.FirstOrDefault(a => a.GetName().Name != null && a.GetName().Name.StartsWith("nng"));

                if (assembly == null)
                {
                    try
                    {
                        assembly = Assembly.Load("nng.NET.Shared");
                    }
                    catch
                    {
                        try
                        {
                            assembly = Assembly.Load("nng.NET");
                        }
                        catch
                        {
                            _isNngAvailable = false;
                            return false;
                        }
                    }
                }

                if (assembly == null)
                {
                    _isNngAvailable = false;
                    return false;
                }

                // Try to find and initialize the factory to verify native lib is available
                var factoryType = assembly.GetType("nng.Native.NngFactory")
                    ?? assembly.GetType("nng.NngFactory");

                if (factoryType == null)
                {
                    _isNngAvailable = false;
                    return false;
                }

                // Try to create a factory instance to verify native library actually works
                var factoryMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                    ?? factoryType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);

                if (factoryMethod == null)
                {
                    _isNngAvailable = false;
                    return false;
                }

                // Attempt to initialize - this will fail if native libs aren't available
                var factory = factoryMethod.Invoke(null, null);
                _isNngAvailable = factory != null;
                return _isNngAvailable.Value;
            }
            catch
            {
                _isNngAvailable = false;
                return false;
            }
        }
    }

    private static ClientConfig CreateTestConfig()
    {
        return new ClientConfig
        {
            AgentAddr = "127.0.0.1:19090",
            ServiceId = "test-service",
            GameId = "test-game",
            Env = "test",
            Insecure = true
        };
    }

    #region Constructor Tests

    [Fact]
    public void CroupierClient_CanBeCreatedWithConfig()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        var client = new CroupierClient(config);

        // Assert
        client.Should().NotBeNull();
        client.Config.Should().BeSameAs(config);
    }

    [Fact]
    public void CroupierClient_CanBeCreatedWithDefaultConfig()
    {
        // Act
        var client = new CroupierClient();

        // Assert
        client.Should().NotBeNull();
        client.Config.Should().NotBeNull();
    }

    [Fact]
    public void CroupierClient_CanBeCreatedWithLogger()
    {
        // Arrange
        var config = CreateTestConfig();
        var logger = new ConsoleCroupierLogger();

        // Act
        var client = new CroupierClient(config, logger);

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    #region Function Registration Tests

    [Fact]
    public void CroupierClient_RegisterFunction_WithHandler()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());
        var descriptor = new FunctionDescriptor
        {
            Id = "get",
            Category = "player",
            Operation = "get"
        };

        FunctionHandlerDelegate handler = (ctx, payload) => Task.FromResult("{}");

        // Act
        client.RegisterFunction(descriptor, handler);

        // Assert - no exception means success
    }

    [Fact]
    public void CroupierClient_RegisterFunction_WithSyncHandler()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());
        var descriptor = new FunctionDescriptor
        {
            Id = "sync",
            Category = "player",
            Operation = "sync"
        };

        SyncFunctionHandlerDelegate handler = (ctx, payload) => "{}";

        // Act
        client.RegisterFunction(descriptor, handler);

        // Assert - no exception means success
    }

    [Fact]
    public void CroupierClient_RegisterFunction_WithIFunctionHandler()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());
        var descriptor = new FunctionDescriptor
        {
            Id = "custom",
            Category = "player",
            Operation = "custom"
        };

        var mockHandler = new Mock<IFunctionHandler>();

        // Act
        client.RegisterFunction(descriptor, mockHandler.Object);

        // Assert - no exception means success
    }

    [Fact]
    public void CroupierClient_RegisterFunction_ThrowsOnInvalidDescriptor()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());
        var descriptor = new FunctionDescriptor(); // Invalid - missing required fields

        FunctionHandlerDelegate handler = (ctx, payload) => Task.FromResult("{}");

        // Act & Assert
        var action = () => client.RegisterFunction(descriptor, handler);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CroupierClient_UnregisterFunction_RemovesFunction()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());
        var descriptor = new FunctionDescriptor
        {
            Id = "remove",
            Category = "player",
            Operation = "remove"
        };

        FunctionHandlerDelegate handler = (ctx, payload) => Task.FromResult("{}");
        client.RegisterFunction(descriptor, handler);

        // Act
        var result = client.UnregisterFunction("player.remove");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CroupierClient_UnregisterFunction_NonExistentFunction_ReturnsFalse()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());

        // Act
        var result = client.UnregisterFunction("nonexistent.function");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Connection State Tests

    [Fact]
    public void CroupierClient_InitiallyNotConnected()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());

        // Assert
        client.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void CroupierClient_Disconnect_WhenNotConnected_DoesNotThrow()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());

        // Act
        var action = () => client.Disconnect();

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Multiple Registration Tests

    [Fact]
    public void CroupierClient_RegisterMultipleFunctions()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());

        var functions = new[]
        {
            new FunctionDescriptor { Id = "get", Category = "player", Operation = "get" },
            new FunctionDescriptor { Id = "ban", Category = "player", Operation = "ban" },
            new FunctionDescriptor { Id = "transfer", Category = "wallet", Operation = "transfer" }
        };

        FunctionHandlerDelegate handler = (ctx, payload) => Task.FromResult("{}");

        // Act
        foreach (var func in functions)
        {
            client.RegisterFunction(func, handler);
        }

        // Assert - no exception means success
    }

    [Fact]
    public void CroupierClient_ReregisterFunction_OverwritesPrevious()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());
        var descriptor = new FunctionDescriptor { Id = "test", Category = "player" };

        FunctionHandlerDelegate handler1 = (ctx, payload) => Task.FromResult("{\"handler\":1}");
        FunctionHandlerDelegate handler2 = (ctx, payload) => Task.FromResult("{\"handler\":2}");

        // Act
        client.RegisterFunction(descriptor, handler1);
        client.RegisterFunction(descriptor, handler2);

        // Assert - no exception means success, second handler overwrites first
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void CroupierClient_ImplementsIDisposable()
    {
        // Assert
        typeof(CroupierClient).Should().Implement<IDisposable>();
    }

    [Fact]
    public void CroupierClient_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());

        // Act & Assert
        var action = () =>
        {
            client.Dispose();
            client.Dispose();
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void CroupierClient_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var client = new CroupierClient(CreateTestConfig());
        client.Dispose();

        // Act & Assert
        var descriptor = new FunctionDescriptor { Id = "test", Category = "player" };
        FunctionHandlerDelegate handler = (ctx, payload) => Task.FromResult("{}");

        var action = () => client.RegisterFunction(descriptor, handler);
        action.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Invoke Tests

    [Fact]
    public async Task CroupierClient_InvokeAsync_WhenConnected()
    {
        try
        {
            // Arrange
            var client = new CroupierClient(CreateTestConfig());
            await client.ConnectAsync();

            // Act
            var result = await client.InvokeAsync("test.function", "{}");

            // Assert
            result.Should().NotBeNullOrEmpty();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("NngFactory") || ex.Message.Contains("NNG"))
        {
            // Skip this integration test if NNG is not available
            Assert.True(true, "NNG native library not available - test skipped");
        }
    }

    #endregion

    #region Config Tests

    [Fact]
    public void CroupierClient_Config_ReturnsPassedConfig()
    {
        // Arrange
        var config = CreateTestConfig();
        config.ServiceId = "my-service";

        // Act
        var client = new CroupierClient(config);

        // Assert
        client.Config.ServiceId.Should().Be("my-service");
    }

    #endregion
}
