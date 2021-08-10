using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UXAV.Logging.Console;

namespace UXAV.Logging
{
    public class LoggerMessage
    {
        private string _toString;
        private readonly Type _tracedType;
        private readonly StackTrace _stackTrace;

        internal LoggerMessage(Logger.LoggerLevel level, StackTrace stackTrace, Logger.MessageType messageType,
            string message)
        {
            Level = level;
            Time = DateTime.Now;
            MessageType = messageType;
            Message = message;
            _tracedType = stackTrace.GetFrame(0).GetMethod().DeclaringType;
        }

        internal LoggerMessage(Exception e)
        {
            Level = Logger.LoggerLevel.Error;
            Time = DateTime.Now;
            MessageType = Logger.MessageType.Exception;
            Message = $"{e.GetType().Name}: {e.Message}";
            _stackTrace = new StackTrace(e, true);
            _tracedType = _stackTrace.GetFrames().Last().GetMethod().DeclaringType;
        }

        public DateTime Time { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public Logger.LoggerLevel Level { get; }

        public int LevelValue => (int) Level;

        [JsonConverter(typeof(StringEnumConverter))]
        public Logger.MessageType MessageType { get; }

        public string ColorClass
        {
            get
            {
                switch (MessageType)
                {
                    case Logger.MessageType.Normal:
                        return "basic";
                    case Logger.MessageType.Debug:
                        return "info";
                    case Logger.MessageType.Highlight:
                        return "control";
                    case Logger.MessageType.Success:
                        return "success";
                    case Logger.MessageType.Warning:
                        return "warning";
                    case Logger.MessageType.Error:
                    case Logger.MessageType.Exception:
                        return "danger";
                    default:
                        return string.Empty;
                }
            }
        }

        public string TracedName => _tracedType == null ? string.Empty : _tracedType.Name;

        public string TracedNameFull => _tracedType == null ? string.Empty : _tracedType.FullName;

        public string Message { get; }

        public string StackTrace => _stackTrace?.ToString();

        public string GetFormattedForConsole()
        {
            using (var writer = new StringWriter())
            {
                switch (MessageType)
                {
                    case Logger.MessageType.Debug:
                        writer.Write(AnsiColors.BackgroundMagenta + " ");
                        break;
                    case Logger.MessageType.Success:
                        writer.Write(AnsiColors.BackgroundGreen + " ");
                        break;
                    case Logger.MessageType.Warning:
                        writer.Write(AnsiColors.BackgroundYellow + " ");
                        break;
                    case Logger.MessageType.Exception:
                    case Logger.MessageType.Error:
                        writer.Write(AnsiColors.BackgroundRed + " ");
                        break;
                    default:
                        writer.Write(AnsiColors.BackgroundWhite + " ");
                        break;
                }

                writer.Write(AnsiColors.Reset + " " + AnsiColors.White);
                writer.Write(Time.ToString("dd-MMM HH:mm:ss.ffff"));
                writer.Write(" " + AnsiColors.Blue + " " + GetPaddedLineName(TracedName) + " " + AnsiColors.Reset);

                switch (MessageType)
                {
                    case Logger.MessageType.Highlight:
                        writer.Write(AnsiColors.Bold);
                        break;
                    case Logger.MessageType.Success:
                        writer.Write(AnsiColors.BrightGreen);
                        break;
                    case Logger.MessageType.Warning:
                        writer.Write(AnsiColors.BrightYellow);
                        break;
                    case Logger.MessageType.Exception:
                        writer.Write(AnsiColors.Red);
                        break;
                    case Logger.MessageType.Error:
                        writer.Write(AnsiColors.BrightRed);
                        break;
                    default:
                        writer.Write(AnsiColors.White);
                        break;
                }

                writer.Write(" " + Message + AnsiColors.Reset);
                return writer.ToString();
            }
        }

        public string GetFormattedForText()
        {
            if (_toString != null) return _toString;
            using (var writer = new StringWriter())
            {
                writer.Write(Time.ToString("O"));
                writer.Write(" " + GetPaddedLineName(TracedName) + " ");

                switch (MessageType)
                {
                    case Logger.MessageType.Debug:
                        writer.Write("  Debug: ");
                        break;
                    case Logger.MessageType.Highlight:
                        writer.Write(" Notice: ");
                        break;
                    case Logger.MessageType.Success:
                        writer.Write("     OK: ");
                        break;
                    case Logger.MessageType.Warning:
                        writer.Write("Warning: ");
                        break;
                    case Logger.MessageType.Error:
                        writer.Write("  Error: ");
                        break;
                    case Logger.MessageType.Normal:
                        writer.Write("   Info: ");
                        break;
                    case Logger.MessageType.Exception:
                        writer.Write("  Error: ");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                writer.Write(Message);
                _toString = writer.ToString();
            }

            return _toString;
        }

        private static string GetPaddedLineName(string contents)
        {
            const int size = 20;
            var result = contents ?? string.Empty;
            if (result.Length > size)
            {
                result = result.Substring(0, size - 2);
                result += "..";
            }
            else
            {
                for (var i = result.Length; i < size; i++)
                {
                    result += " ";
                }
            }

            return result;
        }

        public override string ToString()
        {
            if (MessageType == Logger.MessageType.Exception)
            {
                var trace = _stackTrace.ToString().Replace("\r\n", "\r").Replace('\n', 'r');
                return $"{(string.IsNullOrEmpty(TracedName) ? string.Empty : TracedName + " | ")}{Message}\r{trace}";
            }
            return $"{(string.IsNullOrEmpty(TracedName) ? string.Empty : TracedName + " | ")}{Message}";
        }
    }
}