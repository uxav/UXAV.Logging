using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace UXAV.Logging.Console
{
    public class ConsoleCommand
    {
        private readonly CommandReceivedCallback _callback;

        internal ConsoleCommand(string commandName, string description, IEnumerable<string> argsNames, CommandReceivedCallback callback)
        {
            CommandName = commandName;
            Description = description;
            ArgNames = argsNames.Select(name => name.ToLower()).ToArray();
            _callback = callback;
        }

        public string CommandName { get; }

        public string Description { get; }

        public string[] ArgNames { get; }

        internal void Process(ReceivedCommandEventArgs args)
        {
            var argsGiven = ArgNames.ToList();
            var processedArgs = new Dictionary<string, string>();

            if (argsGiven.Count == 0)
            {
                _callback(args.ArgString, new ReadOnlyDictionary<string, string>(processedArgs), args.Connection, args.Respond);
                return;
            }

            foreach (var valuePair in args.Args.Where(kvp => !string.IsNullOrEmpty(kvp.Key)))
            {
                if (argsGiven.Contains(valuePair.Key))
                {
                    argsGiven.Remove(valuePair.Key);
                    processedArgs.Add(valuePair.Key, valuePair.Value);
                }
                else
                {
                    args.Respond("Command has invalid arguments");
                    return;
                }
            }

            foreach (var valuePair in args.Args.Where(kvp => string.IsNullOrEmpty(kvp.Key)))
            {
                if (argsGiven.Count > 0)
                {
                    var key = argsGiven[0];
                    argsGiven.RemoveAt(0);
                    processedArgs.Add(key, valuePair.Value);
                }
                else
                {
                    args.Respond("Command has invalid arguments");
                    return;
                }
            }

            _callback(args.ArgString, new ReadOnlyDictionary<string, string>(processedArgs), args.Connection, args.Respond);
        }
    }

    public delegate void CommandReceivedCallback(string argString, ReadOnlyDictionary<string, string> args,
        ConsoleConnection connection, CommandResponseAction respond);
}