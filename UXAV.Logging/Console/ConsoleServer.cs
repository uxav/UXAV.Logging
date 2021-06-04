using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Crestron.SimplSharp;
using IPAddress = System.Net.IPAddress;
// ReSharper disable InconsistentNaming

namespace UXAV.Logging.Console
{
    internal sealed class ConsoleServer
    {
        private readonly Dictionary<int, ConsoleConnection> _connections = new Dictionary<int, ConsoleConnection>();
        private Thread _listeningThread;
        private bool _listening;
        private string _csPortForwardIp;
        private const byte IAC = 255;
        private const byte DO = 253;
        private const byte WILL = 251;
        private const byte ECHO = 1;
        private const byte SUPPRESS_GO_AHEAD = 3;
        private const byte TERMINAL = 24;

        public event ReceivedCommandEventHandler ReceivedCommand;

        public IEnumerable<ConsoleConnection> Connections
        {
            get
            {
                lock (_connections)
                {
                    return _connections.Values;
                }
            }
        }

        internal void Start(int portNumber)
        {
            Port = portNumber;
            if (_listeningThread != null && _listeningThread.ThreadState == ThreadState.Running)
            {
                throw new Exception("Logger already running");
            }
            
            try
            {
                if (InitialParametersClass.NumberOfExternalEthernetInterfaces > 0)
                {
                    Logger.Log("Control system has external ethernet interfaces, will attempt to map port to internal IP");
                    var id = CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetCSAdapter); 
                    var controlSubnetIp = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, id);
                    Logger.Log($"CS IP Address is: {controlSubnetIp}");
                    var result = CrestronEthernetHelper.AddPortForwarding((ushort) portNumber, (ushort) portNumber,
                        controlSubnetIp, CrestronEthernetHelper.ePortMapTransport.TCP);
                    if (result == CrestronEthernetHelper.PortForwardingUserPatRetCodes.NoErr)
                    {
                        _csPortForwardIp = controlSubnetIp;
                        Logger.Log(
                            $"Port Fowarding result = {result}, forwarded TCP port {portNumber} to internal IP: {controlSubnetIp}");
                    }
                    else
                    {
                        Logger.Warn($"Could not create port forwarding for Logger, result = {result}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            _listeningThread = new Thread(ListenProcess) {Name = "LoggerRx" + InitialParametersClass.ApplicationNumber};
            _listeningThread.Start();
            Logger.Highlight($"Logger started console on port {portNumber}");
        }

        public int Port { get; private set; }

        public bool Listening => _listening;

        private void ListenProcess()
        {
            var server = new TcpListener(IPAddress.Any, Port);

            try
            {
                server.Start();
                _listening = true;
                Logger.Highlight("Started listening for console connections on port {0}", Port);
            }
            catch (Exception e)
            {
                Logger.Error("Could not start console server socket on port {0}, {1}", Port, e.Message);
                return;
            }

            try
            {
                while (_listening)
                {
                    var client = server.AcceptTcpClient();

                    var negotiated = false;
                    var stream = client.GetStream();
                    var bytes = new byte[256];

                    stream.Write(new[] {IAC, DO, TERMINAL}, 0, 3);

                    try
                    {
                        // ReSharper disable once NotAccessedVariable
                        int i;
                        while (!negotiated && (client.Connected && (i = stream.Read(bytes, 0, bytes.Length)) != 0))
                        {
                            if (bytes[0] != IAC || bytes.Length < 3) continue;
                            if (bytes[1] != WILL || bytes[2] != TERMINAL) continue;

                            stream.Write(new[] {IAC, WILL, ECHO}, 0, 3);
                            stream.Write(new[] {IAC, WILL, SUPPRESS_GO_AHEAD}, 0, 3);

                            negotiated = true;
                        }
                    }
                    catch (Exception e)
                    {
                        if (!(e is IOException))
                        {
                            Logger.Error("Error in telnet negotiation loop", e.Message);
                        }
                    }

                    if (!client.Connected)
                    {
                        Logger.Highlight("Exiting, client has disconnected");
                        return;
                    }

                    var connectionId = 1;

                    lock (_connections)
                    {
                        while (_connections.ContainsKey(connectionId))
                        {
                            connectionId++;
                        }

                        _connections[connectionId] =
                            new ConsoleConnection(connectionId, client, OnReceivedCommand, DisposeConnection);
                    }
                }
            }
            catch (Exception e)
            {
                if(e is ThreadAbortException) return;
                ErrorLog.Error($"Error in {GetType().Name}.ListenProcess()", e.Message);
            }
        }

        private void DisposeConnection(int connectionId)
        {
            lock (_connections)
            {
                _connections.Remove(connectionId);
            }
        }

        internal void Stop()
        {
            _listening = false;
            ConsoleConnection[] clients;

            lock (_connections)
            {
                clients = _connections.Values.ToArray();
            }

            if (_listeningThread != null && _listeningThread.ThreadState == ThreadState.Running)
            {
                _listeningThread.Abort();
            }

            foreach (var client in clients)
            {
                client.Dispose();
            }
            
            if(string.IsNullOrEmpty(_csPortForwardIp)) return;

            try
            {
                Logger.Log("Attempting to remove port forwarding for Logger...");
                var result = CrestronEthernetHelper.RemovePortForwarding((ushort) Port, (ushort) Port, _csPortForwardIp,
                    CrestronEthernetHelper.ePortMapTransport.TCP);
                if (result == CrestronEthernetHelper.PortForwardingUserPatRetCodes.NoErr)
                {
                    Logger.Log("Port forwarding removed ok!");
                    return;
                }
                Logger.Warn($"Could not remove port forwarding for Logger, result = {result}");
                _csPortForwardIp = null;
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public static string GetPrompt()
        {
            return CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance
                ? $"\r\n{AnsiColors.Green}{InitialParametersClass.ControllerPromptName}{AnsiColors.Reset}> "
                : $"\r\n{AnsiColors.Green}{InitialParametersClass.RoomId}{AnsiColors.Reset}> ";
        }

        private void OnReceivedCommand(ReceivedCommandEventArgs args)
        {
            ReceivedCommand?.Invoke(this, args);
        }
    }
}