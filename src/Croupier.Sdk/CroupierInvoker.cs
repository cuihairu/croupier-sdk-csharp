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
using Croupier.Sdk.Logging;
using Croupier.Sdk.Models;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Croupier.Sdk;

/// <summary>
/// Croupier 调用器 - 用于调用远程注册的函数
/// </summary>
public class CroupierInvoker : IDisposable
{
    private readonly string _agentAddr;
    private readonly string _gameId;
    private readonly string _env;
    private readonly ICroupierLogger _logger;
    private readonly bool _insecure;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels;

    private bool _isDisposed;

    /// <summary>
    /// Agent 地址
    /// </summary>
    public string AgentAddr => _agentAddr;

    /// <summary>
    /// 游戏 ID
    /// </summary>
    public string GameId => _gameId;

    /// <summary>
    /// 环境
    /// </summary>
    public string Env => _env;

    /// <summary>
    /// 创建调用器实例
    /// </summary>
    /// <param name="agentAddr">Agent 地址</param>
    /// <param name="gameId">游戏 ID</param>
    /// <param name="env">环境</param>
    /// <param name="insecure">是否使用不安全连接</param>
    /// <param name="logger">日志记录器</param>
    public CroupierInvoker(
        string agentAddr = "127.0.0.1:19090",
        string? gameId = null,
        string? env = null,
        bool insecure = false,
        ICroupierLogger? logger = null)
    {
        _agentAddr = agentAddr;
        _gameId = gameId ?? "default-game";
        _env = env ?? "dev";
        _insecure = insecure;
        _logger = logger ?? new ConsoleCroupierLogger("Invoker");
        _channels = new ConcurrentDictionary<string, GrpcChannel>();

        _logger.LogInfo("CroupierInvoker", $"Invoker created for {_gameId}/{_env}");
    }

    /// <summary>
    /// 从客户端配置创建调用器
    /// </summary>
    /// <param name="config">客户端配置</param>
    /// <param name="logger">日志记录器</param>
    public CroupierInvoker(ClientConfig config, ICroupierLogger? logger = null)
        : this(
            config.AgentAddr,
            config.GameId,
            config.Env,
            config.Insecure,
            logger)
    {
    }

    /// <summary>
    /// 创建带 ILogger 的调用器
    /// </summary>
    /// <param name="agentAddr">Agent 地址</param>
    /// <param name="gameId">游戏 ID</param>
    /// <param name="env">环境</param>
    /// <param name="logger">Microsoft ILogger</param>
    public CroupierInvoker(
        string agentAddr,
        string gameId,
        string env,
        ILogger logger)
        : this(agentAddr, gameId, env, false, new CroupierLogger(logger))
    {
    }

    /// <summary>
    /// 调用远程函数
    /// </summary>
    /// <param name="functionId">函数 ID（格式: category.entity.operation）</param>
    /// <param name="payload">请求负载（JSON 字符串）</param>
    /// <param name="options">调用选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调用结果</returns>
    public async Task<InvokeResult> InvokeAsync(
        string functionId,
        string payload,
        InvokeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        options ??= new InvokeOptions
        {
            GameId = _gameId,
            Env = _env
        };

        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogDebug("CroupierInvoker", $"Invoking {functionId}");

            // TODO: 实现实际的 gRPC 调用
            // 1. 获取或创建 gRPC 通道
            // 2. 创建 FunctionService 客户端
            // 3. 调用 Invoke 方法
            // 4. 处理响应

            await Task.Delay(10, cancellationToken); // 模拟网络延迟

            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogDebug("CroupierInvoker", $"Invoke {functionId} completed ({duration}ms)");

            return InvokeResult.Succeeded("{\"status\":\"ok\"}", duration);
        }
        catch (OperationCanceledException)
        {
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogWarning("CroupierInvoker", $"Invoke {functionId} canceled");
            return InvokeResult.Failed("Operation canceled", "CANCELED", duration);
        }
        catch (Exception ex)
        {
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError("CroupierInvoker", $"Invoke {functionId} failed: {ex.Message}", ex);
            return InvokeResult.Failed(ex.Message, null, duration);
        }
    }

    /// <summary>
    /// 批量调用多个函数
    /// </summary>
    /// <param name="requests">请求列表</param>
    /// <param name="options">调用选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调用结果列表</returns>
    public async Task<List<InvokeResult>> BatchInvokeAsync(
        List<BatchInvokeRequest> requests,
        InvokeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogDebug("CroupierInvoker", $"Batch invoking {requests.Count} functions");

        var results = new List<InvokeResult>();
        var tasks = requests.Select(r =>
            InvokeAsync(r.FunctionId, r.Payload, options, cancellationToken));

        var invokeResults = await Task.WhenAll(tasks);
        results.AddRange(invokeResults);

        return results;
    }

    /// <summary>
    /// 启动异步任务（不需要等待响应的调用）
    /// </summary>
    /// <param name="functionId">函数 ID</param>
    /// <param name="payload">请求负载</param>
    /// <param name="options">调用选项</param>
    /// <returns>任务 ID</returns>
    public async Task<string> StartJobAsync(
        string functionId,
        string payload,
        InvokeOptions? options = null)
    {
        ThrowIfDisposed();

        options ??= new InvokeOptions
        {
            GameId = _gameId,
            Env = _env
        };

        _logger.LogDebug("CroupierInvoker", $"Starting job: {functionId}");

        // TODO: 实现实际的异步任务启动
        await Task.CompletedTask;

        var jobId = $"job_{DateTime.UtcNow.Ticks}";
        _logger.LogInfo("CroupierInvoker", $"Job started: {jobId}");

        return jobId;
    }

    /// <summary>
    /// 取消正在运行的任务
    /// </summary>
    /// <param name="jobId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功取消</returns>
    public async Task<bool> CancelJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogDebug("CroupierInvoker", $"Canceling job: {jobId}");

        // TODO: 实现实际的任务取消
        await Task.CompletedTask;

        return true;
    }

    /// <summary>
    /// 获取任务状态
    /// </summary>
    /// <param name="jobId">任务 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务状态</returns>
    public async Task<JobStatus?> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // TODO: 实现实际的任务状态查询
        await Task.CompletedTask;

        return new JobStatus
        {
            JobId = jobId,
            Status = "running",
            Progress = 0.5
        };
    }

    /// <summary>
    /// 获取或创建 gRPC 通道
    /// </summary>
    private GrpcChannel GetOrCreateChannel()
    {
        return _channels.GetOrAdd(_agentAddr, addr =>
        {
            var options = new GrpcChannelOptions
            {
                MaxSendMessageSize = 4 * 1024 * 1024,
                MaxReceiveMessageSize = 4 * 1024 * 1024
            };

            if (_insecure)
            {
                options.HttpHandler = new Grpc.Core.ChannelOption[]
                {
                    new Grpc.Core.ChannelOption("grpc.ssl_target_name_override", addr.Split(':')[0]),
                    new Grpc.Core.ChannelOption("grpc.default_authority", addr.Split(':')[0])
                };
            }

            _logger.LogDebug("CroupierInvoker", $"Creating gRPC channel to {addr}");
            return GrpcChannel.ForAddress(_insecure ? $"http://{addr}" : $"https://{addr}", options);
        });
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CroupierInvoker));
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _logger.LogInfo("CroupierInvoker", "Disposing...");

        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
        _channels.Clear();

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 批量调用请求
/// </summary>
public class BatchInvokeRequest
{
    /// <summary>
    /// 函数 ID
    /// </summary>
    public required string FunctionId { get; init; }

    /// <summary>
    /// 请求负载
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// 幂等性键（可选）
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// 任务状态
/// </summary>
public class JobStatus
{
    /// <summary>
    /// 任务 ID
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// 状态: pending, running, completed, failed, canceled
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 进度 (0.0 - 1.0)
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 结果数据（如果完成）
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; init; }
}
