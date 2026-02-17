// Copyright 2025 Croupier Authors
// Licensed under the Apache License, Version 2.0

using Croupier.Sdk.Logging;
using Croupier.Sdk.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Croupier.Sdk.Tests;

/// <summary>
/// Tests for CroupierInvoker
/// </summary>
public class CroupierInvokerTests
{
    private static ClientConfig CreateTestConfig()
    {
        return new ClientConfig
        {
            AgentAddr = "127.0.0.1:19090",
            ServiceId = "test-invoker",
            GameId = "test-game",
            Env = "test",
            Insecure = true
        };
    }

    #region Constructor Tests

    [Fact]
    public void CroupierInvoker_CanBeCreatedWithConfig()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        var invoker = new CroupierInvoker(config);

        // Assert
        invoker.Should().NotBeNull();
        invoker.AgentAddr.Should().Be("127.0.0.1:19090");
        invoker.GameId.Should().Be("test-game");
        invoker.Env.Should().Be("test");
    }

    [Fact]
    public void CroupierInvoker_CanBeCreatedWithLogger()
    {
        // Arrange
        var config = CreateTestConfig();
        var logger = new ConsoleCroupierLogger();

        // Act
        var invoker = new CroupierInvoker(config, logger);

        // Assert
        invoker.Should().NotBeNull();
    }

    [Fact]
    public void CroupierInvoker_CanBeCreatedWithDefaultParameters()
    {
        // Act
        var invoker = new CroupierInvoker();

        // Assert
        invoker.Should().NotBeNull();
        invoker.AgentAddr.Should().Be("127.0.0.1:19090");
        invoker.GameId.Should().Be("default-game");
        invoker.Env.Should().Be("dev");
    }

    [Fact]
    public void CroupierInvoker_CanBeCreatedWithCustomParameters()
    {
        // Act
        var invoker = new CroupierInvoker(
            agentAddr: "192.168.1.100:9090",
            gameId: "custom-game",
            env: "production",
            timeoutMs: 10000);

        // Assert
        invoker.AgentAddr.Should().Be("192.168.1.100:9090");
        invoker.GameId.Should().Be("custom-game");
        invoker.Env.Should().Be("production");
    }

    #endregion

    #region InvokeOptions Tests

    [Fact]
    public void InvokeOptions_Default_HasReasonableTimeout()
    {
        // Arrange
        var options = new InvokeOptions();

        // Assert
        options.TimeoutSeconds.Should().BeGreaterThan(0);
        options.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void InvokeOptions_CustomTimeout_IsRespected()
    {
        // Arrange
        var options = new InvokeOptions { TimeoutSeconds = 120 };

        // Assert
        options.TimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void InvokeOptions_IdempotencyKey_CanBeGenerated()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var options = new InvokeOptions { IdempotencyKey = key };

        // Assert
        options.IdempotencyKey.Should().Be(key);
        options.IdempotencyKey.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void CroupierInvoker_ImplementsIDisposable()
    {
        // Assert
        typeof(CroupierInvoker).Should().Implement<IDisposable>();
    }

    [Fact]
    public void CroupierInvoker_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());

        // Act & Assert
        var action = () =>
        {
            invoker.Dispose();
            invoker.Dispose();
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void CroupierInvoker_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());
        invoker.Dispose();

        // Act & Assert
        var action = () => invoker.InvokeAsync("test.func", "{}").GetAwaiter().GetResult();
        action.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Invoke Tests

    [Fact]
    public async Task CroupierInvoker_InvokeAsync_ReturnsResult()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());

        // Act
        var result = await invoker.InvokeAsync("test.function", "{\"input\":\"test\"}");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CroupierInvoker_InvokeAsync_WithOptions()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());
        var options = new InvokeOptions
        {
            GameId = "custom-game",
            Env = "staging",
            TimeoutSeconds = 60,
            IdempotencyKey = "test-key"
        };

        // Act
        var result = await invoker.InvokeAsync("test.function", "{}", options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CroupierInvoker_InvokeAsync_CanBeCanceled()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await invoker.InvokeAsync("test.function", "{}", cancellationToken: cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CANCELED");
    }

    #endregion

    #region BatchInvoke Tests

    [Fact]
    public async Task CroupierInvoker_BatchInvokeAsync_ReturnsResults()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());
        var requests = new List<BatchInvokeRequest>
        {
            new() { FunctionId = "func1", Payload = "{\"id\":1}" },
            new() { FunctionId = "func2", Payload = "{\"id\":2}" },
            new() { FunctionId = "func3", Payload = "{\"id\":3}" }
        };

        // Act
        var results = await invoker.BatchInvokeAsync(requests);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Success);
    }

    #endregion

    #region Job Tests

    [Fact]
    public async Task CroupierInvoker_StartJobAsync_ReturnsJobId()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());

        // Act
        var jobId = await invoker.StartJobAsync("long.running.function", "{}");

        // Assert
        jobId.Should().NotBeNullOrEmpty();
        jobId.Should().StartWith("job_");
    }

    [Fact]
    public async Task CroupierInvoker_CancelJobAsync_ReturnsSuccess()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());

        // Act
        var result = await invoker.CancelJobAsync("job_123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CroupierInvoker_GetJobStatusAsync_ReturnsStatus()
    {
        // Arrange
        var invoker = new CroupierInvoker(CreateTestConfig());

        // Act
        var status = await invoker.GetJobStatusAsync("job_123");

        // Assert
        status.Should().NotBeNull();
        status!.JobId.Should().Be("job_123");
        status.Status.Should().Be("running");
        status.Progress.Should().Be(0.5);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void InvokeOptions_Metadata_CanContainMultipleValues()
    {
        // Arrange
        var options = new InvokeOptions
        {
            Metadata = new Dictionary<string, string>
            {
                ["X-Request-Id"] = Guid.NewGuid().ToString(),
                ["X-Correlation-Id"] = "corr-123",
                ["X-User-Id"] = "user-456",
                ["Authorization"] = "Bearer token123"
            }
        };

        // Assert
        options.Metadata.Should().HaveCount(4);
        options.Metadata.Should().ContainKey("Authorization");
    }

    #endregion
}
