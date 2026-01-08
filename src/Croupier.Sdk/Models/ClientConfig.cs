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

namespace Croupier.Sdk.Models;

/// <summary>
/// 客户端配置
/// </summary>
public class ClientConfig
{
    /// <summary>
    /// Agent 服务器地址
    /// </summary>
    public string AgentAddr { get; set; } = "127.0.0.1:19090";

    /// <summary>
    /// 服务标识符
    /// </summary>
    public string ServiceId { get; set; } = "csharp-service";

    /// <summary>
    /// 服务版本
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// 游戏 ID
    /// </summary>
    public string GameId { get; set; } = "default-game";

    /// <summary>
    /// 环境
    /// </summary>
    public string Env { get; set; } = "dev";

    /// <summary>
    /// 本地监听地址
    /// </summary>
    public string LocalAddr { get; set; } = "0.0.0.0:0";

    /// <summary>
    /// 是否使用不安全连接（跳过 TLS 验证）
    /// </summary>
    public bool Insecure { get; set; }

    /// <summary>
    /// TLS 证书文件路径
    /// </summary>
    public string? CertFile { get; set; }

    /// <summary>
    /// TLS 私钥文件路径
    /// </summary>
    public string? KeyFile { get; set; }

    /// <summary>
    /// TLS CA 证书文件路径
    /// </summary>
    public string? CaFile { get; set; }

    /// <summary>
    /// TLS 服务器名称（用于 SNI）
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// 连接超时（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 心跳间隔（秒）
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 是否自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 重连间隔（秒）
    /// </summary>
    public int ReconnectIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 重连最大尝试次数（0 表示无限重试）
    /// </summary>
    public int ReconnectMaxAttempts { get; set; } = 0;

    /// <summary>
    /// 最大并发消息数
    /// </summary>
    public int MaxConcurrentMessages { get; set; } = 100;

    /// <summary>
    /// 消息最大大小（字节）
    /// </summary>
    public int MaxMessageSize { get; set; } = 4 * 1024 * 1024; // 4MB
}
