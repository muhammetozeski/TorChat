using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Chat
{
    // --- Data Models ---

    public class PeerInfo
    {
        public string OnionAddress { get; set; } = "";
        public string Username { get; set; } = "";
        public string Bio { get; set; } = "";
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public class Packet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SenderOnion { get; set; } = "";
        public string Type { get; set; } = "";
        public string Payload { get; set; } = "";
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public class ChatMessage
    {
        public string Id { get; set; } = "";
        public string SenderOnion { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string Text { get; set; } = "";
        public long Timestamp { get; set; }
    }

    // --- P2P Node ---

    public class P2PNode
    {
        private TorManager? _tor;
        private TcpListener? _listener;

        private readonly ConcurrentDictionary<string, TcpClient> _connections = new();
        private readonly ConcurrentDictionary<string, StreamWriter> _writers = new();
        private readonly ConcurrentDictionary<string, bool> _seenPacketIds = new();
        private readonly ConcurrentDictionary<string, bool> _cancelledConnections = new();

        private readonly ConcurrentDictionary<string, bool> _connectingPeers = new();

        public ConcurrentDictionary<string, PeerInfo> KnownPeers { get; } = new();
        public string? MyOnion => _tor?.OnionAddress;
        public SecureRamKey? GeneratedKey => _tor?.GeneratedKey;
        public PeerInfo MyProfile { get; private set; } = new() { IsOnline = true, Bio = "Merhaba!" };
        public DateTime? ConnectedAt { get; private set; }

        public event Action<ChatMessage>? OnMessageReceived;
        public event Action<PeerInfo>? OnPeerUpdated;
        public event Action<string>? OnLog;

        // --- Lifecycle ---

        public async Task StartAsync(SecureRamKey? secretKey)
        {
            Log("[START] P2PNode.StartAsync started.");

            try
            {
                Log("Instantiating TorManager instance...");
                _tor = new TorManager();
                Log("TorManager instance created.");

                Log("Registering TorManager event listeners (OnLog, OnReady)...");
                _tor.OnLog += msg =>
                {
                    Log($"[TorManager Event] {msg}");
                    try
                    {
                        OnLog?.Invoke(msg);
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Exception in OnLog event delegate: {ex.Message}", LogLevel.Error);
                    }
                };

                _tor.OnReady += onion =>
                {
                    Log($"[TorManager OnReady Event] Received onion address: '{onion}'");
                    ConnectedAt = DateTime.Now;
                    Log($"Setting MyProfile.OnionAddress = '{onion}'...");
                    MyProfile.OnionAddress = onion;
                    string defaultUsername = onion[..Math.Min(8, onion.Length)];
                    Log($"Setting MyProfile.Username = '{defaultUsername}'...");
                    MyProfile.Username = defaultUsername;

                    Log("Invoking OnPeerUpdated event for MyProfile...");
                    try
                    {
                        OnPeerUpdated?.Invoke(MyProfile);
                        Log("OnPeerUpdated event invoked.");
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Exception in OnPeerUpdated event delegate: {ex.Message}", LogLevel.Error);
                    }

                    Log("Calling StartListener()...");
                    StartListener();
                    Log("StartListener() called from OnReady handler.");

                    Log("Spawning MeshMaintainerLoopAsync task...");
                    _ = MeshMaintainerLoopAsync();
                };

                Log("Starting TorManager asynchronously via StartAsync()...");
                await _tor.StartAsync(secretKey);
                Log("[END] TorManager.StartAsync() completed.");
            }
            catch (Exception ex)
            {
                Log($"[FATAL] P2PNode.StartAsync failed: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        private async Task MeshMaintainerLoopAsync()
        {
            Log("[START] MeshMaintainerLoopAsync started.");
            var loopPolicy = AppResiliencePolicies.CreateLoopRetryPolicy("MeshMaintainer", msg => Log(msg));

            while (_tor != null)
            {
                try
                {
                    await loopPolicy.ExecuteAsync(async () =>
                    {
                        await Task.Delay(10000);
                        if (!string.IsNullOrEmpty(MyOnion))
                        {
                            var targetPeers = KnownPeers.Values
                                .Where(p => p.IsOnline && p.OnionAddress != MyOnion &&
                                            !_connections.ContainsKey(p.OnionAddress) &&
                                            !_connectingPeers.ContainsKey(p.OnionAddress) &&
                                            !_cancelledConnections.ContainsKey(p.OnionAddress))
                                .ToList();

                            foreach (var p in targetPeers)
                            {
                                Log($"Mesh maintainer: Retrying connection to unconnected peer '{p.OnionAddress}'...");
                                _ = ConnectToPeerAsync(p.OnionAddress);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log($"[FATAL POLLY LOOP EXHAUSTION] MeshMaintainer loop Policy exhausted retries: {ex.Message}. Exiting loop.", LogLevel.Error);
                    break;
                }
            }
        }

        public void Stop()
        {
            Log("[START] P2PNode.Stop started.");

            Log("Stopping TcpListener...");
            try
            {
                _listener?.Stop();
                _listener = null;
                Log("TcpListener stopped and set to null successfully.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Failed to stop TcpListener: {ex.Message}", LogLevel.Error);
            }

            Log($"Closing {_connections.Count} active connections...");
            foreach (var (peerKey, connection) in _connections)
            {
                try
                {
                    Log($"Closing connection to peer '{peerKey}'...");
                    connection.Close();
                    connection.Dispose();
                    Log($"Connection to peer '{peerKey}' closed and disposed.");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Exception closing connection to '{peerKey}': {ex.Message}", LogLevel.Error);
                }
            }
            _connections.Clear();
            _writers.Clear();
            _cancelledConnections.Clear();

            Log("Stopping TorManager instance...");
            try
            {
                _tor?.Stop();
                Log("TorManager instance stopped successfully.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Failed to stop TorManager: {ex.Message}", LogLevel.Error);
            }

            Log("[END] P2PNode.Stop completed.");
        }

        // --- Connection Management ---

        public void StartListener()
        {
            Log("[START] StartListener started.");

            if (_tor == null)
            {
                Log("[FATAL] StartListener called before TorManager initialization! Throwing InvalidOperationException.", LogLevel.Error);
                throw new InvalidOperationException("TorManager not initialized");
            }

            Log($"Creating TcpListener on IPAddress.Loopback port {_tor.LocalTcpPort}...");
            _listener = new TcpListener(IPAddress.Loopback, _tor.LocalTcpPort);

            Log("Starting TcpListener...");
            _listener.Start();
            Log($"TcpListener listening on 127.0.0.1:{_tor.LocalTcpPort}");

            Log("Spawning AcceptConnectionsAsync task...");
            _ = AcceptConnectionsAsync();
            Log("[END] StartListener completed.");
        }

        private async Task AcceptConnectionsAsync()
        {
            Log("[START] AcceptConnectionsAsync started.");

            var loopPolicy = AppResiliencePolicies.CreateLoopRetryPolicy("AcceptConnections", msg => Log(msg));

            while (_listener != null)
            {
                try
                {
                    await loopPolicy.ExecuteAsync(async () =>
                    {
                        Log("Awaiting incoming TCP connection via AcceptTcpClientAsync()...");
                        var client = await _listener.AcceptTcpClientAsync();
                        Log($"Incoming TCP connection accepted from endpoint '{client.Client.RemoteEndPoint}'.");

                        Log("Spawning HandleConnectionAsync task for accepted client...");
                        _ = HandleConnectionAsync(client, peerOnion: null);
                    });
                }
                catch (ObjectDisposedException)
                {
                    Log("TcpListener disposed. Exiting AcceptConnectionsAsync loop gracefully.");
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    Log($"TcpListener stopped/not listening ({ex.Message}). Exiting AcceptConnectionsAsync loop gracefully.");
                    break;
                }
                catch (Exception ex)
                {
                    Log($"[FATAL POLLY LOOP EXHAUSTION] AcceptConnections loop Policy exhausted retries: {ex.GetType().Name} - {ex.Message}. Exiting loop.", LogLevel.Error);
                    break;
                }
            }

            Log("[END] AcceptConnectionsAsync loop exited.");
        }

        // --- Outbound Connection ---

        public void CancelConnection(string onion)
        {
            Log($"[START] CancelConnection called for onion: '{onion}'");
            _cancelledConnections[onion] = true;
            Log($"Connection marked as cancelled for onion: '{onion}'");
        }

        public async Task ConnectToPeerAsync(string onion)
        {
            Log($"[START] ConnectToPeerAsync started for target onion: '{onion}'");

            if (onion == MyOnion || _connections.ContainsKey(onion))
            {
                Log($"[SKIP] ConnectToPeerAsync skipped for '{onion}' (Reason: self onion or already connected).");
                return;
            }

            if (!_connectingPeers.TryAdd(onion, true))
            {
                Log($"[SKIP] ConnectToPeerAsync skipped for '{onion}' (Reason: connection attempt already in progress).");
                return;
            }

            var connectPolicy = Policy
                .Handle<SocketException>()
                .Or<IOException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(2),
                    onRetry: (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] ConnectToPeerAsync attempt {attempt} to '{onion}' failed. Waiting {span.TotalSeconds}s. Exception: {ex.Message}", LogLevel.Warning);
                    });

            try
            {
                Log($"Executing Polly connect policy to target '{onion}' via SOCKS5 proxy 127.0.0.1:{_tor!.SocksPort}...");
                var client = await connectPolicy.ExecuteAsync(async () =>
                {
                    return await Socks5Client.ConnectAsync("127.0.0.1", _tor!.SocksPort, onion, 80);
                });

                Log($"Socks5Client connected successfully to target onion '{onion}'.");

                Log($"Checking if connection to '{onion}' was cancelled while establishing...");
                if (_cancelledConnections.TryRemove(onion, out _))
                {
                    Log($"Connection to '{onion}' was cancelled. Closing client.", LogLevel.Warning);
                    client.Close();
                    client.Dispose();
                    Log($"Client closed and disposed for cancelled connection to '{onion}'.");
                    return;
                }

                Log($"Spawning HandleConnectionAsync for connected peer '{onion}'...");
                _ = HandleConnectionAsync(client, peerOnion: onion);

                if (!KnownPeers.ContainsKey(onion))
                {
                    Log($"Adding initial PeerInfo entry for '{onion}' to KnownPeers...");
                    var placeholder = new PeerInfo
                    {
                        OnionAddress = onion,
                        Username = onion[..Math.Min(8, onion.Length)],
                        Bio = "",
                        IsOnline = true,
                        LastSeen = DateTime.Now
                    };
                    KnownPeers[onion] = placeholder;
                    try { OnPeerUpdated?.Invoke(placeholder); } catch { }
                }

                Log($"HandleConnectionAsync task spawned for '{onion}'.");
                Log($"[END] ConnectToPeerAsync completed successfully for '{onion}'.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] ConnectToPeerAsync failed for onion '{onion}' after retries: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                try
                {
                    OnLog?.Invoke($"Connect failed ({onion[..Math.Min(8, onion.Length)]}...): {ex.Message}");
                }
                catch { }
            }
            finally
            {
                _connectingPeers.TryRemove(onion, out _);
            }
        }

        // --- Connection Handler ---

        private async Task HandleConnectionAsync(TcpClient client, string? peerOnion)
        {
            Log($"[START] HandleConnectionAsync started. Initial PeerOnion='{peerOnion ?? "(incoming)"}', Endpoint={client.Client.RemoteEndPoint}");

            StreamReader? reader = null;
            StreamWriter? writer = null;

            try
            {
                Log("Obtaining NetworkStream from TcpClient...");
                var stream = client.GetStream();
                Log("Creating StreamReader & StreamWriter (UTF-8)...");
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                Log("StreamReader and StreamWriter instantiated.");

                if (peerOnion != null)
                {
                    Log($"Registering outbound connection for peer '{peerOnion}' in _connections and _writers...");
                    _connections[peerOnion] = client;
                    _writers[peerOnion] = writer;
                    Log($"Outbound peer registered: '{peerOnion}'");
                }

                Log("Constructing local HELLO packet...");
                var hello = new Packet
                {
                    SenderOnion = MyOnion!,
                    Type = "HELLO",
                    Payload = JsonSerializer.Serialize(MyProfile)
                };
                string helloJson = JsonSerializer.Serialize(hello);
                Log($"Sending HELLO packet (Length={helloJson.Length}) to '{peerOnion ?? "(incoming)"}'...");

                var sendPolicy = Policy
                    .Handle<IOException>()
                    .Or<SocketException>()
                    .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                        (ex, span, attempt, ctx) =>
                        {
                            Log($"[RETRY] Writing HELLO packet attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                        });

                await sendPolicy.ExecuteAsync(async () =>
                {
                    await writer.WriteLineAsync(helloJson);
                });
                Log("HELLO packet sent successfully.");

                Log($"Entering read loop for peer '{peerOnion ?? "(incoming)"}'...");
                while (client.Connected)
                {
                    Log($"Awaiting line read from stream for peer '{peerOnion ?? "(incoming)"}'...");
                    string? line = await reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(line))
                    {
                        Log($"Received null/empty line from peer '{peerOnion ?? "(incoming)"}'. Breaking read loop.");
                        break;
                    }
                    Log($"Read raw line (Length={line.Length}) from stream.");

                    Packet? packet = null;
                    try
                    {
                        packet = JsonSerializer.Deserialize<Packet>(line);
                    }
                    catch (Exception jsonEx)
                    {
                        Log($"[ERROR] Failed to deserialize packet JSON from peer '{peerOnion ?? "unknown"}': {jsonEx.Message}", LogLevel.Error);
                        continue;
                    }

                    if (packet == null)
                    {
                        Log("[WARNING] Deserialized packet is null. Skipping processing.", LogLevel.Warning);
                        continue;
                    }
                    Log($"Received Packet: Id='{packet.Id}', Type='{packet.Type}', SenderOnion='{packet.SenderOnion}'");

                    Log($"Checking packet deduplication for Id '{packet.Id}'...");
                    if (!_seenPacketIds.TryAdd(packet.Id, true))
                    {
                        Log($"Duplicate packet Id '{packet.Id}' detected. Skipping processing.");
                        continue;
                    }
                    Log($"Packet Id '{packet.Id}' is unique.");

                    if (peerOnion == null && packet.Type == "HELLO")
                    {
                        Log($"Identifying incoming connection peer as SenderOnion='{packet.SenderOnion}'...");
                        peerOnion = packet.SenderOnion;
                        _connections[peerOnion] = client;
                        _writers[peerOnion] = writer;
                        Log($"Incoming peer identified & registered: '{peerOnion}'");
                    }

                    Log($"Processing packet Id '{packet.Id}' of Type '{packet.Type}'...");
                    ProcessPacket(packet);
                    Log($"Packet Id '{packet.Id}' processed.");

                    if (packet.SenderOnion != MyOnion)
                    {
                        Log($"Forwarding gossip packet Id '{packet.Id}' to other peers (excluding '{packet.SenderOnion}')...");
                        _ = BroadcastRawAsync(line, exclude: packet.SenderOnion);
                        Log("Gossip broadcast task spawned.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Connection exception for peer '{peerOnion ?? "unknown"}': {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }
            finally
            {
                Log($"[CLEANUP] Executing finally block in HandleConnectionAsync for peer '{peerOnion ?? "unknown"}'...");
                if (peerOnion != null)
                {
                    Log($"Removing peer '{peerOnion}' from _connections and _writers dictionaries...");
                    _connections.TryRemove(peerOnion, out _);
                    _writers.TryRemove(peerOnion, out _);

                    if (KnownPeers.TryGetValue(peerOnion, out var peer))
                    {
                        Log($"Updating offline status for peer '{peerOnion}' (IsOnline = false)...");
                        peer.IsOnline = false;
                        try
                        {
                            OnPeerUpdated?.Invoke(peer);
                            Log("OnPeerUpdated event invoked for offline status.");
                        }
                        catch (Exception eventEx)
                        {
                            Log($"[ERROR] Exception in OnPeerUpdated event handler: {eventEx.Message}", LogLevel.Error);
                        }

                        Log("Broadcasting updated peer list (peer offline)...");
                        _ = BroadcastFullPeerListAsync();
                    }
                    Log($"Peer connection cleanup completed for '{peerOnion}'.");
                }

                try
                {
                    writer?.Dispose();
                    reader?.Dispose();
                    client.Close();
                    client.Dispose();
                    Log("Socket & streams closed and disposed.");
                }
                catch (Exception cleanupEx)
                {
                    Log($"[CLEANUP ERROR] Failed closing client resources: {cleanupEx.Message}", LogLevel.Error);
                }

                Log($"[END] HandleConnectionAsync completed for peer '{peerOnion ?? "unknown"}'.");
            }
        }

        // --- Packet Processing ---

        private void ProcessPacket(Packet packet)
        {
            Log($"[START] ProcessPacket started for Packet Id='{packet.Id}', Type='{packet.Type}', SenderOnion='{packet.SenderOnion}'");

            try
            {
                switch (packet.Type)
                {
                    case "HELLO":
                    case "PROFILE_UPDATE":
                        Log($"Processing {packet.Type} payload...");
                        var profile = JsonSerializer.Deserialize<PeerInfo>(packet.Payload);
                        if (profile == null)
                        {
                            Log($"[WARNING] Deserialized profile payload for '{packet.SenderOnion}' is null. Aborting.", LogLevel.Warning);
                            return;
                        }

                        Log($"Deserialized profile: Username='{profile.Username}', Onion='{profile.OnionAddress}'. Updating online status...");
                        profile.IsOnline = true;
                        profile.LastSeen = DateTime.Now;

                        KnownPeers[packet.SenderOnion] = profile;
                        Log($"Updated KnownPeers for '{packet.SenderOnion}'. Invoking OnPeerUpdated...");
                        try
                        {
                            OnPeerUpdated?.Invoke(profile);
                            Log("OnPeerUpdated invoked successfully.");
                        }
                        catch (Exception eventEx)
                        {
                            Log($"[ERROR] Exception in OnPeerUpdated event handler: {eventEx.Message}", LogLevel.Error);
                        }

                        if (packet.Type == "HELLO")
                        {
                            var myProfilePacket = new Packet
                            {
                                SenderOnion = MyOnion!,
                                Type = "PROFILE_UPDATE",
                                Payload = JsonSerializer.Serialize(MyProfile)
                            };
                            Log($"Sending PROFILE_UPDATE reply to '{packet.SenderOnion}'...");
                            _ = SendToAsync(packet.SenderOnion, myProfilePacket);

                            Log($"Filtering existing known peers to send gossip PEERS list to '{packet.SenderOnion}'...");
                            var otherPeers = KnownPeers.Keys
                                .Where(k => k != packet.SenderOnion && k != MyOnion)
                                .ToList();
                            Log($"Found {otherPeers.Count} other peer address(es) to gossip.");

                            if (otherPeers.Count > 0)
                            {
                                var peersPacket = new Packet
                                {
                                    SenderOnion = MyOnion!,
                                    Type = "PEERS",
                                    Payload = JsonSerializer.Serialize(otherPeers)
                                };
                                Log($"Sending PEERS packet to '{packet.SenderOnion}'...");
                                _ = SendToAsync(packet.SenderOnion, peersPacket);
                                Log("SendToAsync task spawned for PEERS packet.");
                            }
                        }

                        Log("Broadcasting full peer list after receiving profile...");
                        _ = BroadcastFullPeerListAsync();
                        break;

                    case "PEER_LIST_UPDATE":
                        Log("Processing PEER_LIST_UPDATE payload...");
                        var peerListFull = JsonSerializer.Deserialize<List<PeerInfo>>(packet.Payload);
                        if (peerListFull == null)
                        {
                            Log("[WARNING] Deserialized PEER_LIST_UPDATE payload is null.", LogLevel.Warning);
                            return;
                        }
                        Log($"Received {peerListFull.Count} peer info item(s) via PEER_LIST_UPDATE.");

                        bool listChanged = false;
                        foreach (var p in peerListFull)
                        {
                            if (string.IsNullOrEmpty(p.OnionAddress) || p.OnionAddress == MyOnion) continue;

                            if (!KnownPeers.TryGetValue(p.OnionAddress, out var existing) ||
                                existing.IsOnline != p.IsOnline ||
                                existing.Username != p.Username ||
                                existing.Bio != p.Bio)
                            {
                                KnownPeers[p.OnionAddress] = p;
                                listChanged = true;
                                try { OnPeerUpdated?.Invoke(p); } catch { }
                            }

                            if (p.IsOnline && !_connections.ContainsKey(p.OnionAddress) && !_cancelledConnections.ContainsKey(p.OnionAddress))
                            {
                                Log($"Discovered online peer '{p.OnionAddress}' without connection. Triggering ConnectToPeerAsync...");
                                _ = ConnectToPeerAsync(p.OnionAddress);
                            }
                        }

                        if (listChanged)
                        {
                            Log("Peer list updated via PEER_LIST_UPDATE. Re-broadcasting full peer list...");
                            _ = BroadcastFullPeerListAsync(exclude: packet.SenderOnion);
                        }
                        break;

                    case "PEERS":
                        Log("Processing PEERS payload...");
                        var peerList = JsonSerializer.Deserialize<List<string>>(packet.Payload);
                        if (peerList == null)
                        {
                            Log("[WARNING] Deserialized peer list is null. Aborting.", LogLevel.Warning);
                            return;
                        }
                        Log($"Received {peerList.Count} peer address(es) via PEERS packet.");

                        foreach (var addr in peerList)
                        {
                            Log($"Evaluating discovered peer address '{addr}'...");
                            if (!KnownPeers.ContainsKey(addr) && addr != MyOnion)
                            {
                                Log($"Discovered new peer address '{addr}'. Triggering ConnectToPeerAsync...");
                                _ = ConnectToPeerAsync(addr);
                                Log($"ConnectToPeerAsync triggered for '{addr}'.");
                            }
                            else
                            {
                                Log($"Discovered address '{addr}' is already known or self. Skipping.");
                            }
                        }
                        break;

                    case "MSG":
                        Log("Processing MSG payload...");
                        var msg = JsonSerializer.Deserialize<ChatMessage>(packet.Payload);
                        if (msg != null)
                        {
                            Log($"Deserialized ChatMessage: Id='{msg.Id}', SenderName='{msg.SenderName}', Text='{msg.Text}'. Invoking OnMessageReceived...");
                            try
                            {
                                OnMessageReceived?.Invoke(msg);
                                Log("OnMessageReceived event invoked successfully.");
                            }
                            catch (Exception eventEx)
                            {
                                Log($"[ERROR] Exception in OnMessageReceived event handler: {eventEx.Message}", LogLevel.Error);
                            }
                        }
                        else
                        {
                            Log("[WARNING] Deserialized ChatMessage payload is null.", LogLevel.Warning);
                        }
                        break;

                    default:
                        Log($"[WARNING] Unrecognized packet type received: '{packet.Type}'", LogLevel.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in ProcessPacket for Packet Id '{packet.Id}': {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log($"[END] ProcessPacket finished for Packet Id '{packet.Id}'.");
        }

        // --- Messaging ---

        public async Task BroadcastMessageAsync(string text)
        {
            Log($"[START] BroadcastMessageAsync started. Message text length={text.Length}");

            try
            {
                var msg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderOnion = MyOnion!,
                    SenderName = MyProfile.Username,
                    Text = text,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                Log($"Created ChatMessage object with Id='{msg.Id}', SenderName='{msg.SenderName}'");

                _seenPacketIds.TryAdd(msg.Id, true);
                Log($"Added local ChatMessage Id '{msg.Id}' to _seenPacketIds.");

                Log("Invoking OnMessageReceived for local UI render...");
                try
                {
                    OnMessageReceived?.Invoke(msg);
                    Log("OnMessageReceived invoked successfully.");
                }
                catch (Exception eventEx)
                {
                    Log($"[ERROR] Exception in OnMessageReceived event handler: {eventEx.Message}", LogLevel.Error);
                }

                var packet = new Packet
                {
                    Id = msg.Id,
                    SenderOnion = MyOnion!,
                    Type = "MSG",
                    Payload = JsonSerializer.Serialize(msg)
                };
                Log($"Created MSG Packet object with Id '{packet.Id}'.");

                Log("Broadcasting MSG packet to all connected peers...");
                await BroadcastPacketAsync(packet);
                Log($"[END] BroadcastMessageAsync completed for ChatMessage Id '{msg.Id}'.");
            }
            catch (Exception ex)
            {
                Log($"[FATAL] Exception in BroadcastMessageAsync: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        public async Task ReconnectAsync()
        {
            Log("[START] ReconnectAsync started. Resetting connections and re-establishing peer mesh...");

            foreach (var (peerKey, connection) in _connections)
            {
                try
                {
                    connection.Close();
                    connection.Dispose();
                }
                catch { }
            }
            _connections.Clear();
            _writers.Clear();
            _connectingPeers.Clear();

            if (!string.IsNullOrEmpty(MyOnion))
            {
                foreach (var peer in KnownPeers.Values.ToList())
                {
                    if (peer.OnionAddress != MyOnion)
                    {
                        Log($"ReconnectAsync: Connecting to '{peer.OnionAddress}'...");
                        _ = ConnectToPeerAsync(peer.OnionAddress);
                    }
                }
            }
            Log("[END] ReconnectAsync completed.");
        }

        public async Task UpdateProfileAsync(string name, string bio)
        {
            Log($"[START] UpdateProfileAsync started. Name='{name}', Bio='{bio}'");

            try
            {
                MyProfile.Username = name;
                MyProfile.Bio = bio;
                Log($"Updated MyProfile properties: Username='{MyProfile.Username}', Bio='{MyProfile.Bio}'");

                Log("Invoking OnPeerUpdated for local profile change...");
                try
                {
                    OnPeerUpdated?.Invoke(MyProfile);
                    Log("OnPeerUpdated invoked.");
                }
                catch (Exception eventEx)
                {
                    Log($"[ERROR] Exception in OnPeerUpdated event handler: {eventEx.Message}", LogLevel.Error);
                }

                var packet = new Packet
                {
                    SenderOnion = MyOnion!,
                    Type = "PROFILE_UPDATE",
                    Payload = JsonSerializer.Serialize(MyProfile)
                };
                Log("Constructed PROFILE_UPDATE packet. Broadcasting...");

                await BroadcastPacketAsync(packet);
                Log("Broadcasting updated full peer list after profile change...");
                await BroadcastFullPeerListAsync();
                Log("[END] UpdateProfileAsync completed successfully.");
            }
            catch (Exception ex)
            {
                Log($"[FATAL] Exception in UpdateProfileAsync: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        public async Task BroadcastFullPeerListAsync(string? exclude = null)
        {
            Log($"[START] BroadcastFullPeerListAsync started. Exclude='{exclude ?? "(none)"}'");
            try
            {
                var list = KnownPeers.Values.ToList();
                if (!string.IsNullOrEmpty(MyOnion) && MyProfile != null)
                {
                    MyProfile.IsOnline = true;
                    if (!list.Any(p => p.OnionAddress == MyOnion))
                    {
                        list.Add(MyProfile);
                    }
                }

                var packet = new Packet
                {
                    SenderOnion = MyOnion ?? "",
                    Type = "PEER_LIST_UPDATE",
                    Payload = JsonSerializer.Serialize(list)
                };

                Log($"Broadcasting PEER_LIST_UPDATE packet with {list.Count} item(s)...");
                string rawJson = JsonSerializer.Serialize(packet);
                await BroadcastRawAsync(rawJson, exclude: exclude);
                Log("[END] BroadcastFullPeerListAsync finished.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in BroadcastFullPeerListAsync: {ex.Message}", LogLevel.Error);
            }
        }

        // --- Transport ---

        private Task BroadcastPacketAsync(Packet packet)
        {
            Log($"[START] BroadcastPacketAsync started for Packet Id='{packet.Id}', Type='{packet.Type}'");
            string json = JsonSerializer.Serialize(packet);
            Log($"Serialized packet to JSON (Length={json.Length}). Calling BroadcastRawAsync...");
            return BroadcastRawAsync(json, exclude: null);
        }

        private async Task SendToAsync(string targetOnion, Packet packet)
        {
            Log($"[START] SendToAsync started. TargetOnion='{targetOnion}', PacketId='{packet.Id}', Type='{packet.Type}'");

            if (_writers.TryGetValue(targetOnion, out var writer))
            {
                string json = JsonSerializer.Serialize(packet);
                Log($"StreamWriter found for '{targetOnion}'. Writing JSON (Length={json.Length}) with Polly retry policy...");

                var sendPolicy = Policy
                    .Handle<IOException>()
                    .Or<SocketException>()
                    .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                        (ex, span, attempt, ctx) =>
                        {
                            Log($"[RETRY] SendToAsync attempt {attempt} to '{targetOnion}' failed: {ex.Message}", LogLevel.Warning);
                        });

                try
                {
                    await sendPolicy.ExecuteAsync(async () =>
                    {
                        await writer.WriteLineAsync(json);
                    });
                    Log($"Packet sent successfully to '{targetOnion}'.");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] SendToAsync failed to write to '{targetOnion}' after retries: {ex.Message}. Removing peer connection.", LogLevel.Error);
                    _writers.TryRemove(targetOnion, out _);
                    if (_connections.TryRemove(targetOnion, out var conn))
                    {
                        try { conn.Close(); conn.Dispose(); } catch { }
                    }
                    if (KnownPeers.TryGetValue(targetOnion, out var peer))
                    {
                        peer.IsOnline = false;
                        OnPeerUpdated?.Invoke(peer);
                    }
                }
            }
            else
            {
                Log($"[WARNING] StreamWriter not found for target onion '{targetOnion}'. Packet not sent.", LogLevel.Warning);
            }

            Log($"[END] SendToAsync completed for '{targetOnion}'.");
        }

        private async Task BroadcastRawAsync(string rawJson, string? exclude)
        {
            Log($"[START] BroadcastRawAsync started. JSON length={rawJson.Length}, ExcludeOnion='{exclude ?? "(none)"}'");

            var writeTasks = new List<Task>();

            var sendPolicy = Policy
                .Handle<IOException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                    (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] BroadcastRawAsync write attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                    });

            foreach (var (onion, writer) in _writers)
            {
                if (onion == exclude)
                {
                    Log($"Excluding peer '{onion}' from broadcast.");
                    continue;
                }

                Log($"Adding write task for peer '{onion}'...");
                writeTasks.Add(sendPolicy.ExecuteAsync(async () =>
                {
                    await writer.WriteLineAsync(rawJson);
                }).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Log($"[ERROR] Broadcast write to peer '{onion}' failed after retries: {t.Exception?.GetBaseException().Message}. Cleaning up peer.", LogLevel.Error);
                        _writers.TryRemove(onion, out _);
                        if (_connections.TryRemove(onion, out var conn))
                        {
                            try { conn.Close(); conn.Dispose(); } catch { }
                        }
                        if (KnownPeers.TryGetValue(onion, out var peer))
                        {
                            peer.IsOnline = false;
                            OnPeerUpdated?.Invoke(peer);
                        }
                    }
                }));
            }

            Log($"Awaiting {writeTasks.Count} broadcast write tasks via Task.WhenAll...");
            await Task.WhenAll(writeTasks);
            Log("[END] BroadcastRawAsync finished.");
        }
    }
}
