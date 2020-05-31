using System;

namespace PacmanServerConsole
{
    /// <summary>
    /// Этот класс нужен просто для того чтобы отслеживать в коде все свои вызовы лога в консоль
    /// и делать цвет
    /// </summary>
    public static class Logger
    {
        private static object loglock = new object();

        public static void ColorLog(string message, ConsoleColor consoleColor = ConsoleColor.Gray)
        {
            lock (loglock)
            {
                bool defaultColor = consoleColor == ConsoleColor.Gray;
                if (!defaultColor) Console.ForegroundColor = consoleColor;
                Console.WriteLine(message);
                if (!defaultColor) Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        public static void Space() { Console.WriteLine(); }
    }
}
