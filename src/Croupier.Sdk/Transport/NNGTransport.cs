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

using Croupier.Sdk.Logging;

namespace Croupier.Sdk.Transport;

/// <summary>
/// NNG-based transport client using REQ/REP pattern.
/// Uses nng.NET library for NNG communication.
/// </summary>
public sealed class NNGTransport : IDisposable
{
    private readonly string _address;
    private readonly int _timeoutMs;
    private readonly ICroupierLogger _logger;
    private readonly object _lock = new();

    private dynamic? _factory;
    private dynamic? _socket;
    private bool _connected;
    private int _requestId;
    private bool _isDisposed;

    /// <summary>
    /// Gets whether the transport is connected.
    /// </summary>
    public bool IsConnected => _connected && _socket != null;

    /// <summary>
    /// Initialize NNG transport.
    /// </summary>
    /// <param name="address">NNG address (e.g., "tcp://127.0.0.1:19090")</param>
    /// <param name="timeoutMs">Request timeout in milliseconds</param>
    /// <param name="logger">Logger instance</param>
    public NNGTransport(string address, int timeoutMs = 5000, ICroupierLogger? logger = null)
    {
        _address = address;
        _timeoutMs = timeoutMs;
        _logger = logger ?? new ConsoleCroupierLogger("NNGTransport");
    }

    /// <summary>
    /// Connect to the NNG server (Agent).
    /// </summary>
    public void Connect()
    {
        lock (_lock)
        {
            if (_connected)
            {
                return;
            }

            ThrowIfDisposed();

            _logger.LogInfo("NNGTransport", $"Connecting to NNG server at: {_address}");

            try
            {
                // Initialize NNG using reflection to handle API version differences
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET.Shared")
                    ?? assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET")
                    ?? assemblies.FirstOrDefault(a => a.GetName().Name.StartsWith("nng"));

                if (assembly == null)
                {
                    throw new InvalidOperationException("Failed to find nng.NET assembly. Tried: nng.NET.Shared, nng.NET");
                }

                // Try to find the factory initialization method
                var factoryType = assembly.GetType("nng.Native.NngFactory")
                    ?? assembly.GetType("nng.NngFactory");

                if (factoryType == null)
                {
                    throw new InvalidOperationException("Failed to find NngFactory type");
                }

                // Create factory instance
                var factoryMethod = factoryType.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?? factoryType.GetMethod("Init", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (factoryMethod == null)
                {
                    throw new InvalidOperationException("Failed to find factory creation method");
                }

                _factory = factoryMethod.Invoke(null, null);

                if (_factory == null)
                {
                    throw new InvalidOperationException("Failed to initialize NNG factory");
                }

                // Create REQ socket and dial
                dynamic result = _factory.RequesterOpen().ThenDial(_address);
                if (result.IsOk())
                {
                    _socket = result.Ok();
                }
                else
                {
                    throw new InvalidOperationException($"Failed to connect to {_address}: {result.Err()}");
                }

                _connected = true;
                _logger.LogInfo("NNGTransport", $"Connected to: {_address}");
            }
            catch (Exception ex)
            {
                _logger.LogError("NNGTransport", $"Connection failed: {ex.Message}", ex);
                Cleanup();
                throw;
            }
        }
    }

    /// <summary>
    /// Close the connection.
    /// </summary>
    public void Close()
    {
        lock (_lock)
        {
            if (!_connected)
            {
                return;
            }

            Cleanup();
            _connected = false;
            _logger.LogInfo("NNGTransport", "NNG transport closed");
        }
    }

    private void Cleanup()
    {
        _socket?.Dispose();
        _socket = null;
    }

    /// <summary>
    /// Send a request and wait for response.
    /// </summary>
    /// <param name="msgType">Protocol message type (e.g., MsgInvokeRequest)</param>
    /// <param name="data">Protobuf serialized request body</param>
    /// <returns>Response body bytes</returns>
    public byte[] Call(int msgType, byte[]? data)
    {
        lock (_lock)
        {
            if (!_connected || _socket == null || _factory == null)
            {
                throw new InvalidOperationException("Not connected");
            }

            ThrowIfDisposed();

            // Generate request ID
            _requestId = (_requestId + 1) & 0x7FFFFFFF; // Keep positive for safety

            // Build message with protocol header
            var message = Protocol.NewMessage(msgType, _requestId, data);

            _logger.LogDebug("NNGTransport", $"Sending message type=0x{msgType:X6}, reqId={_requestId}");

            try
            {
                // Create NNG message
                var nngMsg = _factory.CreateMessage();
                nngMsg.Append(message);

                // Send request
                var sendResult = _socket.SendMsg(nngMsg);
                if (!sendResult.IsOk())
                {
                    throw new InvalidOperationException($"Send failed: {sendResult.Err()}");
                }

                // Receive response
                var recvResult = _socket.RecvMsg();
                if (!recvResult.IsOk())
                {
                    throw new InvalidOperationException($"Receive failed: {recvResult.Err()}");
                }

                var responseMsg = recvResult.Ok();

                // Extract response bytes
                var responseData = responseMsg.AsSpan().ToArray();

                // Parse response
                var parsed = Protocol.ParseMessage(responseData);

                _logger.LogDebug("NNGTransport", $"Received response type=0x{parsed.MsgId:X6}, reqId={parsed.ReqId}");

                // Verify request ID matches
                if (parsed.ReqId != _requestId)
                {
                    _logger.LogWarning("NNGTransport", $"Request ID mismatch: expected {_requestId}, got {parsed.ReqId}");
                }

                // Verify response type
                var expectedRespType = Protocol.GetResponseMsgId(msgType);
                if (parsed.MsgId != expectedRespType)
                {
                    throw new InvalidOperationException(
                        $"Unexpected response type: expected 0x{expectedRespType:X6}, got 0x{parsed.MsgId:X6}");
                }

                return parsed.Body;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.LogError("NNGTransport", $"Call failed: {ex.Message}", ex);
                throw new InvalidOperationException($"NNG call failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Send a request asynchronously and wait for response.
    /// </summary>
    /// <param name="msgType">Protocol message type</param>
    /// <param name="data">Protobuf serialized request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response body bytes</returns>
    public Task<byte[]> CallAsync(int msgType, byte[]? data, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Call(msgType, data), cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(NNGTransport));
        }
    }

    /// <summary>
    /// Release resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Close();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// NNG-based server for receiving function calls from Agent.
/// </summary>
public sealed class NNGServer : IDisposable
{
    private readonly string _address;
    private readonly int _timeoutMs;
    private readonly ICroupierLogger _logger;
    private readonly object _lock = new();

    private dynamic? _factory;
    private dynamic? _socket;
    private bool _isListening;
    private bool _isDisposed;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;

    /// <summary>
    /// Gets whether the server is listening.
    /// </summary>
    public bool IsListening => _isListening;

    /// <summary>
    /// Event raised when a request is received.
    /// </summary>
    public event EventHandler<RequestReceivedEventArgs>? RequestReceived;

    /// <summary>
    /// Initialize NNG server.
    /// </summary>
    /// <param name="address">NNG address to listen on (e.g., "tcp://127.0.0.1:0" for auto port)</param>
    /// <param name="timeoutMs">Request timeout in milliseconds</param>
    /// <param name="logger">Logger instance</param>
    public NNGServer(string address, int timeoutMs = 5000, ICroupierLogger? logger = null)
    {
        _address = address;
        _timeoutMs = timeoutMs;
        _logger = logger ?? new ConsoleCroupierLogger("NNGServer");
    }

    /// <summary>
    /// Start listening for connections.
    /// </summary>
    public void Listen()
    {
        lock (_lock)
        {
            if (_isListening)
            {
                return;
            }

            ThrowIfDisposed();

            _logger.LogInfo("NNGServer", $"Starting NNG server at: {_address}");

            try
            {
                // Initialize NNG using reflection to handle API version differences
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET.Shared")
                    ?? assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET")
                    ?? assemblies.FirstOrDefault(a => a.GetName().Name.StartsWith("nng"));

                if (assembly == null)
                {
                    throw new InvalidOperationException("Failed to find nng.NET assembly. Tried: nng.NET.Shared, nng.NET");
                }

                // Try to find the factory initialization method
                var factoryType = assembly.GetType("nng.Native.NngFactory")
                    ?? assembly.GetType("nng.NngFactory");

                if (factoryType == null)
                {
                    throw new InvalidOperationException("Failed to find NngFactory type");
                }

                // Create factory instance
                var factoryMethod = factoryType.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?? factoryType.GetMethod("Init", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (factoryMethod == null)
                {
                    throw new InvalidOperationException("Failed to find factory creation method");
                }

                _factory = factoryMethod.Invoke(null, null);

                if (_factory == null)
                {
                    throw new InvalidOperationException("Failed to initialize NNG factory");
                }

                // Create REP socket and listen
                dynamic result = _factory.ReplierOpen().ThenListen(_address);
                if (result.IsOk())
                {
                    _socket = result.Ok();
                }
                else
                {
                    throw new InvalidOperationException($"Failed to listen on {_address}: {result.Err()}");
                }

                _isListening = true;
                _listenCts = new CancellationTokenSource();
                _listenTask = ListenLoopAsync(_listenCts.Token);

                _logger.LogInfo("NNGServer", $"Server listening on: {_address}");
            }
            catch (Exception ex)
            {
                _logger.LogError("NNGServer", $"Failed to start server: {ex.Message}", ex);
                Cleanup();
                throw;
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfo("NNGServer", "Listen loop started");

        while (!cancellationToken.IsCancellationRequested && _socket != null && _factory != null)
        {
            try
            {
                // Wait for request (run on thread pool to avoid blocking)
                var recvResult = await Task.Run(() => _socket.RecvMsg(), cancellationToken);

                if (!recvResult.IsOk())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    _logger.LogWarning("NNGServer", $"Receive error: {recvResult.Err()}");
                    continue;
                }

                var requestMsg = recvResult.Ok();
                var data = requestMsg.AsSpan().ToArray();

                try
                {
                    var parsed = Protocol.ParseMessage(data);
                    _logger.LogDebug("NNGServer", $"Received message type=0x{parsed.MsgId:X6}");

                    var args = new RequestReceivedEventArgs(parsed.MsgId, parsed.ReqId, parsed.Body);
                    RequestReceived?.Invoke(this, args);

                    // Send response
                    var responseData = args.Response;
                    var responseMsg = _factory.CreateMessage();
                    responseMsg.Append(responseData);

                    var sendResult = await Task.Run(() => _socket.SendMsg(responseMsg), cancellationToken);
                    if (!sendResult.IsOk())
                    {
                        _logger.LogWarning("NNGServer", $"Reply failed: {sendResult.Err()}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("NNGServer", $"Error processing request: {ex.Message}", ex);

                    // Send error response
                    try
                    {
                        var errorResponse = Protocol.NewMessage(
                            Protocol.MsgInvokeResponse,
                            0,
                            Array.Empty<byte>());
                        var errorMsg = _factory.CreateMessage();
                        errorMsg.Append(errorResponse);
                        await Task.Run(() => _socket.SendMsg(errorMsg), cancellationToken);
                    }
                    catch
                    {
                        // Ignore reply errors
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("NNGServer", $"Listen loop error: {ex.Message}", ex);
                await Task.Delay(100, cancellationToken);
            }
        }

        _logger.LogInfo("NNGServer", "Listen loop stopped");
    }

    /// <summary>
    /// Stop the server.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isListening)
            {
                return;
            }

            _logger.LogInfo("NNGServer", "Stopping server...");

            _listenCts?.Cancel();

            try
            {
                _listenTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Expected on cancellation
            }

            Cleanup();

            _listenCts?.Dispose();
            _listenCts = null;
            _isListening = false;

            _logger.LogInfo("NNGServer", "Server stopped");
        }
    }

    private void Cleanup()
    {
        _socket?.Dispose();
        _socket = null;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(NNGServer));
        }
    }

    /// <summary>
    /// Release resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Stop();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for request received event.
/// </summary>
public class RequestReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Message type ID.
    /// </summary>
    public int MsgId { get; }

    /// <summary>
    /// Request ID.
    /// </summary>
    public int ReqId { get; }

    /// <summary>
    /// Request body.
    /// </summary>
    public byte[] Body { get; }

    /// <summary>
    /// Response to send back.
    /// </summary>
    public byte[] Response { get; set; }

    /// <summary>
    /// Create event args.
    /// </summary>
    public RequestReceivedEventArgs(int msgId, int reqId, byte[] body)
    {
        MsgId = msgId;
        ReqId = reqId;
        Body = body;
        Response = Array.Empty<byte>();
    }
}
