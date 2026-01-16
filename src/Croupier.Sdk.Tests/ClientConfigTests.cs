// Copyright 2025 Croupier Authors
// Licensed under the Apache License, Version 2.0

using Croupier.Sdk.Models;
using FluentAssertions;
using Xunit;

namespace Croupier.Sdk.Tests;

/// <summary>
/// Tests for ClientConfig model
/// </summary>
public class ClientConfigTests
{
    [Fact]
    public void ClientConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var config = new ClientConfig();

        // Assert
        config.AgentAddr.Should().Be("127.0.0.1:19090");
        config.ServiceId.Should().Be("csharp-service");
        config.ServiceVersion.Should().Be("1.0.0");
        config.GameId.Should().Be("default-game");
        config.Env.Should().Be("dev");
        config.LocalAddr.Should().Be("0.0.0.0:0");
        config.Insecure.Should().BeFalse();
        config.TimeoutSeconds.Should().Be(30);
        config.HeartbeatIntervalSeconds.Should().Be(30);
        config.AutoReconnect.Should().BeTrue();
        config.ReconnectIntervalSeconds.Should().Be(5);
        config.ReconnectMaxAttempts.Should().Be(0);
        config.MaxConcurrentMessages.Should().Be(100);
        config.MaxMessageSize.Should().Be(4 * 1024 * 1024);
    }

    [Fact]
    public void ClientConfig_CustomValues_ShouldBeSettable()
    {
        // Arrange
        var config = new ClientConfig
        {
            AgentAddr = "192.168.1.100:8080",
            ServiceId = "my-service",
            ServiceVersion = "2.0.0",
            GameId = "my-game",
            Env = "production",
            LocalAddr = "0.0.0.0:9000",
            Insecure = true,
            CertFile = "/path/to/cert.pem",
            KeyFile = "/path/to/key.pem",
            CaFile = "/path/to/ca.pem",
            ServerName = "myserver.com",
            TimeoutSeconds = 60,
            HeartbeatIntervalSeconds = 15,
            AutoReconnect = false,
            ReconnectIntervalSeconds = 10,
            ReconnectMaxAttempts = 5,
            MaxConcurrentMessages = 50,
            MaxMessageSize = 8 * 1024 * 1024
        };

        // Assert
        config.AgentAddr.Should().Be("192.168.1.100:8080");
        config.ServiceId.Should().Be("my-service");
        config.ServiceVersion.Should().Be("2.0.0");
        config.GameId.Should().Be("my-game");
        config.Env.Should().Be("production");
        config.LocalAddr.Should().Be("0.0.0.0:9000");
        config.Insecure.Should().BeTrue();
        config.CertFile.Should().Be("/path/to/cert.pem");
        config.KeyFile.Should().Be("/path/to/key.pem");
        config.CaFile.Should().Be("/path/to/ca.pem");
        config.ServerName.Should().Be("myserver.com");
        config.TimeoutSeconds.Should().Be(60);
        config.HeartbeatIntervalSeconds.Should().Be(15);
        config.AutoReconnect.Should().BeFalse();
        config.ReconnectIntervalSeconds.Should().Be(10);
        config.ReconnectMaxAttempts.Should().Be(5);
        config.MaxConcurrentMessages.Should().Be(50);
        config.MaxMessageSize.Should().Be(8 * 1024 * 1024);
    }

    [Theory]
    [InlineData("localhost:19090")]
    [InlineData("192.168.1.1:8080")]
    [InlineData("agent.example.com:443")]
    public void ClientConfig_AgentAddr_AcceptsValidFormats(string addr)
    {
        // Arrange
        var config = new ClientConfig { AgentAddr = addr };

        // Assert
        config.AgentAddr.Should().Be(addr);
    }

    [Fact]
    public void ClientConfig_TlsProperties_AreOptional()
    {
        // Arrange & Act
        var config = new ClientConfig();

        // Assert
        config.CertFile.Should().BeNull();
        config.KeyFile.Should().BeNull();
        config.CaFile.Should().BeNull();
        config.ServerName.Should().BeNull();
    }

    [Fact]
    public void ClientConfig_MaxMessageSize_DefaultIsFourMB()
    {
        // Arrange & Act
        var config = new ClientConfig();

        // Assert
        config.MaxMessageSize.Should().Be(4 * 1024 * 1024);
        config.MaxMessageSize.Should().Be(4194304); // 4MB in bytes
    }
}
