# API 参考

## 核心类

### CroupierClient

连接 Agent 并注册函数的客户端。

```csharp
public class CroupierClient : IDisposable
{
    // 构造函数
    public CroupierClient(ClientConfig? config = null, ICroupierLogger? logger = null)

    // 属性
    public ClientConfig Config { get; }
    public bool IsConnected { get; }
    public string? LocalAddress { get; }

    // 方法
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    public void Disconnect()
    public Task ServeAsync(CancellationToken cancellationToken = default)
    public void Stop()
    public void RegisterFunction(FunctionDescriptor descriptor, IFunctionHandler handler)
    public void RegisterFunction(FunctionDescriptor descriptor, FunctionHandlerDelegate handler)
    public void RegisterFunction(FunctionDescriptor descriptor, SyncFunctionHandlerDelegate handler)
    public bool UnregisterFunction(string functionId)
    public void Dispose()
}
```

### CroupierInvoker

调用远程注册的函数。

```csharp
public class CroupierInvoker : IDisposable
{
    // 构造函数
    public CroupierInvoker(string agentAddr, string? gameId = null, string? env = null,
                          bool insecure = false, ICroupierLogger? logger = null)

    // 属性
    public string AgentAddr { get; }
    public string GameId { get; }
    public string Env { get; }

    // 方法
    public Task<InvokeResult> InvokeAsync(string functionId, string payload,
                                          InvokeOptions? options = null,
                                          CancellationToken cancellationToken = default)
    public Task<List<InvokeResult>> BatchInvokeAsync(List<BatchInvokeRequest> requests,
                                                     InvokeOptions? options = null,
                                                     CancellationToken cancellationToken = default)
    public Task<string> StartJobAsync(string functionId, string payload,
                                       InvokeOptions? options = null)
    public Task<bool> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    public void Dispose()
}
```

## 模型类

### FunctionDescriptor

函数描述符，用于注册函数。

```csharp
public class FunctionDescriptor
{
    public string Id { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string Category { get; set; }
    public string Risk { get; set; } = "medium";
    public string? Entity { get; set; }
    public string? Operation { get; set; }
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? InputSchema { get; set; }
    public string? OutputSchema { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    public bool IsValid()
    public string GetFullName()
}
```

### ClientConfig

客户端配置。

```csharp
public class ClientConfig
{
    public string AgentAddr { get; set; } = "127.0.0.1:19090";
    public string ServiceId { get; set; } = "csharp-service";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string GameId { get; set; } = "default-game";
    public string Env { get; set; } = "dev";
    public string LocalAddr { get; set; } = "0.0.0.0:0";
    public bool Insecure { get; set; }
    public string? CertFile { get; set; }
    public string? KeyFile { get; set; }
    public string? CaFile { get; set; }
    public string? ServerName { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    // ... 更多配置选项
}
```

### InvokeOptions / InvokeResult

```csharp
public class InvokeOptions
{
    public string? GameId { get; set; }
    public string? Env { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string? IdempotencyKey { get; set; }
    public string? RequestId { get; set; }
    public string? UserId { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class InvokeResult
{
    public bool Success { get; init; }
    public string? Data { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public long DurationMs { get; init; }

    public static InvokeResult Succeeded(string data, long durationMs = 0)
    public static InvokeResult Failed(string error, string? errorCode = null, long durationMs = 0)
}
```

## 委托和接口

```csharp
// 异步处理器委托
public delegate Task<string> FunctionHandlerDelegate(
    FunctionContext context,
    string payload);

// 同步处理器委托
public delegate string SyncFunctionHandlerDelegate(
    FunctionContext context,
    string payload);

// 函数处理器接口
public interface IFunctionHandler
{
    Task<string> HandleAsync(FunctionContext context, string payload);
}
```
