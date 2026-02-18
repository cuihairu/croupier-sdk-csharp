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

using Croupier.Sdk.Transport;
using Xunit;
using Xunit.Priority;
using System.Reflection;

namespace Croupier.Sdk.Tests.Transport;

/// <summary>
/// Tests for NNGTransport and NNGServer.
/// Note: These tests require nng native library to be available.
/// </summary>
[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public class NNGTransportTests : IDisposable
{
    private const string TestAddress = "inproc://test-transport";
    private NNGServer? _server;
    private NNGTransport? _client;

    private static bool IsNNGAvailable()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET.Shared")
                ?? assemblies.FirstOrDefault(a => a.GetName().Name == "nng.NET")
                ?? assemblies.FirstOrDefault(a => a.GetName().Name.StartsWith("nng"));

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
                        return false;
                    }
                }
            }

            return assembly != null;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _server?.Dispose();
    }

    [Fact, Priority(1)]
    public void Constructor_ShouldInitializeCorrectly()
    {
        if (!IsNNGAvailable())
        {
            Assert.True(true, "NNG native library not available - test skipped");
            return;
        }

        var transport = new NNGTransport("tcp://127.0.0.1:19090");

        Assert.False(transport.IsConnected);
    }

    [Fact, Priority(1)]
    public void Constructor_WithCustomTimeout_ShouldUseTimeout()
    {
        if (!IsNNGAvailable())
        {
            Assert.True(true, "NNG native library not available - test skipped");
            return;
        }

        var transport = new NNGTransport("tcp://127.0.0.1:19090", 10000);

        Assert.False(transport.IsConnected);
    }

    [Fact, Priority(2)]
    public void Connect_ShouldSetIsConnected()
    {
        // Start server first
        _server = new NNGServer("inproc://test-connect");
        _server.Listen();

        // Connect client
        _client = new NNGTransport("inproc://test-connect");
        _client.Connect();

        Assert.True(_client.IsConnected);
    }

    [Fact, Priority(2)]
    public void Close_ShouldSetIsConnectedFalse()
    {
        // Start server
        _server = new NNGServer("inproc://test-close");
        _server.Listen();

        // Connect and close
        _client = new NNGTransport("inproc://test-close");
        _client.Connect();
        Assert.True(_client.IsConnected);

        _client.Close();
        Assert.False(_client.IsConnected);
    }

    [Fact, Priority(2)]
    public void Dispose_ShouldCleanupResources()
    {
        var transport = new NNGTransport("inproc://test-dispose");
        transport.Dispose();

        Assert.Throws<ObjectDisposedException>(() => transport.Connect());
    }

    [Fact, Priority(3)]
    public void Call_ShouldSendAndReceiveMessage()
    {
        var serverAddress = "inproc://test-call";
        var receivedMsgId = 0;
        var receivedReqId = 0;
        byte[]? receivedBody = null;

        // Setup server
        _server = new NNGServer(serverAddress);
        _server.RequestReceived += (sender, e) =>
        {
            receivedMsgId = e.MsgId;
            receivedReqId = e.ReqId;
            receivedBody = e.Body;

            // Send response
            var response = Protocol.NewMessage(Protocol.GetResponseMsgId(e.MsgId), e.ReqId, e.Body);
            e.Response = response;
        };
        _server.Listen();

        // Give server time to start
        Thread.Sleep(100);

        // Connect client
        _client = new NNGTransport(serverAddress);
        _client.Connect();

        // Send request
        var body = System.Text.Encoding.UTF8.GetBytes("test payload");
        var response = _client.Call(Protocol.MsgInvokeRequest, body);

        // Verify request was received
        Assert.Equal(Protocol.MsgInvokeRequest, receivedMsgId);
        Assert.NotNull(receivedBody);
        Assert.Equal(body, receivedBody);

        // Verify response
        Assert.NotNull(response);
        var parsed = Protocol.ParseMessage(response);
        Assert.Equal(Protocol.MsgInvokeResponse, parsed.MsgId);
    }

    [Fact, Priority(3)]
    public async Task CallAsync_ShouldSendAndReceiveMessage()
    {
        var serverAddress = "inproc://test-call-async";
        var receivedMsgId = 0;

        // Setup server
        _server = new NNGServer(serverAddress);
        _server.RequestReceived += (sender, e) =>
        {
            receivedMsgId = e.MsgId;
            var response = Protocol.NewMessage(Protocol.GetResponseMsgId(e.MsgId), e.ReqId, e.Body);
            e.Response = response;
        };
        _server.Listen();

        Thread.Sleep(100);

        // Connect client
        _client = new NNGTransport(serverAddress);
        _client.Connect();

        // Send async request
        var body = System.Text.Encoding.UTF8.GetBytes("async test payload");
        var response = await _client.CallAsync(Protocol.MsgHeartbeatRequest, body);

        // Verify
        Assert.Equal(Protocol.MsgHeartbeatRequest, receivedMsgId);
        Assert.NotNull(response);
    }

    [Fact, Priority(3)]
    public void Call_WithNullBody_ShouldWork()
    {
        var serverAddress = "inproc://test-null-body";

        _server = new NNGServer(serverAddress);
        _server.RequestReceived += (sender, e) =>
        {
            var response = Protocol.NewMessage(Protocol.GetResponseMsgId(e.MsgId), e.ReqId, null);
            e.Response = response;
        };
        _server.Listen();

        Thread.Sleep(100);

        _client = new NNGTransport(serverAddress);
        _client.Connect();

        // Send with null body
        var response = _client.Call(Protocol.MsgHeartbeatRequest, null);

        Assert.NotNull(response);
        var parsed = Protocol.ParseMessage(response);
        Assert.Equal(Protocol.MsgHeartbeatResponse, parsed.MsgId);
    }

    [Fact, Priority(3)]
    public void Call_WhenNotConnected_ShouldThrow()
    {
        _client = new NNGTransport("inproc://test-not-connected");

        Assert.Throws<InvalidOperationException>(() => _client.Call(Protocol.MsgInvokeRequest, null));
    }
}

/// <summary>
/// Tests for NNGServer.
/// </summary>
[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public class NNGServerTests : IDisposable
{
    private NNGServer? _server;

    public void Dispose()
    {
        _server?.Dispose();
    }

    [Fact, Priority(1)]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _server = new NNGServer("inproc://test-server-ctor");

        Assert.False(_server.IsListening);
    }

    [Fact, Priority(2)]
    public void Listen_ShouldSetIsListening()
    {
        _server = new NNGServer("inproc://test-server-listen");
        _server.Listen();

        Assert.True(_server.IsListening);
    }

    [Fact, Priority(2)]
    public void Stop_ShouldSetIsListeningFalse()
    {
        _server = new NNGServer("inproc://test-server-stop");
        _server.Listen();
        Assert.True(_server.IsListening);

        _server.Stop();
        Assert.False(_server.IsListening);
    }

    [Fact, Priority(2)]
    public void Dispose_ShouldCleanupResources()
    {
        var server = new NNGServer("inproc://test-server-dispose");
        server.Listen();
        server.Dispose();

        // Should not throw - server is disposed
        Assert.Throws<ObjectDisposedException>(() => server.Listen());
    }

    [Fact, Priority(2)]
    public void Listen_WhenAlreadyListening_ShouldNotThrow()
    {
        _server = new NNGServer("inproc://test-server-double");
        _server.Listen();
        _server.Listen(); // Should not throw

        Assert.True(_server.IsListening);
    }

    [Fact, Priority(3)]
    public void RequestReceived_ShouldFireOnRequest()
    {
        var serverAddress = "inproc://test-server-request";
        var requestReceived = false;

        _server = new NNGServer(serverAddress);
        _server.RequestReceived += (sender, e) =>
        {
            requestReceived = true;
            e.Response = Protocol.NewMessage(Protocol.GetResponseMsgId(e.MsgId), e.ReqId, null);
        };
        _server.Listen();

        Thread.Sleep(100);

        // Send a request from client
        using var client = new NNGTransport(serverAddress);
        client.Connect();
        client.Call(Protocol.MsgHeartbeatRequest, null);

        Assert.True(requestReceived);
    }
}

/// <summary>
/// Integration tests for full REQ/REP communication.
/// </summary>
[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public class NNGIntegrationTests : IDisposable
{
    private NNGServer? _server;
    private NNGTransport? _client;

    public void Dispose()
    {
        _client?.Dispose();
        _server?.Dispose();
    }

    [Fact, Priority(1)]
    public void FullRequestResponse_ShouldWork()
    {
        var address = "inproc://test-integration-1";

        // Setup echo server
        _server = new NNGServer(address);
        _server.RequestReceived += (sender, e) =>
        {
            // Echo back the body with response type
            e.Response = Protocol.NewMessage(Protocol.GetResponseMsgId(e.MsgId), e.ReqId, e.Body);
        };
        _server.Listen();

        Thread.Sleep(100);

        // Client sends message
        _client = new NNGTransport(address);
        _client.Connect();

        var payload = System.Text.Encoding.UTF8.GetBytes("{\"test\":\"data\"}");
        var response = _client.Call(Protocol.MsgInvokeRequest, payload);

        // Verify round-trip
        var parsed = Protocol.ParseMessage(response);
        Assert.Equal(Protocol.MsgInvokeResponse, parsed.MsgId);
        Assert.Equal(payload, parsed.Body);
    }

    [Fact, Priority(2)]
    public void MultipleRequests_ShouldWorkSequentially()
    {
        var address = "inproc://test-integration-multi";

        _server = new NNGServer(address);
        _server.RequestReceived += (sender, e) =>
        {
            e.Response = Protocol.NewMessage(Protocol.GetResponseMsgId(e.MsgId), e.ReqId, e.Body);
        };
        _server.Listen();

        Thread.Sleep(100);

        _client = new NNGTransport(address);
        _client.Connect();

        // Send multiple requests
        for (int i = 0; i < 5; i++)
        {
            var payload = System.Text.Encoding.UTF8.GetBytes($"request-{i}");
            var response = _client.Call(Protocol.MsgInvokeRequest, payload);

            var parsed = Protocol.ParseMessage(response);
            Assert.Equal(Protocol.MsgInvokeResponse, parsed.MsgId);
            Assert.Equal(payload, parsed.Body);
        }
    }

    [Fact, Priority(2)]
    public void DifferentMessageTypes_ShouldRouteCorrectly()
    {
        var address = "inproc://test-integration-types";
        var lastMsgId = 0;

        _server = new NNGServer(address);
        _server.RequestReceived += (sender, e) =>
        {
            lastMsgId = e.MsgId;
            e.Response = Protocol.NewMessage(Protocol.GetResponseMsgId(e.MsgId), e.ReqId, null);
        };
        _server.Listen();

        Thread.Sleep(100);

        _client = new NNGTransport(address);
        _client.Connect();

        // Test different message types
        var testCases = new[]
        {
            Protocol.MsgHeartbeatRequest,
            Protocol.MsgInvokeRequest,
            Protocol.MsgRegisterLocalRequest,
        };

        foreach (var msgType in testCases)
        {
            _client.Call(msgType, null);
            Assert.Equal(msgType, lastMsgId);
        }
    }
}
