using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Figgle;
using IPEndPoint = System.Net.IPEndPoint;
using Stream = System.IO.Stream;

namespace UXAV.Logging.Console
{
    public class ConsoleConnection : IDisposable
    {
        private readonly Action<ReceivedCommandEventArgs> _receivedCommandCallBack;
        private readonly Action<int> _callBackOnClose;
        private bool _promptWritten;

        internal ConsoleConnection(int connectionId, TcpClient client,
            Action<ReceivedCommandEventArgs> receivedCommandCallBack, Action<int> callBackOnClose)
        {
            LogStreamLevel = Logging.Logger.DefaultLogStreamLevel;
            ConnectionId = connectionId;
            Client = client;
            _receivedCommandCallBack = receivedCommandCallBack;
            _callBackOnClose = callBackOnClose;

            var remoteEndpoint = (IPEndPoint) client.Client.RemoteEndPoint;
            var localEndpoint = (IPEndPoint) client.Client.LocalEndPoint;
            var remoteIp = remoteEndpoint.Address;
            Logging.Logger.Highlight("Accepted logger console connection from \"{0}\" on port {1}", remoteIp,
                localEndpoint.Port);
            ThreadPool.QueueUserWorkItem(ClientThreadProcess);
        }

        public int ConnectionId { get; }

        public IPEndPoint RemoteEndpoint => Client.Client.RemoteEndPoint as IPEndPoint;

        internal TcpClient Client { get; }

        public Logging.Logger.LoggerLevel LogStreamLevel { get; internal set; }

        private void ClientThreadProcess(object state)
        {
            try
            {
                var bytes = new byte[256];
                var stream = Client.GetStream();

                WriteLine(string.Empty);
                WriteLine(string.Empty);
                WriteLine(AnsiColors.Blue + FiggleFonts.Standard.Render("AVnet Logger") + AnsiColors.Reset);

                WriteLine(AnsiColors.BrightYellow + Environment.NewLine + "AVnet Console Logger for App {0} ({1}) 👍\r\n" +
                          AnsiColors.Reset, InitialParametersClass.ApplicationNumber, InitialParametersClass.RoomId);

                WriteLine(AnsiColors.White + Environment.NewLine +
                          $"Log streaming is set to \"{LogStreamLevel}\" for this connection\r\n" + AnsiColors.Reset);

                var remoteIp = ((IPEndPoint) Client.Client.RemoteEndPoint).Address;
                Logging.Logger.Log("Logger thread process started for {0}", remoteIp);

                WritePrompt();

                var buffer = new byte[10000];
                var bufferLength = 0;

                try
                {
                    int readLen;
                    while (Client.Connected && (readLen = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        if (bytes[0] == 255)
                        {
                            continue;
                        }

                        for (var readIndex = 0; readIndex < readLen; readIndex++)
                        {
                            var readChar = bytes[readIndex];

                            if (readChar == 13)
                            {
                                var data = Encoding.UTF8.GetString(buffer, 0, bufferLength);
                                bufferLength = 0;

                                if (data == "bye")
                                {
                                    Write("Closing connection!");
                                    Client.Close();
                                }
                                else
                                {
                                    stream.Write(new byte[] {0x0d, 0x0a}, 0, 2);
                                    OnReceiveLine(data);
                                }
                            }
                            else if (readChar == 0)
                            {
                                // skip
                            }
                            else if (readChar == 9)
                            {
                                if (bufferLength == 0)
                                {
                                    stream.Write(new byte[] {0x07}, 0, 1);
                                    break;
                                }

                                var currentTypedCommand = Encoding.UTF8.GetString(buffer, 0, bufferLength).ToLower();
                                var possibleCommands =
                                    Logging.Logger.CommandNames.Where(n => n.ToLower().StartsWith(currentTypedCommand))
                                        .ToArray();
                                if (possibleCommands.Length == 0)
                                {
                                    stream.Write(new byte[] {0x07}, 0, 1);
                                    break;
                                }

                                if (possibleCommands.Length > 1)
                                {
                                    var maxLen = possibleCommands.Select(cmd => cmd.Length).Concat(new[] {0}).Max();
                                    var minLen = possibleCommands.Select(cmd => cmd.Length).Concat(new[] {maxLen})
                                        .Min();
                                    int matchedLen;
                                    for (matchedLen = 0; matchedLen < minLen; matchedLen++)
                                    {
                                        var matchedChar = possibleCommands.First().ToLower()[matchedLen];
                                        if (possibleCommands.Any(c => c.ToLower()[matchedLen] != matchedChar))
                                        {
                                            break;
                                        }
                                    }

                                    var partialCmd = possibleCommands.First().Substring(0, matchedLen);

                                    if (partialCmd == Encoding.UTF8.GetString(buffer, 0, bufferLength))
                                    {
                                        WriteLine("\r\nPossible Commands:\r\n");
                                        foreach (var cmd in possibleCommands)
                                        {
                                            WriteLine($"  - {cmd}");
                                        }

                                        WritePrompt();
                                        stream.Write(buffer, 0, bufferLength);
                                        break;
                                    }

                                    var newBuffer = ReplaceBuffer(stream, bufferLength, partialCmd);
                                    Array.Copy(newBuffer, buffer, newBuffer.Length);
                                    bufferLength = newBuffer.Length;
                                    break;
                                }

                                foreach (var name in Logging.Logger.CommandNames)
                                {
                                    var nameLower = name.ToLower();
                                    if (nameLower == currentTypedCommand) continue;

                                    if (!nameLower.StartsWith(currentTypedCommand)) continue;

                                    var newBuffer = ReplaceBuffer(stream, bufferLength, name);
                                    Array.Copy(newBuffer, buffer, newBuffer.Length);
                                    bufferLength = newBuffer.Length;
                                    break;
                                }
                            }
                            else if (readChar == 10)
                            {
                                // skip
                            }
                            else if (readChar == 0x7f || readChar == 0x08)
                            {
                                if (bufferLength <= 0) continue;
                                stream.Write(new byte[] {0x08, 0x20, 0x08}, 0, 3);
                                bufferLength--;
                            }
                            else
                            {
                                buffer[bufferLength] = bytes[readIndex];
                                bufferLength++;
                                stream.Write(new[] {readChar}, 0, 1);
                            }
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    Client.Dispose();
                }
                catch (Exception e)
                {
                    if (!(e is IOException))
                    {
                        Logging.Logger.Error("Error in ClientThreadProcess Loop, {0}", e.Message);
                        Client.Dispose();
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Client.Dispose();
            }
            catch (Exception e)
            {
                Logging.Logger.Error("Error in ClientThreadProcess, {0}", e.GetType().Name + ", " + e.Message);
                Client.Dispose();
            }

            _callBackOnClose(ConnectionId);
        }

        private byte[] ReplaceBuffer(Stream stream, int currentLength, string newContent)
        {
            var contentBytes = Encoding.UTF8.GetBytes(newContent);
            var newBytes = new byte[contentBytes.Length + currentLength];
            int j;
            for (j = 0; j < currentLength; j++)
            {
                newBytes[j] = 0x08;
            }
            for (var k = 0; k < contentBytes.Length; k++)
            {
                newBytes[k + j] = contentBytes[k];
            }
            stream.Write(newBytes, 0, newBytes.Length);
            return contentBytes;
        }

        private void OnReceiveLine(string message)
        {
            if (message.Length > 0)
            {
                var match = Regex.Match(message, @"(\w+)(?: (.+))?");

                //var remoteEndpoint = Client.Client.RemoteEndPoint as IPEndPoint;

                if (match.Success)
                {
                    try
                    {
                        var command = match.Groups[1].Value;
                        var argString = match.Groups[2].Value;
                        var args = new List<KeyValuePair<string, string>>();
                        var argMatch = Regex.Matches(argString, @"(?:-(\w+) +)?((?:\w+)|""(?:[^""\\]|\\.)*"")");
                        foreach (Match m in argMatch)
                        {
                            var key = m.Groups[1].Value;
                            var val = m.Groups[2].Value;
                            if (val.StartsWith("\"") && val.EndsWith("\""))
                            {
                                val = val.Substring(1, val.Length - 2);
                            }

                            val = val.Replace("\\\"", "\"");

                            args.Add(new KeyValuePair<string, string>(key, val));
                        }

                        _receivedCommandCallBack(new ReceivedCommandEventArgs
                        {
                            Connection = this,
                            Command = command,
                            ArgString = argString,
                            Args = args.AsReadOnly(),
                            Respond = Write
                        });
                    }
                    catch (Exception e)
                    {
                        Logging.Logger.Error(e);
                        WriteLine("Error processing command: {0}", e.Message);
                    }
                }
                else
                {
                    WriteLine("Unknown or bad command");
                }
            }
            WritePrompt();
        }

        private void WritePrompt()
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(ConsoleServer.GetPrompt());
                Client.GetStream().Write(bytes, 0, bytes.Length);
                _promptWritten = true;
            }
            catch (ObjectDisposedException)
            {
                // ReSharper disable once RedundantJumpStatement
                return;
            }
        }

        public void Write(string message)
        {
            try
            {
                var result = Regex.Replace(message, "\r\n?|\n", "\r\n");

                var bytes = Encoding.UTF8.GetBytes(result);

                if (_promptWritten)
                {
                    var newLine = Encoding.UTF8.GetBytes("\r\n");
                    Client.GetStream().Write(newLine, 0, newLine.Length);
                    _promptWritten = false;
                }

                Client.GetStream().Write(bytes, 0, bytes.Length);
            }
            catch (ObjectDisposedException)
            {
                // ReSharper disable once RedundantJumpStatement
                return;
            }
            catch (InvalidOperationException)
            {
                // ReSharper disable once RedundantJumpStatement
                return;
            }
        }

        public void WriteLine(string message)
        {
            Write(message + Environment.NewLine);
        }

        public void Write(string message, params object[] args)
        {
            Write(string.Format(message, args));
        }

        public void WriteLine(string message, params object[] args)
        {
            WriteLine(string.Format(message, args));
        }

        public void Dispose()
        {
            Client?.Dispose();
        }
    }

    public class ReceivedCommandEventArgs
    {
        public ConsoleConnection Connection { get; internal set; }
        public string Command { get; internal set; }
        public string ArgString { get; internal set; }
        public ReadOnlyCollection<KeyValuePair<string, string>> Args { get; internal set; }
        public CommandResponseAction Respond { get; internal set; }
    }

    public delegate void CommandResponseAction(string response);

    internal delegate void ReceivedCommandEventHandler(ConsoleServer consoleServer, ReceivedCommandEventArgs args);
}