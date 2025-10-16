namespace server
{
    /// <summary>
    /// Used to differentiate between log events
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Marks messages used for debugging
        /// </summary>
        Debug,
        /// <summary>
        /// Marks minor notices not important enough for a warning
        /// </summary>
        Info,
        /// <summary>
        /// Marks more important warnings
        /// </summary>
        Warning,
        /// <summary>
        /// Marks errors
        /// </summary>
        Error,
        /// <summary>
        /// Marks an error that leads to the application being terminated 
        /// </summary>
        Fatal,
        /// <summary>
        /// Marks a successfully completed action 
        /// </summary>
        Ok
    }

    internal class Util
    {
        /// <summary>
        /// Shows messages in the console with a Enum.LogLevel used to show the reason together with a message and the time of the log message
        /// </summary>
        /// <param name="message">The message to be displayed in the log message</param>
        /// <param name="level">The Enums.LogLevel to indicate the reason behind the log message</param>
        public static void Log(string message, LogLevel level)
        {
            string prefix = level switch
            {
                LogLevel.Debug => "[DEBUG]    ",
                LogLevel.Info => "[INFO]     ",
                LogLevel.Warning => "[WARNING]  ",
                LogLevel.Error => "[ERROR]    ",
                LogLevel.Fatal => "[FATAL]    ",
                LogLevel.Ok => "[OK]       ",
                _ => ""
            };

            Console.ForegroundColor = level switch
            {
                LogLevel.Debug => ConsoleColor.Blue,
                LogLevel.Info => ConsoleColor.Gray,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Fatal => ConsoleColor.DarkRed,
                LogLevel.Ok => ConsoleColor.White,
                _ => ConsoleColor.Gray
            };

            Console.WriteLine($"{prefix}{DateTime.Now:HH:mm:ss} {message}");
            Console.ResetColor();
        }
    }
}
