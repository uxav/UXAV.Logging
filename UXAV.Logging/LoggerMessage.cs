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
        private StackTrace _stackTrace;

        internal LoggerMessage(Logging.Logger.LoggerLevel level, StackTrace stackTrace, Logging.Logger.MessageType messageType,
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
            Level = Logging.Logger.LoggerLevel.Error;
            Time = DateTime.Now;
            MessageType = Logging.Logger.MessageType.Exception;
            Message = $"{e.GetType().Name}: {e.Message}";
            _stackTrace = new StackTrace(e, true);
            _tracedType = _stackTrace.GetFrames().Last().GetMethod().DeclaringType;
        }

        public DateTime Time { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public Logging.Logger.LoggerLevel Level { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public Logging.Logger.MessageType MessageType { get; }

        public string ColorClass
        {
            get
            {
                switch (MessageType)
                {
                    case Logging.Logger.MessageType.Normal:
                        return "basic";
                    case Logging.Logger.MessageType.Debug:
                        return "info";
                    case Logging.Logger.MessageType.Highlight:
                        return "control";
                    case Logging.Logger.MessageType.Success:
                        return "success";
                    case Logging.Logger.MessageType.Warning:
                        return "warning";
                    case Logging.Logger.MessageType.Error:
                    case Logging.Logger.MessageType.Exception:
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
                    case Logging.Logger.MessageType.Debug:
                        writer.Write(AnsiColors.BackgroundMagenta + " ");
                        break;
                    case Logging.Logger.MessageType.Success:
                        writer.Write(AnsiColors.BackgroundGreen + " ");
                        break;
                    case Logging.Logger.MessageType.Warning:
                        writer.Write(AnsiColors.BackgroundYellow + " ");
                        break;
                    case Logging.Logger.MessageType.Exception:
                    case Logging.Logger.MessageType.Error:
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
                    case Logging.Logger.MessageType.Highlight:
                        writer.Write(AnsiColors.Bold);
                        break;
                    case Logging.Logger.MessageType.Success:
                        writer.Write(AnsiColors.BrightGreen);
                        break;
                    case Logging.Logger.MessageType.Warning:
                        writer.Write(AnsiColors.BrightYellow);
                        break;
                    case Logging.Logger.MessageType.Exception:
                        writer.Write(AnsiColors.Red);
                        break;
                    case Logging.Logger.MessageType.Error:
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
                    case Logging.Logger.MessageType.Debug:
                        writer.Write("  Debug: ");
                        break;
                    case Logging.Logger.MessageType.Highlight:
                        writer.Write(" Notice: ");
                        break;
                    case Logging.Logger.MessageType.Success:
                        writer.Write("     OK: ");
                        break;
                    case Logging.Logger.MessageType.Warning:
                        writer.Write("Warning: ");
                        break;
                    case Logging.Logger.MessageType.Error:
                        writer.Write("  Error: ");
                        break;
                    case Logging.Logger.MessageType.Normal:
                        writer.Write("   Info: ");
                        break;
                    case Logging.Logger.MessageType.Exception:
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
                result = result + "..";
            }
            else
            {
                for (var i = result.Length; i < size; i++)
                {
                    result = result + " ";
                }
            }

            return result;
        }

        public override string ToString()
        {
            return $"{Level} | {(string.IsNullOrEmpty(TracedName) ? string.Empty : TracedName + " | ")}{Message}";
        }
    }
}