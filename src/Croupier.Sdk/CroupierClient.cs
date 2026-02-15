// Copyright 2025 Croupier Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Concurrent;
using System.Threading.Channels;
using Croupier.Sdk.Logging;
using Croupier.Sdk.Models;
using Croupier.Sdk.Transport;
using Croupier.Sdk.V1;
using Microsoft.Extensions.Logging;

namespace Croupier.Sdk;

/// <summary>
/// Croupier 客户端 - 用于连接 Agent 并注册/调用函数
/// </summary>
public partial class CroupierClient : IDisposable
{
    private readonly ClientConfig _config;
    private readonly ICroupierLogger _logger;
    private readonly ConcurrentDictionary<string, IFunctionHandler> _handlers;
    private readonly ConcurrentDictionary<string, FunctionDescriptor> _descriptors;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly Channel<FunctionCallTask> _callChannel;

    private NNGTransport? _transport;
    private NNGServer? _server;
    private bool _isConnected;
    private bool _isDisposed;
    private Task? _processTask;

    /// <summary>
    /// 客户端配置
    /// </summary>
    public ClientConfig Config => _config;

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _isConnected && _transport != null;

    /// <summary>
    /// 本地监听地址
    /// </summary>
    public string? LocalAddress { get; private set; }

    /// <summary>
    /// 创建 Croupier 客户端实例
    /// </summary>
    /// <param name="config">客户端配置</param>
    /// <param name="logger">日志记录器（可选）</param>
    public CroupierClient(ClientConfig? config = null, ICroupierLogger? logger = null)
    {
        _config = config ?? new ClientConfig();
        _logger = logger ?? new ConsoleCroupierLogger();
        _handlers = new ConcurrentDictionary<string, IFunctionHandler>();
        _descriptors = new ConcurrentDictionary<string, FunctionDescriptor>();
        _shutdownCts = new CancellationTokenSource();
        _callChannel = Channel.CreateUnbounded<FunctionCallTask>();

        _logger.LogInfo("CroupierClient", $"Client created for service: {_config.ServiceId}");
    }

    /// <summary>
    /// 创建带 ILogger 的 Croupier 客户端实例
    /// </summary>
    /// <param name="config">客户端配置</param>
    /// <param name="logger">Microsoft ILogger</param>
    public CroupierClient(ClientConfig config, ILogger logger)
        : this(config, new CroupierLogger(logger))
    {
    }

    /// <summary>
    /// 连接到 Agent
    /// </summary>
    /// <exception cref="InvalidOperationException">连接失败时抛出</exception>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isConnected)
        {
            _logger.LogInfo("CroupierClient", "Already connected");
            return;
        }

        _logger.LogInfo("CroupierClient", $"Connecting to Agent at {_config.AgentAddr}...");

        try
        {
            // Create NNG transport
            var address = _config.AgentAddr.StartsWith("tcp://") ? _config.AgentAddr : $"tcp://{_config.AgentAddr}";
            _transport = new NNGTransport(address, _config.TimeoutSeconds * 1000, _logger);
            _transport.Connect();

            // TODO: Implement service registration via NNG

            _isConnected = true;
            LocalAddress = _config.LocalAddr;

            _logger.LogInfo("CroupierClient", $"Connected successfully. Local address: {LocalAddress}");

            // Start message processing loop
            _processTask = Task.Run(() => ProcessCallsAsync(_shutdownCts.Token), cancellationToken);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError("CroupierClient", $"Failed to connect: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        ThrowIfDisposed();

        if (!_isConnected)
            return;

        _logger.LogInfo("CroupierClient", "Disconnecting...");

        _shutdownCts.Cancel();
        _isConnected = false;

        try
        {
            _processTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _transport?.Dispose();
        _transport = null;
        _server?.Dispose();
        _server = null;

        _logger.LogInfo("CroupierClient", "Disconnected");
    }

    /// <summary>
    /// 注册函数处理器
    /// </summary>
    /// <param name="descriptor">函数描述符</param>
    /// <param name="handler">函数处理器</param>
    /// <exception cref="ArgumentException">描述符无效时抛出</exception>
    public void RegisterFunction(FunctionDescriptor descriptor, IFunctionHandler handler)
    {
        ThrowIfDisposed();

        if (!descriptor.IsValid())
            throw new ArgumentException("Invalid function descriptor", nameof(descriptor));

        var fullName = descriptor.GetFullName();

        if (!_handlers.TryAdd(fullName, handler))
        {
            _logger.LogWarning("CroupierClient", $"Function {fullName} already registered, replacing");
            _handlers[fullName] = handler;
        }

        _descriptors[fullName] = descriptor;

        _logger.LogInfo("CroupierClient", $"Registered function: {fullName} (version: {descriptor.Version})");
    }

    /// <summary>
    /// 注册函数处理器（委托版本）
    /// </summary>
    /// <param name="descriptor">函数描述符</param>
    /// <param name="handler">函数处理器委托</param>
    public void RegisterFunction(FunctionDescriptor descriptor, FunctionHandlerDelegate handler)
    {
        RegisterFunction(descriptor, new DelegateFunctionHandler(handler));
    }

    /// <summary>
    /// 注册函数处理器（同步委托版本）
    /// </summary>
    /// <param name="descriptor">函数描述符</param>
    /// <param name="handler">同步函数处理器委托</param>
    public void RegisterFunction(FunctionDescriptor descriptor, SyncFunctionHandlerDelegate handler)
    {
        RegisterFunction(descriptor, new SyncDelegateFunctionHandler(handler));
    }

    /// <summary>
    /// 取消注册函数
    /// </summary>
    /// <param name="functionId">函数 ID</param>
    /// <returns>是否成功取消注册</returns>
    public bool UnregisterFunction(string functionId)
    {
        ThrowIfDisposed();

        if (_handlers.TryRemove(functionId, out _))
        {
            _descriptors.TryRemove(functionId, out _);
            _logger.LogInfo("CroupierClient", $"Unregistered function: {functionId}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 启动服务（开始接收函数调用）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ServeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isConnected)
            await ConnectAsync(cancellationToken);

        _logger.LogInfo("CroupierClient", "Starting service...");

        // Start NNG server for receiving function calls from Agent
        var listenAddr = _config.LocalAddr ?? "tcp://127.0.0.1:0";
        _server = new NNGServer(listenAddr, _config.TimeoutSeconds * 1000, _logger);
        _server.RequestReceived += OnRequestReceived;
        _server.Listen();

        LocalAddress = listenAddr;
        _logger.LogInfo("CroupierClient", $"Server listening on: {LocalAddress}");

        // Wait for shutdown signal
        await Task.Delay(Timeout.Infinite, cancellationToken);

        _logger.LogInfo("CroupierClient", "Service stopped");
    }

    /// <summary>
    /// Handle incoming request from Agent.
    /// </summary>
    private void OnRequestReceived(object? sender, RequestReceivedEventArgs e)
    {
        _logger.LogDebug("CroupierClient", $"Received request type={Protocol.MsgIdString(e.MsgId)}");

        try
        {
            if (e.MsgId == Protocol.MsgInvokeRequest)
            {
                var request = InvokeRequest.Parser.ParseFrom(e.Body);
                var task = new FunctionCallTask
                {
                    FunctionId = request.FunctionId,
                    CallId = Guid.NewGuid().ToString("N"),
                    GameId = _config.GameId,
                    Env = _config.Env,
                    Payload = request.Payload.ToStringUtf8(),
                    IdempotencyKey = request.IdempotencyKey
                };

                var result = ProcessFunctionCallAsync(task).GetAwaiter().GetResult();

                var response = new InvokeResponse
                {
                    Payload = Google.Protobuf.ByteString.CopyFromUtf8(result)
                };
                e.Response = response.ToByteArray();
            }
            else
            {
                _logger.LogWarning("CroupierClient", $"Unknown message type: {e.MsgId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("CroupierClient", $"Error handling request: {ex.Message}", ex);
            var errorResponse = new InvokeResponse
            {
                Payload = Google.Protobuf.ByteString.CopyFromUtf8($"{{\"error\":\"{ex.Message}\"}}")
            };
            e.Response = errorResponse.ToByteArray();
        }
    }

    /// <summary>
    /// 调用远程函数
    /// </summary>
    /// <param name="functionId">函数 ID</param>
    /// <param name="payload">请求负载（JSON）</param>
    /// <param name="options">调用选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应负载（JSON）</returns>
    public async Task<string> InvokeAsync(
        string functionId,
        string payload,
        InvokeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        options ??= new InvokeOptions
        {
            GameId = _config.GameId,
            Env = _config.Env
        };

        _logger.LogDebug("CroupierInvoker", $"Invoking {functionId}");

        if (_transport == null || !_transport.IsConnected)
        {
            throw new InvalidOperationException("Not connected to Agent");
        }

        // Build protobuf request
        var request = new InvokeRequest
        {
            FunctionId = functionId,
            Payload = Google.Protobuf.ByteString.CopyFromUtf8(payload)
        };

        if (!string.IsNullOrEmpty(options.IdempotencyKey))
        {
            request.IdempotencyKey = options.IdempotencyKey;
        }

        if (options.Metadata != null)
        {
            foreach (var kvp in options.Metadata)
            {
                request.Metadata.Add(kvp.Key, kvp.Value);
            }
        }

        // Send via NNG
        var requestData = request.ToByteArray();
        var responseData = await _transport.CallAsync(
            Protocol.MsgInvokeRequest,
            requestData,
            cancellationToken);

        // Parse response
        var response = InvokeResponse.Parser.ParseFrom(responseData);
        return response.Payload.ToStringUtf8();
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    public void Stop()
    {
        _logger.LogInfo("CroupierClient", "Stopping service...");
        _shutdownCts.Cancel();
    }

    /// <summary>
    /// 处理函数调用
    /// </summary>
    private async Task<string> ProcessFunctionCallAsync(FunctionCallTask task)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogDebug("CroupierClient", $"Processing call: {task.FunctionId}");

            if (!_handlers.TryGetValue(task.FunctionId, out var handler))
            {
                return $"{{\"error\":\"Function not found: {task.FunctionId}\"}}";
            }

            var context = new FunctionContext
            {
                FunctionId = task.FunctionId,
                CallId = task.CallId,
                GameId = task.GameId,
                Env = task.Env,
                UserId = task.UserId,
                Timestamp = startTime.Ticks,
                IdempotencyKey = task.IdempotencyKey,
                CallerServiceId = task.CallerServiceId
            };

            var result = await handler.HandleAsync(context, task.Payload);

            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogDebug("CroupierClient", $"Call completed: {task.FunctionId} ({duration}ms)");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("CroupierClient", $"Call failed: {task.FunctionId} - {ex.Message}", ex);
            return $"{{\"error\":\"{ex.Message}\"}}";
        }
    }

    /// <summary>
    /// 处理函数调用循环
    /// </summary>
    private async Task ProcessCallsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfo("CroupierClient", "Call processor started");

        await foreach (var task in _callChannel.Reader.ReadAllAsync(cancellationToken))
        {
            _ = Task.Run(() => ProcessFunctionCallAsync(task), cancellationToken);
        }

        _logger.LogInfo("CroupierClient", "Call processor stopped");
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CroupierClient));
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _logger.LogInfo("CroupierClient", "Disposing...");

        Disconnect();
        _shutdownCts.Dispose();

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 函数调用任务
    /// </summary>
    private class FunctionCallTask
    {
        public required string FunctionId { get; init; }
        public required string CallId { get; init; }
        public required string GameId { get; init; }
        public required string Env { get; init; }
        public required string Payload { get; init; }
        public string? UserId { get; init; }
        public string? IdempotencyKey { get; init; }
        public string? CallerServiceId { get; init; }
    }
}
