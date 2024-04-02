using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.SDK.API;
using CodeProject.AI.API;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// This class uses a UdpClient to send and receive broadcast messages on the network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is responsible for managing a network or mesh of servers and enabling service 
    /// discovery and monitoring. It provides functionality for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Broadcasting and receiving heartbeat messages</description></item>
    /// <item><description>Marking services as active or inactive based on their last 
    /// heartbeat</description></item>
    /// <item><description>Broadcasting goodbye messages when services are removed from the 
    /// network</description></item>
    /// </list>
    /// <para>The manager maintains a dictionary of discovered services and their status. It also
    /// allows for a custom status builders to be used to create the status sent in the heartbeat
    /// message.</para>
    /// <para>Methods are provided for starting and stopping the service discovery and monitoring
    /// process. Events are provided for when services become active, inactive, or are closed.</para>
    /// <para>See the MeshNodeStatus.cs file to create a Mesh class that uses this class, a custom
    /// status class and custom status builder.</para>
    /// </remarks>
    /// <typeparam name="TStatus">The Type of the status data included in the HEARTBEAT message.</typeparam>
    public class BaseMeshMonitor<TStatus>
        where TStatus: MeshServerBroadcastData, new()
    {
        private const string HeartBeatId           = "HEARTBEAT";
        private const string GoodByeId             = "GOODBYE";
        private const string NotStartedMessage     = "ServerMeshMonitor is not started";
        private const string AlreadyStartedMessage = "ServerMeshMonitor is already started";
        private const char   Separator             = '|';
        private const string HostnameEnvVar        = "COMPUTERNAME";

        private UdpClient? _udpClient = null;
        
        private ApiClient? _pingClient = null;
        private readonly List<KnownMeshServerPingStatus> _knownServers = new();

        // An address of this machine that is connected to the internet. It may not be the only
        // address, though
        private readonly IPAddress       _activeIPAddress = IPAddress.Any;

        // All addresses found on this machine.
        private readonly List<IPAddress> _localIPAddresses = new();

        // dependencies
        private readonly IOptionsMonitor<MeshOptions> _monitoredMeshOptions;
        private MeshOptions _oldMeshOptions;
        private readonly IMeshServerBroadcastBuilder<TStatus> _statusBuilder;
        private readonly ILogger<BaseMeshMonitor<TStatus>> _logger;

        private readonly ConcurrentDictionary<string, MeshServerRoutingEntry> _discoveredServers = new();

        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _listenerTask;
        private Task? _pingServersTask;
        private Task? _heartbeatTask;
        private Task? _inactiveTask;

        /// <summary>
        /// Gets a value indicating whether requests to mesh servers will be done using host names
        /// or IP addresses
        /// </summary>
        public bool RouteViaHostname => true;

        /// <summary>
        /// Gets the dictionary of discovered services.
        /// </summary>
        public IReadOnlyDictionary<string, MeshServerRoutingEntry> DiscoveredServers => _discoveredServers;

        /// <summary>
        /// A value indicating whether the service discovery and monitoring process is running.
        /// </summary>
        public bool IsRunning => _udpClient != null;

        /// <summary>
        /// Gets a value indicating whether the service discovery and monitoring process is enabled.
        /// </summary>
        public bool IsEnabled => CurrentMeshOptions.Enable && 
                                 (CurrentMeshOptions.EnableStatusBroadcast || 
                                  CurrentMeshOptions.EnableStatusMonitoring);

        /// <summary>
        /// Gets or sets the Action that is called when a service is marked as active.
        /// </summary>
        public Action<MeshServerRoutingEntry>? OnActive { get; set; } = null;

        /// <summary>
        /// Gets or sets the Action that is called when a service is marked as inactive.
        /// </summary>
        public Action<MeshServerRoutingEntry>? OnInActive { get; set; } = null;

        /// <summary>
        /// Gets or sets the Action that is called when a service is closed.
        /// </summary>
        public Action<MeshServerRoutingEntry>? OnClose { get; set; } = null;

        /// <summary>
        /// Gets the MeshConfig.
        /// </summary>
        public MeshOptions CurrentMeshOptions => _monitoredMeshOptions.CurrentValue;

        /// <summary>
        /// Gets the current mesh status
        /// </summary>
        public TStatus MeshStatus => _statusBuilder.Build(this);

        /// <summary>
        /// Gets the IPAddress of the local machine that we know is active.
        /// </summary>
        public IPAddress LocalIPAddress => _activeIPAddress;

        /// <summary>
        /// Gets the name of the local server.
        ///</summary>
        /// <remarks>
        /// 1. LocalHostname is the local NetBIOS name for this computer which is NOT guaranteed to
        ///    be unique on a network.
        /// 2. If there is an environment variable present under the key 'HostnameEnvVar' then that
        ///    value will be used in preference to what the current OS reports. This is done to 
        ///    override systems such as Docker which provide a local name instead of the host name.
        /// </remarks>
        public string LocalHostname => Environment.GetEnvironmentVariable(HostnameEnvVar) 
                                    ?? SystemInfo.MachineName;

        /// <summary>
        /// Creates a new instance of the BaseMeshMonitor class.
        /// </summary>
        public BaseMeshMonitor(IOptionsMonitor<MeshOptions> meshConfig,
                               IMeshServerBroadcastBuilder<TStatus> statusBuilder,
                               ILogger<BaseMeshMonitor<TStatus>> logger)
        {
            _monitoredMeshOptions = meshConfig;
            _statusBuilder        = statusBuilder;
            _logger               = logger!;
            _oldMeshOptions       = meshConfig.CurrentValue;

            // Get the local address using a UDP socket. This is a better way to get the local
            // address because it will return the address of the network adapter that is connected
            // to the network.
            // See https://stackoverflow.com/a/42098280
            using (Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("8.8.8.8", 65530); // IP doesn't actually need to be connected
                }
                catch
                {
                }
                IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                _activeIPAddress = endPoint?.Address ?? IPAddress.Any;
            }

            // set _localAddress as the first IPv4 address of the local machine
            try
            {
                foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                        _localIPAddresses.Add(address);
                }
            }
            catch
            {
                // can get "nodename nor servname provided, or not known"
            }

            // Setup the list of known servers
            foreach (string hostname in CurrentMeshOptions.KnownMeshHostnames)
                RegisterKnownServer(hostname);

            // If the settings change then handle this. The settings can change in two places: the
            // appsettings.*.json files in the application root, or in the serversettings.json file
            // in the persisted data folder (eg C:\ProgramData\CodeProject\AI in Windows).
            meshConfig.OnChange(async (config, _) => { await OnChange(config); });

            // Start the service if it is enabled.
            if (IsEnabled)
                StartMonitoring();
        }

        /// <summary>
        /// Starts the service discovery and monitoring process.
        /// </summary>
        public void StartMonitoring()
        {
            // don't start if the service is not enabled
            if (!IsEnabled)
                return;

            bool portInUse = IPGlobalProperties.GetIPGlobalProperties()
                                               .GetActiveUdpListeners()
                                               .Any(p => p.Port == CurrentMeshOptions.Port);
            if (portInUse)
            {
                _logger.LogError($"Unable to start mesh monitoring. UDP Port {CurrentMeshOptions.Port} is already in use");
                return;
            }

            try
            {
                // Note that CurrentMeshOptions.Enable will have to be true for this code to be
                // reached, so no need to test for CurrentMeshOptions.Enable.

                // Note that we only create the clients and cancel token if and when needed
                if (CurrentMeshOptions.EnableStatusBroadcast)
                {
                    _cancellationTokenSource ??= new CancellationTokenSource();

                    _udpClient ??= new UdpClient(CurrentMeshOptions.Port);
                    _udpClient.EnableBroadcast = true;
                
                    _logger.LogInformation($"** Starting mesh broadcasting");
                    _heartbeatTask ??= BroadcastHeartbeatLoopAsync(_cancellationTokenSource.Token);
                }

                if (CurrentMeshOptions.EnableStatusMonitoring)
                {
                    _cancellationTokenSource ??= new CancellationTokenSource();

                    _udpClient ??= new UdpClient(CurrentMeshOptions.Port);
                    _udpClient.EnableBroadcast = true;

                    _logger.LogInformation($"** Starting mesh broadcast monitoring");
                    _listenerTask    ??= ListenForBroadcastsLoopAsync(_cancellationTokenSource.Token);

                    // Even if there are no known servers now, we may have another server register 
                    // itself in the future. So: start the pinging task, regardless.
                    _pingClient ??= new ApiClient(CurrentMeshOptions.Port);
                    _pingClient.Timeout = (int)CurrentMeshOptions.MeshServerPingTimeout.TotalSeconds;

                    _logger.LogInformation($"** Starting known mesh server pinging");
                    _pingServersTask ??= PingKnownMeshServersLoopAsync(_cancellationTokenSource.Token);

                    _inactiveTask    ??= CheckInactiveServices(_cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to start mesh monitoring.");
            }
        }

        /// <summary>
        /// Signals the server discovery and monitoring process to stop, broadcasts a GOODBYE message
        /// and closes the UdpClient.
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            _cancellationTokenSource?.Cancel();
            
            if (_listenerTask is not null)
                await _listenerTask;
            _listenerTask = null;

            if (_pingServersTask is not null)
                await _pingServersTask;
            _pingServersTask = null;

            if (_heartbeatTask is not null)
                await _heartbeatTask;
            _heartbeatTask = null;

            if (_inactiveTask is not null)
                await _inactiveTask;
             _inactiveTask = null;

            try
            {
                await BroadcastGoodbyeAsync();
            }
            catch
            {
                // Ignore exceptions
            }

            _pingClient?.Dispose();
            _pingClient = null;

            _udpClient?.Dispose();
            _udpClient = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        /// <summary>
        /// Signals the server discovery and monitoring process to stop and restart.
        /// </summary>
        public async Task RestartMonitoringAsync()
        {
            // Stop the service if it is running and is no longer enabled.
            if (IsRunning)
                await StopMonitoringAsync();

            // Start the service if it is enabled and not running.
            if (IsEnabled)
                StartMonitoring();
        }

        /// <summary>
        /// Called to manually update the MeshOptions and trigger a OnChange event
        /// </summary>
        /// <param name="config">The mesh options</param>
        /// <param name="forceRestart">Whether or not to force the mesh monitor to restart.</param>
        public async Task UpdateOptions(MeshOptions config, bool forceRestart = false)
        {
            await OnChange(config, forceRestart);
        }

        /// <summary>
        /// Registers a server in the list of known servers.
        /// </summary>
        /// <param name="hostname">The hostname to register</param>
        /// <returns>True on success; false otherwise</returns>
        public bool RegisterKnownServer(string hostname)
        {
            // If it's already registered we're all good
            if (IsKnownServer(hostname))
                return true;

            var pingStatus = new KnownMeshServerPingStatus(hostname);
            pingStatus.CheckIsLoopback(LocalIPAddress, _localIPAddresses);
            _knownServers.Add(pingStatus);

            return true;
        }

        /// <summary>
        /// Checks if a hostname is registered as a known server.
        /// </summary>
        /// <param name="hostname">The hostname to check</param>
        /// <returns>True if registered; false otherwise</returns>
        public bool IsKnownServer(string hostname)
        {
            // If it's already registered we're all good
            return _knownServers.Any(s => s.Hostname.EqualsIgnoreCase(hostname));
        }

        /// <summary>
        /// Removes a server from the list of known servers.
        /// </summary>
        /// <param name="hostname">The hostname to remove</param>
        /// <returns>True on success; false otherwise</returns>
        public bool UnregisterKnownServer(string hostname)
        {
            if (_knownServers is null)
                return false;

            for (int index = 0; index < _knownServers.Count; index++)
            {
                if (_knownServers[index].Hostname.EqualsIgnoreCase(hostname))
                {
                    _knownServers.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        private async Task OnChange(MeshOptions config, bool forceRestart = false)
        {
            // check for changes in the MeshConfig properties that require restarting the service
            bool restart = config.Enable                 != _oldMeshOptions.Enable                ||
                           config.EnableStatusBroadcast  != _oldMeshOptions.EnableStatusBroadcast ||
                           config.EnableStatusMonitoring != _oldMeshOptions.EnableStatusMonitoring;

            // Force a restart when we're turning on/off the server to ensure the udpClient is
            // created or destroyed.
            if ((!IsRunning && config.Enable) || (IsRunning && !config.Enable))
                restart = true;

            if (forceRestart || restart)
            {
                _oldMeshOptions = config;
                await RestartMonitoringAsync().ConfigureAwait(false);
            }

            await SendHeartbeatAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Listens for broadcast messages from other services.
        /// </summary>
        /// <param name="cancellationToken">The Cancellation Token used to stop the Task.</param>
        /// <returns>The Task.</returns>
        private async Task ListenForBroadcastsLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_udpClient == null)
                        throw new InvalidOperationException(NotStartedMessage);

                    // Get the next message and process
                    UdpReceiveResult receiveResult = await _udpClient.ReceiveAsync(cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // The IP address we see; not the IP address the remote server thinks it has.
                    IPAddress endPointIPAddress = receiveResult.RemoteEndPoint.Address;

                    // Split the message into it's parts and handle accordingly
                    string message = Encoding.ASCII.GetString(receiveResult.Buffer);
                    string[] messageParts = message.Split(Separator);
                    if (messageParts.Length < 3)
                       continue;

                    string broadcastServiceName = messageParts[0];
                    string messageTypeId        = messageParts[1];
                    string statusJson           = messageParts[2];

                    // If it's not for us we leave
                    if (broadcastServiceName != CurrentMeshOptions.ServiceName)
                        continue;

                    TStatus? status;
                    try
                    {
                        status = JsonSerializer.Deserialize<TStatus>(statusJson, _jsonSerializerOptions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing status message from " +
                                             endPointIPAddress.ToString());
                        continue;
                    }

                    if (status is null)
                        continue;

                    // Each server may have multiple IP addresses, and each server will hear its
                    // own messages that are broadcast. We need to know if the message we received
                    // is actually from this server itself. If it is then we'll normalise the IP to
                    // be the local IP we saw from sending out a UDP probe earlier
                    bool isLocal = false;
                    foreach (IPAddress localAddress in _localIPAddresses)
                    {
                        // use .Equals() to compare the IPAddress, because '==' will compare the
                        // object reference.
                        // We can't *only* check hostname, since they aren't guaranteed to be unique.
                        // We want to see matching hostnames AND a matching IP address for this to
                        // be considered local (which isn't strictly necessary: we could just check
                        // IP addresses and be done with it)
                        if (endPointIPAddress.Equals(localAddress) && 
                            status.Hostname.EqualsIgnoreCase(LocalHostname))
                        {
                            endPointIPAddress = LocalIPAddress; // normalise to a single IP address
                            isLocal           = true;
                            break;
                        }
                    }

                    string serverKey = status.Hostname;

                    if (messageTypeId == HeartBeatId)               // Process the HEARTBEAT message
                    {
                        MeshServerRoutingEntry? server;
                        if (_discoveredServers.ContainsKey(serverKey))
                        {
                            server = _discoveredServers[serverKey];
                        }
                        else
                        {
                            // The hostname for a docker container isn't a hostname that can be used
                            // to send messages to the server. For that you use the host computer's
                            // hostname. All we have is IP address (EXCEPT if we pass the host
                            // computer's name to the docker container manually)
                            // TIP: Hostname is $HOSTNAME in linux, %COMPUTERNAME% in Windows (do not
                            // use %LOGONSERVER% on windows because that's a different thing)
                            string callableHostname = status.Hostname;
                            if (status.Platform.EqualsIgnoreCase("Docker"))
                                callableHostname = endPointIPAddress.ToString();

                            // QUICK CHECK: If we have this hostname in our "known server" list then
                            // remove it from there so we manage it from here.
                            UnregisterKnownServer(callableHostname);

                            server = new MeshServerRoutingEntry(callableHostname,
                                                                endPointIPAddress.ToString(),
                                                                isLocal);

                            _discoveredServers[serverKey] = server;
                        }

                        // We'll always update the serviceStatus.Status with the latest and greatest.
                        server.LastContactTime = DateTime.UtcNow;
                        server.Status          = status!;

                        // The service is always active if we've seen activity, but if this is a
                        // service that was newly added then we need to call the OnActive callback
                        // for that service
                        if (!server.IsActive)
                        {
                            OnActive?.Invoke(server);
                            server.IsActive = true;
                        }

                        // This server we just heard from may not know about us. Let it know if needed.
                        await PingBackServerIfNecessary(server);
                    }
                    else if (messageTypeId == GoodByeId)              // Process the GOODBYE message
                    {
                        if (_discoveredServers.TryRemove(serverKey, out var serviceStatus))
                            OnClose?.Invoke(serviceStatus);
                    }

                    // Ignore other messages
                }
                catch (SocketException /*ex*/)
                {
                    // _logger.LogError(ex, "SocketException in ListenForBroadcastsLoopAsync");
                }
                catch (ObjectDisposedException /*ex*/)
                {
                    // _logger.LogError(ex, "ObjectDisposedException in ListenForBroadcastsLoopAsync");
                }
                catch (OperationCanceledException /*ex*/)
                {
                    // _logger.LogError(ex, "OperationCanceledException in ListenForBroadcastsLoopAsync");
                }
            }
        }

        /// <summary>
        /// Pings each server in our list of known servers (_meshOptions.IPAddresses) to get the
        /// information that would normally be found via UDP broadcast. The reason these servers are
        /// on the IPAddress list is because they are unable to reach this server via UDP broadcast.
        /// </summary>
        /// <param name="cancellationToken">The Cancellation Token used to stop the Task.</param>
        /// <returns>The Task.</returns>
        private async Task PingKnownMeshServersLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_pingClient == null)
                    throw new InvalidOperationException(NotStartedMessage);

                if (cancellationToken.IsCancellationRequested)
                    break;

                foreach (KnownMeshServerPingStatus knownServer in _knownServers)
                {
                    // Is there any point?
                    if (knownServer.IsLoopback || string.IsNullOrWhiteSpace(knownServer.Hostname))
                        continue;

                    // Have we seen an error recently?
                    if (knownServer.LastStatusCode != HttpStatusCode.OK &&
                        DateTime.UtcNow - knownServer.LastErrorUtcTime < CurrentMeshOptions.PingErrorRecoveryTimeout)
                    {
                        continue;
                    }

                    string callableHostname = knownServer.Hostname;
                    knownServer.LastErrorUtcTime = DateTime.MinValue;

                    TStatus? status = default;

                    _pingClient.Hostname = callableHostname;
                    ServerResponse response = await _pingClient.GetAsync<TStatus>("server/mesh/status")
                                                               .ConfigureAwait(false);
                    // Health check
                    if (response is null)
                    {
                        knownServer.LastStatusCode = HttpStatusCode.ServiceUnavailable;
                    }
                    else if (response.Code != HttpStatusCode.OK)
                    {
                        knownServer.LastStatusCode = response.Code;
                    }
                    else if (response is TStatus theStatus)
                    {
                        status = theStatus;
                        knownServer.LastStatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        knownServer.LastStatusCode = HttpStatusCode.InternalServerError;
                    }

                    if (knownServer.LastStatusCode != HttpStatusCode.OK)
                    {
                        status = (TStatus) new MeshServerBroadcastData()
                        {
                            Code              = response?.Code ?? HttpStatusCode.InternalServerError,
                            Hostname          = callableHostname,
                            SystemDescription = "Unknown: server unresponsive",
                            Platform          = "Unknown"
                        };
                        _logger.LogWarning($"Unable to ping mesh server '{callableHostname}'. " +
                                           "Pausing pings temporarily.");
                        knownServer.LastErrorUtcTime = DateTime.UtcNow;
                        // continue;
                    }

                    string serverKey = status!.Hostname;
                    MeshServerRoutingEntry? server;
                    if (_discoveredServers.ContainsKey(serverKey))
                    {
                        server = _discoveredServers[serverKey];
                    }
                    else
                    {
                        server = new MeshServerRoutingEntry(knownServer.Hostname, null,
                                                            knownServer.IsLoopback);
                        _discoveredServers[serverKey] = server;
                    }

                    // We'll always update the serviceStatus.Status with the latest and greatest.
                    server.LastContactTime = DateTime.UtcNow;
                    server.Status          = status;

                    if (response != null && response.Code == HttpStatusCode.OK)
                    {
                        if (!server.IsActive)
                        {
                            OnActive?.Invoke(server);
                            server.IsActive = true;
                        }

                        // This server we just heard from may not know about us. Let it know if needed.
                        await PingBackServerIfNecessary(server);
                    }
                }

                try
                {
                    await Task.Delay(CurrentMeshOptions.ServerPingInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // The loop will terminate if the cancellation token is cancelled.
                }
            }
        }

        /// <summary>
        /// Alerts a server that we are here if it doesn't know about us.
        /// </summary>
        /// <param name="server">The <see cref="MeshServerRoutingEntry"/> object that has sent a
        /// broadcast</param>
        /// <returns>True if successful, false otherwise. NOTE: true doesn't mean it pinged. It
        /// means the method didn't see a problem, regardless of whether it needed to ping the other
        /// server</returns>
        private async Task<bool> PingBackServerIfNecessary(MeshServerRoutingEntry server)
        {
            if (_pingClient == null)
                return false;

            if (server.IsLocalServer)
                return true;

            // No need to ping if the other server knows about us.           
            if (server.Status.KnownHostnames is not null)
                foreach (var knownHostname in server.Status.KnownHostnames)
                    if (knownHostname.EqualsIgnoreCase(LocalHostname))
                        return true;

            // It's oblivious to us. Let's correct that blatant snub
            string route = $"server/mesh/register/{LocalHostname}";
            _pingClient.Hostname = server.CallableHostname;
            var response = await _pingClient.PostAsync<ServerResponse>(route).ConfigureAwait(false);

            return response?.Code == HttpStatusCode.OK;
        }

        /// <summary>
        /// This Task broadcasts a HEARTBEAT message to other servers.
        /// </summary>
        /// <param name="cancellationToken">The Cancellation Token to stop the Task.</param>
        /// <returns>The Task.</returns>
        private async Task BroadcastHeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await SendHeartbeatAsync(cancellationToken);
                    await Task.Delay(CurrentMeshOptions.HeartbeatInterval, cancellationToken);
                }
                catch (SocketException /*ex*/)
                {
                    // _logger.LogError(ex, "SocketException in BroadcastHeartbeatLoopAsync");
                }
                catch (ObjectDisposedException /*ex*/)
                {
                    // _logger.LogError(ex, "ObjectDisposedException in BroadcastHeartbeatLoopAsync");
                }
                catch (OperationCanceledException)
                {
                    // Ignore operation canceled exceptions
                }
                catch (Exception /*ex*/)
                {
                    // _logger.LogError(ex, "Exception in BroadcastHeartbeatLoopAsync");
                }
            }
        }

        /// <summary>
        /// This is public in case the caller wants to send a HEARTBEAT message outside of the
        /// normal loop. For example, if an outside process starts up the monitor they want may want
        /// to immediately broadcast a heartbeat rather than waiting.
        /// </summary>
        /// <remarks>
        /// This could be when modules are started or stopped so that remote service get updated
        /// immediately.
        /// </remarks>
        /// <param name="cancellationToken"></param>
        /// <returns>A task</returns>
        public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
        {
            // Don't send a HEARTBEAT message if the service discovery and monitoring process is not
            // running.
            if (!IsEnabled || !IsRunning)
                return;

            try
            {
                // TODO: make sure the jsonString does not contain the separator character.
                string jsonStatus = JsonSerializer.Serialize(MeshStatus, _jsonSerializerOptions);
                string message    = $"{CurrentMeshOptions.ServiceName}{Separator}{HeartBeatId}{Separator}{jsonStatus}";
                await BroadcastMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SendHeartbeatAsync");
            }
        }

        /// <summary>
        /// Broadcasts a GOODBYE message to other servers.
        /// </summary>
        /// <returns>A task</returns>
        private Task BroadcastGoodbyeAsync(CancellationToken token = default)
        {
            // TODO: make sure the jsonString does not contain the separator character.
            string jsonStatus = JsonSerializer.Serialize(MeshStatus, _jsonSerializerOptions);
            string message    = $"{CurrentMeshOptions.ServiceName}{Separator}{GoodByeId}{Separator}{jsonStatus}";
            return BroadcastMessageAsync(message, token);
        }

        /// <summary>
        /// Broadcasts a message to other servers.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="token">The CancellationToken</param>
        /// <returns></returns>
        private async Task BroadcastMessageAsync(string message, CancellationToken token=default)
        {
            // Check if the UDP client is null here. If it is null then the service is not running
            // and we don't need, nor are able, to broadcast the message. Note that even if the
            // service is being stopped, the UDP client will not be null here because the
            // StopMonitoringAsync method sets the UDP client to null *after* broadcasting the
            // GOODBYE message, which we want to broadcast.
            if (!IsRunning)
                return;

            byte[] messageBytes = Encoding.ASCII.GetBytes(message);
            var packet = new ReadOnlyMemory<byte>(messageBytes);

            try
            {
                //await _udpClient!.SendAsync(messageBytes, messageBytes.Length,
                //                            new IPEndPoint(IPAddress.Broadcast, CurrentMeshOptions.Port));
                await _udpClient!.SendAsync(packet, new IPEndPoint(IPAddress.Broadcast, CurrentMeshOptions.Port),
                                            token);
            }
            catch (Exception ex)
            {

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogError(ex, "Exception in BroadcastMessageAsync");
                // Ignore socket exceptions
            }
        }

        /// <summary>
        /// This Task checks the status of the discovered services and marks them as inactive 
        /// if they have not sent a HEARTBEAT message in the specified time.
        /// </summary>
        /// <param name="cancellationToken">The Cancellation Token used to stop the Task.</param>
        /// <returns>The Task.</returns>
        private async Task CheckInactiveServices(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Mark services as inactive if they have not sent a HEARTBEAT message in the specified time
                    foreach (MeshServerRoutingEntry serviceStatus in _discoveredServers.Values)
                    {
                        TimeSpan timeSinceLastRequest = DateTime.UtcNow - serviceStatus.LastContactTime;
                        if (serviceStatus.IsActive && timeSinceLastRequest > CurrentMeshOptions.HeartbeatInactiveTimeout)
                        {
                            serviceStatus.IsActive = false;
                            OnInActive?.Invoke(serviceStatus);
                        }
                    }
                    await Task.Delay(CurrentMeshOptions.HeartbeatInterval, cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogError("ObjectDisposedException in CheckInactiveServices");
                    // Ignore object disposed exceptions
                }
                catch (TaskCanceledException)
                {
                    // Ignore task canceled exceptions
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception in CheckInactiveServices");
                    // Ignore other exceptions
                }
            }
        }
    }
}
