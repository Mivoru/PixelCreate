using LiteNetLib;
using PixelCreate.Core;

namespace PixelCreate.Transport;

/// <summary>Real UDP through LiteNetLib. Peer 0 always means the server; clients get ids from 1.</summary>
public sealed class UdpTransport : ITransport, IDisposable
{
    private const string ConnectionKey = "pixelcreate-1";

    private readonly NetManager _manager;
    private readonly Dictionary<int, NetPeer> _peers = new();
    private readonly Dictionary<NetPeer, int> _ids = new();
    private readonly Queue<(int Peer, byte[] Data)> _inbox = new();
    private int _nextId = 1;

    private UdpTransport(EventBasedNetListener listener, bool isServer)
    {
        // A client that stalls for a few seconds (loading, a breakpoint, a window dragged) keeps its seat.
        _manager = new NetManager(listener) { UnsyncedEvents = false, DisconnectTimeout = 20000 };
        listener.NetworkReceiveEvent += (peer, reader, _, _) =>
        {
            _inbox.Enqueue((_ids.GetValueOrDefault(peer), reader.GetRemainingBytes()));
            reader.Recycle();
        };
        listener.PeerConnectedEvent += peer =>
        {
            var id = isServer ? _nextId++ : 0;
            _peers[id] = peer;
            _ids[peer] = id;
        };
        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            // A connection attempt that never got through reports no peer.
            if (peer is not null && _ids.Remove(peer, out var id))
            {
                _peers.Remove(id);
            }

            LastDisconnect = info.Reason.ToString();
        };
    }

    public static UdpTransport Listen(int port)
    {
        var listener = new EventBasedNetListener();
        listener.ConnectionRequestEvent += request => request.AcceptIfKey(ConnectionKey);
        var transport = new UdpTransport(listener, isServer: true);
        if (!transport._manager.Start(port))
        {
            throw new InvalidOperationException($"Cannot listen on UDP port {port}.");
        }

        return transport;
    }

    public static UdpTransport Connect(string host, int port)
    {
        var transport = new UdpTransport(new EventBasedNetListener(), isServer: false);
        transport._manager.Start();
        transport._manager.Connect(host, port, ConnectionKey);
        return transport;
    }

    public bool IsConnected => _peers.Count > 0;

    /// <summary>Why the last connection ended, for the log; empty while none has.</summary>
    public string LastDisconnect { get; private set; } = "";

    /// <summary>Processes network events without taking any packet from the inbox.</summary>
    public void Poll() => _manager.PollEvents();

    public void Send(int peer, byte[] data)
    {
        if (_peers.TryGetValue(peer, out var target))
        {
            target.Send(data, DeliveryMethod.Unreliable);
        }
    }

    public bool TryReceive(out int peer, out byte[] data)
    {
        _manager.PollEvents();
        if (_inbox.TryDequeue(out var packet))
        {
            (peer, data) = packet;
            return true;
        }

        peer = 0;
        data = Array.Empty<byte>();
        return false;
    }

    public void Dispose() => _manager.Stop();
}
