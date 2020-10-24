using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Crestron.SimplSharp;
using UXAV.Logging.Console;
using ObjectModel = System.Collections.ObjectModel;

namespace UXAV.Logging
{
    public static class Logger
    {
        private static readonly ConsoleServer ConsoleServer;

        private static readonly Dictionary<string, ConsoleCommand> CommandsDict =
            new Dictionary<string, ConsoleCommand>();

        private static readonly MessageStack MessageHistory = new MessageStack(1000);
        private static LoggerLevel _level = LoggerLevel.Info;

        static Logger()
        {
            ConsoleServer = new ConsoleServer();
            ConsoleServer.ReceivedCommand += ConsoleServerOnReceivedCommand;
            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStatusEventHandler;
            AddCommand(PrintCommandHelp, "Help", "Get list of commands", "command");
            AddCommand(TailLog, "Tail", "Tail the logger entries", "count");
            AddCommand(ConsoleLog, "Log", "Write a log entry");
            AddCommand(GetIpTable, "IPTable", "Print the IP Table for the current running program");
            AddCommand(SetStreamLevel, "LogStreamLevel", "Set the level logs stream on this connection", "level");
            AddCommand(ListAssemblies, "ListAssemblies", "List the available assemblies in the app");
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance)
            {
                AddCommand((argString, args, connection, respond) =>
                {
                    var response = string.Empty;
                    CrestronConsole.SendControlSystemCommand($"progreset -P:{InitialParametersClass.ApplicationNumber}",
                        ref response);
                }, "RestartApp", "Restart the running application");
                AddCommand(GetAutoDiscovery, "AutoDiscover", "Get the system autodiscovery results");
                AddCommand(RelayConsoleCommand, "Console", "Send a command to the Crestron console");
            }

            var ass = Assembly.GetExecutingAssembly().GetName();
            Highlight($"{ass.Name}.{nameof(Logger)} started, version {ass.Version}");
        }

        public enum MessageType
        {
            Normal,
            Debug,
            Highlight,
            Success,
            Warning,
            Error,
            Exception
        }

        public enum LoggerLevel
        {
            None,
            Error,
            Warning,
            Notice,
            Info,
            Debug
        }

        public static LoggerLevel Level
        {
            get => _level;
            set
            {
                if (value == LoggerLevel.None)
                {
                    throw new Exception("Cannot set Logger Level to None");
                }

                _level = value;
            }
        }

        public static LoggerLevel DefaultLogStreamLevel { get; set; } = LoggerLevel.Info;

        private static void TailLog(string argString, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            var count = 50;
            if (args.ContainsKey("count"))
            {
                if (args["count"].ToLower() == "all")
                {
                    lock (MessageHistory)
                    {
                        foreach (var message in MessageHistory)
                        {
                            respond(message.GetFormattedForConsole() + "\r\n");
                        }
                    }

                    return;
                }

                try
                {
                    count = int.Parse(args["count"]);
                }
                catch (Exception e)
                {
                    respond($"Error with arg \"count\", {e.Message}\r\n\r\n");
                    return;
                }
            }

            lock (MessageHistory)
            {
                foreach (var message in MessageHistory.GetLast(count))
                {
                    respond(message.GetFormattedForConsole() + "\r\n");
                }
            }
        }

        private static void PrintCommandHelp(string argString, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            lock (CommandsDict)
            {
                if (args.ContainsKey("command") && CommandsDict.ContainsKey(args["command"]))
                {
                    var c = CommandsDict[args["command"]];
                    respond($"  {AnsiColors.BrightCyan}{c.CommandName}{AnsiColors.Reset}    {c.Description}\r\n");
                    if (c.ArgNames.Length > 0)
                    {
                        respond($"\r\n  {c.ArgNames.Length} argument" + (c.ArgNames.Length > 1 ? "s" : string.Empty) +
                                "\r\n");
                    }

                    foreach (var argName in c.ArgNames)
                    {
                        respond($"      -{AnsiColors.Cyan}{argName}{AnsiColors.Reset}\r\n");
                    }

                    return;
                }

                if (args.ContainsKey("command"))
                {
                    respond($"Unknown command \"{AnsiColors.BrightRed}{args["command"]}{AnsiColors.Reset}\"");
                    return;
                }

                var nameColLen = "Command".Length;
                var descriptionColLen = "Description".Length;
                foreach (var c in CommandsDict.Values)
                {
                    if (c.CommandName.Length > nameColLen)
                    {
                        nameColLen = c.CommandName.Length;
                    }

                    if (c.Description.Length > descriptionColLen)
                    {
                        descriptionColLen = c.Description.Length;
                    }
                }

                var header = Figgle.FiggleFonts.Ogre.Render("Command Help").Replace(Environment.NewLine, "\r\n");
                respond("\r\n" + AnsiColors.Blue + header + AnsiColors.Reset + "\r\n");

                respond(
                    "  " + AnsiColors.White + "Command".PadRight(nameColLen + 2) + "  " + "Description" +
                    AnsiColors.Reset +
                    "\r\n");
                var line = "  ";
                for (var i = 0; i < (nameColLen + 2 + 2 + descriptionColLen + 2); i++)
                {
                    line = line + "-";
                }

                respond(AnsiColors.Blue + line + AnsiColors.Reset + "\r\n");
                foreach (var c in CommandsDict.Values.OrderBy(c => c.CommandName))
                {
                    respond(
                        $"  {AnsiColors.BrightCyan}{c.CommandName.PadRight(nameColLen + 2)}{AnsiColors.Reset}  {c.Description}\r\n");
                }
            }
        }

        private static void ConsoleLog(string argString, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            Log(argString);
        }

        private static void GetIpTable(string argString, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance)
            {
                respond($"IP Table for app {InitialParametersClass.RoomId}:\r\n");

                var consoleResponse = string.Empty;
                if (!CrestronConsole.SendControlSystemCommand($"ipt -p:{InitialParametersClass.ApplicationNumber} -t",
                    ref consoleResponse))
                {
                    respond("Error getting IP Table from device console");
                    return;
                }

                var matches = Regex.Matches(consoleResponse,
                    @"\ *(\w+)\ *\|(\w+)\ *\|(\w+)\ *\|(\w+)?\ *\|(\d+)\ *\|([\d\.]+)\ *\|\ *([\w\-\ ]+)\ *?\|\ *([\w ]+)");

                var table = new ConsoleTable("IP ID", "Model", "Device Type", "Device ID", "Port", "IP Address",
                    "Status", "Description");

                foreach (Match match in matches)
                {
                    var ipId = int.Parse(match.Groups[1].Value.Trim(), NumberStyles.HexNumber);
                    var deviceType = match.Groups[2].Value;
                    var status = match.Groups[3].Value == "ONLINE";
                    var deviceId = match.Groups[4].Value;
                    var port = match.Groups[5].Value;
                    var ipAddress = match.Groups[6].Value;
                    var model = match.Groups[7].Value;
                    var description = match.Groups[8].Value;
                    table.AddRow(ipId.ToString("X2"), model, deviceType, deviceId, port, ipAddress,
                        status
                            ? AnsiColors.Green + "ONLINE " + AnsiColors.Reset
                            : AnsiColors.White + "OFFLINE" + AnsiColors.Reset,
                        description);
                }

                respond(table.ToString(true));
                return;
            }
        }

        private static void SetStreamLevel(string argString, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            if (!args.ContainsKey("level"))
            {
                respond($"Your log streaming level is set to: {connection.LogStreamLevel}");
                return;
            }

            LoggerLevel level;
            try
            {
                level = (LoggerLevel) Enum.Parse(typeof(LoggerLevel), args["level"], true);
            }
            catch
            {
                respond("Incorrect level specified");
                return;
            }

            connection.LogStreamLevel = level;
            respond($"Your log streaming level is now set to: {level}");
        }

        private static void ListAssemblies(string argString, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            var table = new ConsoleTable("Assembly Name", "Version", "Company", "Description");
            foreach (var fileInfo in GetAssemblyInfo())
            {
                if (fileInfo.ReadError)
                {
                    table.AddRow(fileInfo.Name, AnsiColors.Red + fileInfo.Version + AnsiColors.Reset, "", "");
                    continue;
                }
                table.AddRow(fileInfo.Name, fileInfo.Version, fileInfo.Company, fileInfo.Description);
            }

            respond("\r\n" + table.ToString(true));
        }

        private static IEnumerable<AssemblyFileInfo> GetAssemblyInfo()
        {
            var results = new List<AssemblyFileInfo>();
            var files = Directory.GetFiles(InitialParametersClass.ProgramDirectory.ToString(), "*.dll");
            foreach (var file in files)
            {
                var info = new AssemblyFileInfo();
                try
                {
                    var an = AssemblyName.GetAssemblyName(file);
                    var vi = FileVersionInfo.GetVersionInfo(file);
                    info.Name = an.Name;
                    info.Version = an.Version.ToString();
                    info.Company = vi.CompanyName;
                    info.Description = vi.FileDescription;
                }
                catch(Exception e)
                {
                    info.Name = file;
                    info.Version = e.Message;
                    info.ReadError = true;
                    info.Company = string.Empty;
                    info.Description = string.Empty;
                }

                results.Add(info);
            }

            return results.OrderBy(i => i.Name);
        }

        private struct AssemblyFileInfo
        {
            public string Version { get; set; }
            public string Name { get; set; }
            public string Company { get; set; }
            public bool ReadError { get; set; }
            public string Description { get; set; }
        }

        private static void GetAutoDiscovery(string argString, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Server)
            {
                respond("Not device platform!");
                return;
            }

            var response = string.Empty;
            CrestronConsole.SendControlSystemCommand("autodiscover query tableformat", ref response);
            var lines = response.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
            var table = new ConsoleTable("Model", "IP Address", "HostName", "IP ID", "Interface", "Version",
                "Mac Address");
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^([\d\.]+)\ *\|(\w+)\ *\|(\w+)\ *\|([\w-]+)\ *\|(.+)$");
                if (match.Success)
                {
                    var ipAddress = match.Groups[1].Value;
                    var ipId = match.Groups[2].Value;
                    var hostname = match.Groups[4].Value;
                    var network = match.Groups[3].Value;
                    var detailsString = match.Groups[5].Value;
                    var details = Regex.Match(detailsString,
                        @"([\w-]+).*\[(.+?)(?: \((.+)\))?, *#(\w{8})\]\ ?(?:@E-(\w{12}))*");
                    if (details.Success)
                    {
                        var model = details.Groups[1].Value;
                        var version = details.Groups[2].Value;
                        var macAddress = details.Groups[5].Value;
                        if (!string.IsNullOrEmpty(macAddress))
                        {
                            macAddress = string.Join(":", Enumerable.Range(0, 6)
                                .Select(i => macAddress.Substring(i * 2, 2)));
                        }

                        table.AddRow(model, ipAddress, hostname, ipId, network, version, macAddress);
                    }
                    else
                    {
                        table.AddRow("Unknown", ipAddress, hostname, ipId, network, detailsString, "");
                    }
                }
            }

            respond(table.ToString(true));
        }

        private static void RelayConsoleCommand(string argstring, ObjectModel.ReadOnlyDictionary<string, string> args,
            ConsoleConnection connection, CommandResponseAction respond)
        {
            var response = string.Empty;
            CrestronConsole.SendControlSystemCommand(argstring, ref response);
            respond(response);
        }

        public static IEnumerable<string> CommandNames
        {
            get
            {
                lock (CommandsDict)
                {
                    return CommandsDict.OrderBy(i => i.Key).Select(k => k.Value.CommandName).ToArray();
                }
            }
        }

        public static object ListenPort => ConsoleServer.Port;

        public static IEnumerable<string> History
        {
            get
            {
                lock (MessageHistory)
                {
                    return MessageHistory.Select(message => message.GetFormattedForConsole()).ToArray();
                }
            }
        }

        public static event MessageLoggedEventHandler MessageLogged;

        private static void ConsoleServerOnReceivedCommand(ConsoleServer consoleServer, ReceivedCommandEventArgs args)
        {
            var cmdLower = args.Command.ToLower();
            Action<ReceivedCommandEventArgs> process = null;

            lock (CommandsDict)
            {
                if (CommandsDict.ContainsKey(cmdLower))
                {
                    process = CommandsDict[cmdLower].Process;
                }
            }

            if (process == null)
            {
                if (cmdLower.Length < 3)
                {
                    args.Respond($"Unknown command \"{args.Command}\"\r\n");
                    return;
                }

                var possibleCommands =
                    CommandNames.Where(n => n.ToLower().StartsWith(cmdLower))
                        .ToArray();
                if (possibleCommands.Length > 1)
                {
                    args.Respond($"Ambiguous command \"{args.Command}\"\r\n");
                    return;
                }

                if (possibleCommands.Length == 0)
                {
                    args.Respond($"Unknown command \"{args.Command}\"\r\n");
                    return;
                }

                var cmd = possibleCommands.First().ToLower();
                process = CommandsDict[cmd].Process;
            }

            try
            {
                process(args);
            }
            catch (Exception e)
            {
                args.Respond("Error processing command:\r\n" + e);
            }
        }

        public static ConsoleCommand AddCommand(CommandReceivedCallback callback, string commandName,
            string description, params string[] argNames)
        {
            var cmdLower = commandName.ToLower();
            var cmd = new ConsoleCommand(commandName, description, argNames, callback);
            lock (CommandsDict)
            {
                if (CommandsDict.ContainsKey(cmdLower))
                {
                    throw new ArgumentException("Command already registered");
                }

                CommandsDict.Add(cmdLower, cmd);
            }

            return cmd;
        }

        public static void RemoveCommand(string commandName)
        {
            var cmdLower = commandName.ToLower();
            lock (CommandsDict)
            {
                if (!CommandsDict.ContainsKey(cmdLower))
                {
                    throw new ArgumentException("Command does not exist");
                }

                CommandsDict.Remove(cmdLower);
            }
        }

        public static void StartConsole(int portNumber)
        {
            ConsoleServer.Start(portNumber);
        }

        public static bool ConsoleListening => ConsoleServer.Listening;

        public static string[] ConsoleConnections
        {
            get
            {
                return ConsoleServer.Connections.Select(connection => connection.RemoteEndpoint.Address.ToString())
                    .ToArray();
            }
        }

        public static void StopConsole()
        {
            ConsoleServer.Stop();
        }

        public static LoggerMessage[] GetHistory()
        {
            lock (MessageHistory)
            {
                return MessageHistory.ToArray();
            }
        }

        public static LoggerMessage[] GetHistory(int count)
        {
            lock (MessageHistory)
            {
                return MessageHistory.GetLast(count).ToArray();
            }
        }

        public static void Log(string message)
        {
            Log(LoggerLevel.Info, 1, message);
        }

        public static void Log(string message, params object[] args)
        {
            Log(LoggerLevel.Info, 1, message, args);
        }

        public static void Log(int skipFrames, string message)
        {
            Log(LoggerLevel.Info, skipFrames + 1, message);
        }

        public static void Log(int skipFrames, string message, params object[] args)
        {
            Log(LoggerLevel.Info, skipFrames + 1, message, args);
        }

        public static void Log(LoggerLevel level, string message)
        {
            Log(level, 1, message);
        }

        public static void Log(LoggerLevel level, string message, params object[] args)
        {
            Log(level, 1, message, args);
        }

        public static void Log(LoggerLevel level, int skipFrames, string message)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(level, frame, MessageType.Normal, message));
        }

        public static void Log(LoggerLevel level, int skipFrames, string message, params object[] args)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(level, frame, MessageType.Normal, string.Format(message, args)));
        }

        public static void Debug(string message)
        {
            Debug(1, message);
        }

        public static void Debug(string message, params object[] args)
        {
            Debug(1, message, args);
        }

        public static void Debug(int skipFrames, string message)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            var lines = message.Split(Environment.NewLine.ToCharArray());
            var fMessage = lines.First();
            fMessage = lines.Skip(1)
                .Aggregate(fMessage, (current, line) => current + Environment.NewLine + "  " + line);
            WriteLog(new LoggerMessage(LoggerLevel.Debug, frame, MessageType.Debug, fMessage));
        }

        public static void Debug(int skipFrames, string message, params object[] args)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            var lines = message.Split(Environment.NewLine.ToCharArray());
            var fMessage = lines.First();
            fMessage = lines.Skip(1)
                .Aggregate(fMessage, (current, line) => current + Environment.NewLine + "  " + line);
            fMessage = string.Format(fMessage, args);
            WriteLog(new LoggerMessage(LoggerLevel.Debug, frame, MessageType.Debug, fMessage));
        }

        public static void Highlight(string message)
        {
            Highlight(2, message);
        }

        public static void Highlight(string message, params object[] args)
        {
            Highlight(2, message, args);
        }

        public static void Highlight(int skipFrames, string message)
        {
            Highlight(LoggerLevel.Notice, skipFrames, message);
        }

        public static void Highlight(int skipFrames, string message, params object[] args)
        {
            Highlight(LoggerLevel.Notice, skipFrames, message, args);
        }

        public static void Highlight(LoggerLevel level, string message)
        {
            Highlight(level, 1, message);
        }

        public static void Highlight(LoggerLevel level, string message, params object[] args)
        {
            Highlight(level, 1, message, args);
        }

        public static void Highlight(LoggerLevel level, int skipFrames, string message)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(level, frame, MessageType.Highlight, message));
        }

        public static void Highlight(LoggerLevel level, int skipFrames, string message, params object[] args)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(level, frame, MessageType.Highlight, string.Format(message, args)));
        }

        public static void Success(string message)
        {
            Success(1, message);
        }

        public static void Success(string message, params object[] args)
        {
            Success(1, message, args);
        }

        public static void Success(int skipFrames, string message)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(LoggerLevel.Notice, frame, MessageType.Success, message));
        }

        public static void Success(int skipFrames, string message, params object[] args)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(LoggerLevel.Notice, frame, MessageType.Success, string.Format(message, args)));
        }

        public static void Warn(string message)
        {
            Warn(LoggerLevel.Warning, 1, message);
        }

        public static void Warn(string message, params object[] args)
        {
            Warn(LoggerLevel.Warning, 1, message, args);
        }

        public static void Warn(int skipFrames, string message)
        {
            Warn(LoggerLevel.Warning, skipFrames + 1, message);
        }

        public static void Warn(int skipFrames, string message, params object[] args)
        {
            Warn(LoggerLevel.Warning, skipFrames + 1, message, args);
        }

        public static void Warn(LoggerLevel level, string message)
        {
            Warn(level, 1, message);
        }

        public static void Warn(LoggerLevel level, string message, params object[] args)
        {
            Warn(level, 1, message, args);
        }

        public static void Warn(LoggerLevel level, int skipFrames, string message)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(level, frame, MessageType.Warning, message));
        }

        public static void Warn(LoggerLevel level, int skipFrames, string message, params object[] args)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(level, frame, MessageType.Warning, string.Format(message, args)));
        }

        public static void Error(string message)
        {
            Error(1, message);
        }

        public static void Error(string message, params object[] args)
        {
            Error(1, message, args);
        }

        public static void Error(int skipFrames, string message)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(LoggerLevel.Error, frame, MessageType.Error, message));
        }

        public static void Error(int skipFrames, string message, params object[] args)
        {
            var frame = new StackTrace(skipFrames + 1, true);
            WriteLog(new LoggerMessage(LoggerLevel.Error, frame, MessageType.Error, string.Format(message, args)));
        }

        public static void Error(Exception e)
        {
            WriteLog(new LoggerMessage(e));
        }

        private static void WriteLog(LoggerMessage message)
        {
            if (message.Level <= Level)
            {
                lock (MessageHistory)
                {
                    MessageHistory.Add(message);
                }

                switch (message.Level)
                {
                    case LoggerLevel.Error:
                        ErrorLog.Error(message.ToString());
                        break;
                    case LoggerLevel.Warning:
                        ErrorLog.Warn(message.ToString());
                        break;
                    case LoggerLevel.Notice:
                        ErrorLog.Notice(message.ToString());
                        break;
                    case LoggerLevel.Info:
                        ErrorLog.Info(message.ToString());
                        break;
                    case LoggerLevel.Debug:
                        ErrorLog.Ok(message.ToString());
                        break;
                }
            }

            OnMessageLogged(message);

            foreach (var connection in ConsoleServer.Connections.Where(c => c.LogStreamLevel >= message.Level))
            {
                connection.WriteLine(message.GetFormattedForConsole());
            }
        }

        private static void OnProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;
            Warn("Program Stopping Now! Logger will close 👊");
            Highlight("Closing logger console application on port {0}", ConsoleServer.Port);
            Task.Run(StopLogger);
        }

        private static void StopLogger()
        {
            Thread.Sleep(500);
            Warn("Stopping Logger now!");
            Thread.Sleep(200);
            ConsoleServer.Stop();
        }

        private static void OnMessageLogged(LoggerMessage message)
        {
            MessageLogged?.Invoke(message);
        }
    }

    public delegate void MessageLoggedEventHandler(LoggerMessage message);
}