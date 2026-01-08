# Unity 集成

## 支持

Croupier C# SDK 支持 Unity 2021.3+。

## 安装

### 方式一：UPM 包

在 Unity 项目的 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.croupier.sdk": "https://github.com/cuihairu/croupier-sdk-csharp.git?path=src/Unity"
  }
}
```

### 方式二：直接复制 DLL

1. 构建 SDK 项目：
   ```bash
   dotnet build -c Release
   ```

2. 将生成的 DLL 复制到 Unity 项目的 `Assets/Plugins/` 目录：
   - `Croupier.Sdk.dll`
   - `Google.Protobuf.dll`
   - `Grpc.Net.Client.dll`

## MonoBehaviour 封装

```csharp
using UnityEngine;
using Croupier.Sdk;
using Croupier.Sdk.Models;

public class CroupierBehaviour : MonoBehaviour
{
    private CroupierClient _client;

    [Header("Configuration")]
    [SerializeField] private string agentAddr = "127.0.0.1:19090";
    [SerializeField] private string gameId = "my-game";
    [SerializeField] private string env = "dev";

    async void Start()
    {
        var config = new ClientConfig
        {
            AgentAddr = agentAddr,
            GameId = gameId,
            Env = env
        };

        _client = new CroupierClient(config);

        // 注册 Unity 相关函数
        RegisterUnityFunctions();

        await _client.ConnectAsync();
        Debug.Log("Croupier connected");
    }

    void RegisterUnityFunctions()
    {
        _client.RegisterFunction(new FunctionDescriptor
        {
            Id = "unity.spawn",
            Version = "1.0.0",
            Category = "unity",
            Risk = "low"
        }, async (context, payload) =>
        {
            // Unity 主线程调度
            var tcs = new TaskCompletionSource<string>();

            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    var result = SpawnEntity(payload);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return await tcs.Task;
        });
    }

    void OnDestroy()
    {
        _client?.Stop();
        _client?.Disconnect();
    }
}
```

## 主线程调度

Unity 的 API 只能在主线程调用，需要使用调度器：

```csharp
public static class MainThreadDispatcher
{
    private static readonly Queue<System.Action> _executionQueue =
        new Queue<System.Action>();
    private static readonly object _lock = new object();

    public static void Enqueue(System.Action action)
    {
        lock (_lock)
        {
            _executionQueue.Enqueue(action);
        }
    }

    public void Update()
    {
        lock (_lock)
        {
            while (_executionQueue.Count > 0)
            {
                var action = _executionQueue.Dequeue();
                action?.Invoke();
            }
        }
    }
}
```

## 编辑器工具

```csharp
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CroupierBehaviour))]
public class CroupierBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var behaviour = (CroupierBehaviour)target;

        if (GUILayout.Button("Test Connection"))
        {
            behaviour.TestConnection();
        }
    }
}
```

## 注意事项

1. **线程安全**：Unity API 只能在主线程调用
2. **生命周期**：在 `OnDestroy` 中清理连接
3. **打包设置**：确保 .NET 4.x 或 .NET Standard 2.0 兼容性
4. **网络权限**：在 `PlayerSettings` 中设置允许网络访问
