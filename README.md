# Croupier C# SDK

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-0.1.0-orange.svg)](https://github.com/cuihairu/croupier-sdk-csharp)

Croupier SDK for .NET 8+ and Unity 2021+.

## 概述

Croupier C# SDK 提供：

- **CroupierClient** - 连接 Agent 并注册函数
- **CroupierInvoker** - 调用远程注册的函数
- **依赖注入集成** - 支持 Microsoft.Extensions.DependencyInjection
- **Unity 集成** - MonoBehaviour 组件（可选）

## 支持的平台

| 平台 | 支持版本 |
|------|---------|
| .NET | 8.0+ |
| Unity | 2021.3+ |

## 安装

```bash
dotnet add package Croupier.Sdk
```

或从源码构建：

```bash
git clone https://github.com/cuihairu/croupier-sdk-csharp.git
cd croupier-sdk-csharp
dotnet build
```

## 快速开始

### 1. 创建客户端

```csharp
using Croupier.Sdk;
using Croupier.Sdk.Models;

var config = new ClientConfig
{
    AgentAddr = "127.0.0.1:19090",
    ServiceId = "my-service",
    GameId = "my-game",
    Env = "production"
};

var client = new CroupierClient(config);
await client.ConnectAsync();
```

### 2. 注册函数

```csharp
var descriptor = new FunctionDescriptor
{
    Id = "player.get",
    Version = "1.0.0",
    Category = "player",
    Risk = "low",
    DisplayName = "获取玩家信息",
    Description = "根据玩家 ID 获取玩家详细信息"
};

client.RegisterFunction(descriptor, async (context, payload) =>
{
    // 处理函数调用
    var response = new
    {
        status = "success",
        player = new { id = "123", name = "TestPlayer" }
    };

    return System.Text.Json.JsonSerializer.Serialize(response);
});
```

### 3. 调用远程函数

```csharp
var invoker = new CroupierInvoker("127.0.0.1:19090", "my-game", "production");

var result = await invoker.InvokeAsync("player.ban", "{\"player_id\":\"123\"}");

if (result.Success)
{
    Console.WriteLine($"Result: {result.Data}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

## 配置

### 环境变量

| 变量名 | 说明 | 默认值 |
|--------|------|--------|
| `CROUPIER_AGENT_ADDR` | Agent 地址 | 127.0.0.1:19090 |
| `CROUPIER_SERVICE_ID` | 服务 ID | csharp-service |
| `CROUPIER_GAME_ID` | 游戏 ID | default-game |
| `CROUPIER_ENV` | 环境 | dev |
| `CROUPIER_INSECURE` | 跳过 TLS | false |

## 依赖注入

```csharp
using Croupier.Sdk.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddCroupier(options =>
{
    options.AgentAddr = "127.0.0.1:19090";
    options.GameId = "my-game";
    options.Env = "production";
});

var serviceProvider = services.BuildServiceProvider();
var client = serviceProvider.GetRequiredService<CroupierClient>();
```

## 示例

查看 `examples/SimpleService` 目录获取完整示例。

## 许可证

Apache License 2.0
