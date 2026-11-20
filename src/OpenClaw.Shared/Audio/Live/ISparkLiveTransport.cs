using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Audio.Live;

/// <summary>
/// Transport abstraction for bidirectional realtime live streaming (WebSocket, WebRTC, or mock).
/// </summary>
public interface ISparkLiveTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(string endpoint, CancellationToken cancellationToken);
    Task SendAudioChunkAsync(ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken);
    Task SendControlMessageAsync(string type, string payload, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);

    event Action<ReadOnlyMemory<byte>>? AudioChunkReceived;
    event Action<string, string>? ControlMessageReceived;
    event Action<Exception>? TransportError;
    event Action? Disconnected;
}
