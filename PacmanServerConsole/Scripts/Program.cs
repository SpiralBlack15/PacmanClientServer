using System;
using System.Threading;

namespace PacmanServerConsole
{
    internal class Program
    {
        static ServerSocketV2 server;
        static void Main(string[] args)
        {
            Logger.ColorLog($"--PACMAN SERVER APPLICATION--", ConsoleColor.Green);
            Logger.Space();

            try
            {
                // запускаем сервер
                server = new ServerSocketV2();
                server.RunListener();

                // подписываемся на сообщения от клиента
                server.onMessageReceived += PacmanCommands.MessageReceiver;
            }
            catch (Exception error)
            {
                server.Disconnect();
                Logger.ColorLog($"ERROR in main {error}", ConsoleColor.Red);
            }

            // нажмите Enter, чтобы выйти
            Logger.ColorLog($"(Press Enter to close server)", ConsoleColor.DarkGray);
            ConsoleKey key = ConsoleKey.NoName;
            while (key != ConsoleKey.Enter) { key = Console.ReadKey().Key; }

            // подметаем мусор за собой сами
            if (server != null) server.Dispose();
        }
    }
}
