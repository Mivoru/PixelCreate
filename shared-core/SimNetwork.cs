namespace PixelCreate.Core;

/// <summary>
/// A network in memory with latency, jitter, loss and reordering. It runs on a virtual clock
/// and a fixed seed, so a test takes milliseconds and every run is identical.
/// </summary>
public sealed class SimNetwork
{
    private sealed class Endpoint : ITransport
    {
        private readonly SimNetwork _network;
        private readonly int _id;
        public readonly Queue<(int From, byte[] Data)> Inbox = new();

        public Endpoint(SimNetwork network, int id)
        {
            _network = network;
            _id = id;
        }

        public void Send(int peer, byte[] data) => _network.Enqueue(_id, peer, data);

        public bool TryReceive(out int peer, out byte[] data)
        {
            if (Inbox.TryDequeue(out var packet))
            {
                (peer, data) = packet;
                return true;
            }

            peer = 0;
            data = Array.Empty<byte>();
            return false;
        }
    }

    private readonly Random _random;
    private readonly double _oneWayMs;
    private readonly double _jitterMs;
    private readonly double _lossChance;
    private readonly Dictionary<int, Endpoint> _endpoints = new();
    private readonly PriorityQueue<(int From, int To, byte[] Data), double> _inFlight = new();
    private double _nowMs;

    public long Sent { get; private set; }
    public long Lost { get; private set; }

    public SimNetwork(int seed, double rttMs, double jitterMs, double lossPercent)
    {
        _random = new Random(seed);
        _oneWayMs = rttMs / 2;
        _jitterMs = jitterMs;
        _lossChance = lossPercent / 100;
    }

    public ITransport Connect(int id)
    {
        var endpoint = new Endpoint(this, id);
        _endpoints.Add(id, endpoint);
        return endpoint;
    }

    public void Advance(double ms)
    {
        _nowMs += ms;
        while (_inFlight.TryPeek(out var packet, out var arrival) && arrival <= _nowMs)
        {
            _inFlight.Dequeue();
            _endpoints[packet.To].Inbox.Enqueue((packet.From, packet.Data));
        }
    }

    private void Enqueue(int from, int to, byte[] data)
    {
        Sent++;
        if (_random.NextDouble() < _lossChance)
        {
            Lost++;
            return;
        }

        var jitter = (_random.NextDouble() * 2 - 1) * _jitterMs;
        _inFlight.Enqueue((from, to, data), _nowMs + _oneWayMs + jitter);
    }
}
