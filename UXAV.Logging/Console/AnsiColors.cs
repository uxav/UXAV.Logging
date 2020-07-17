using System.Diagnostics.CodeAnalysis;

namespace UXAV.Logging.Console
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class AnsiColors
    {
        public const string Black = "\u001b[30m";
        public const string Red = "\u001b[31m";
        public const string Green = "\u001b[32m";
        public const string Yellow = "\u001b[33m";
        public const string Blue = "\u001b[34m";
        public const string Magenta = "\u001b[35m";
        public const string Cyan = "\u001b[36m";
        public const string White = "\u001b[37m";

        public const string BrightBlack = "\u001b[30;1m";
        public const string BrightRed = "\u001b[31;1m";
        public const string BrightGreen = "\u001b[32;1m";
        public const string BrightYellow = "\u001b[33;1m";
        public const string BrightBlue = "\u001b[34;1m";
        public const string BrightMagenta = "\u001b[35;1m";
        public const string BrightCyan = "\u001b[36;1m";
        public const string BrightWhite = "\u001b[37;1m";

        public const string BackgroundBlack = "\u001b[40m";
        public const string BackgroundRed = "\u001b[41m";
        public const string BackgroundGreen = "\u001b[42m";
        public const string BackgroundYellow = "\u001b[43m";
        public const string BackgroundBlue = "\u001b[44m";
        public const string BackgroundMagenta = "\u001b[45m";
        public const string BackgroundCyan = "\u001b[46m";
        public const string BackgroundWhite = "\u001b[47m";

        public static string BackgroundBrightBlack = "\u001b[40;1m";
        public static string BackgroundBrightRed = "\u001b[41;1m";
        public static string BackgroundBrightGreen = "\u001b[42;1m";
        public static string BackgroundBrightYellow = "\u001b[43;1m";
        public static string BackgroundBrightBlue = "\u001b[44;1m";
        public static string BackgroundBrightMagenta = "\u001b[45;1m";
        public static string BackgroundBrightCyan = "\u001b[46;1m";
        public static string BackgroundBrightWhite = "\u001b[47;1m";

        public static string Bold = "\u001b[1m";
        public static string Reversed = "\u001b[7m";
        public static string Underline = "\u001b[4m";

        public static string Reset = "\u001b[0m";
    }
}