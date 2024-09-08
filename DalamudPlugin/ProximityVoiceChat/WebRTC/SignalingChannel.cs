﻿using AsyncAwaitBestPractices;
using ProximityVoiceChat.Log;
using SocketIO.Serializer.SystemTextJson;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ProximityVoiceChat.WebRTC;

public class SignalingChannel : IDisposable
{
    public bool Connected => !(this.disconnectCts?.IsCancellationRequested ?? false) && this.socket.Connected;
    public bool Disconnected { get; private set; }

    public event Action? OnConnected;
    public event Action<SocketIOResponse>? OnMessage;
    public event Action? OnDisconnected;

    private CancellationTokenSource? disconnectCts;

    private readonly string peerId;
    private readonly string peerType;
    private readonly SocketIOClient.SocketIO socket;
    private readonly ILogger logger;
    private readonly bool verbose;

    public SignalingChannel(string peerId, string peerType, string signalingServerUrl, string token, ILogger logger, bool verbose = false)
    {
        this.peerId = peerId;
        this.peerType = peerType;
        this.logger = logger;
        this.verbose = verbose;
        this.socket = new SocketIOClient.SocketIO(signalingServerUrl, new SocketIOOptions
        {
            Auth = new Dictionary<string, string>() { { "token", token } },
            Reconnection = true,
        });
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        this.socket.Serializer = new SystemTextJsonSerializer(options);
        this.AddListeners();
    }

    public Task ConnectAsync()
    {
        this.disconnectCts?.Dispose();
        this.disconnectCts = new();
        this.Disconnected = false;
        return this.socket.ConnectAsync(this.disconnectCts.Token);
    }

    public Task SendAsync(SignalMessage.SignalPayload payload)
    {
        if (this.socket.Connected)
        {
            return this.socket.EmitAsync("message", new SignalMessage
            {
                from = this.peerId,
                target = "all",
                payload = payload,
            });
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    public Task SendToAsync(string targetPeerId, SignalMessage.SignalPayload payload)
    {
        if (this.socket.Connected)
        {
            return this.socket.EmitAsync("messageOne", new SignalMessage
            {
                from = this.peerId,
                target = targetPeerId,
                payload = payload,
            });
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    public Task DisconnectAsync()
    {
        this.Disconnected = true;
        if (this.socket.Connected)
        {
            return this.socket.DisconnectAsync();
        }
        else
        {
            this.logger.Debug("Cancelling signaling server connection.");
            this.disconnectCts?.Cancel();
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        this.OnConnected = null;
        this.OnMessage = null;
        this.OnDisconnected = null;
        this.RemoveListeners();
        this.socket?.Dispose();
    }

    private void AddListeners()
    {
        if (this.socket != null)
        {
            this.socket.OnConnected += this.OnConnect;
            this.socket.OnDisconnected += this.OnDisconnect;
            this.socket.OnError += this.OnError;
            this.socket.OnReconnected += this.OnReconnect;
            this.socket.On("message", this.OnMessageCallback);
            this.socket.On("uniquenessError", this.OnUniquenessError);
        }
    }

    private void RemoveListeners()
    {
        if (this.socket != null)
        {
            this.socket.OnConnected -= this.OnConnect;
            this.socket.OnDisconnected -= this.OnDisconnect;
            this.socket.OnError -= this.OnError;
            this.socket.OnReconnected -= this.OnReconnect;
            this.socket.Off("message");
            this.socket.Off("uniquenessError");
        }
    }

    private void OnConnect(object? sender, EventArgs args)
    {
        try
        {
            if (!Connected)
            {
                return;
            }

            if (this.verbose)
            {
                this.logger.Debug("Connected to signaling server.");
            }
            this.OnConnected?.Invoke();
            this.socket.EmitAsync("ready", this.peerId, this.peerType).SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
        }
        catch (Exception ex)
        {
            this.logger.Error(ex.ToString());
        }
    }

    private void OnDisconnect(object? sender, string reason)
    {
        try
        {
            if (this.verbose)
            {
                this.logger.Debug("Disconnected from signaling server, reason: {0}", reason);
            }
            this.Disconnected = true;
            this.OnDisconnected?.Invoke();
        }
        catch (Exception ex)
        {
            this.logger.Error(ex.ToString());
        }
    }

    private void OnError(object? sender, string error)
    {
        this.logger.Error("Signaling server ERROR: " + error);
    }

    private void OnReconnect(object? sender, int attempts)
    {
        if (this.verbose)
        {
            this.logger.Info("Signaling server reconnect, attempts: {0}", attempts);
        }
    }

    private void OnMessageCallback(SocketIOResponse response)
    {
        if (!Connected)
        {
            return;
        }

        if (this.verbose)
        {
            this.logger.Trace("Signaling server message: {0}", response);
        }
        this.OnMessage?.Invoke(response);
    }

    private void OnUniquenessError(SocketIOResponse response)
    {
        if (!Connected)
        {
            return;
        }

        this.logger.Error("Uniqueness ERROR: {0}", response);

        // This error auto disconnects the client, but does not immediately set the socket state to not Connected.
        // So we need to dispose and nullify the token to avoid calling Cancel on the token, which for some reason
        // throws an exception due to cancellation token subscriptions.
        this.disconnectCts?.Dispose();
        this.disconnectCts = null;
    }
}
