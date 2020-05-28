using System;
using System.Threading;

namespace PacmanServerConsole
{
    internal class Program
    {
        static ServerSocketV2 server;
        static void Main(string[] args)
        {
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
                Console.WriteLine($"Error: {error}");
            }

            // нажмите Enter, чтобы выйти
            Console.WriteLine("Press Enter to close server");
            ConsoleKey key = ConsoleKey.NoName;
            while (key != ConsoleKey.Enter)
            {
                key = Console.ReadKey().Key;
            }

            // подметаем мусор за собой сами
            if (server != null) server.Dispose();
        }
    }
}
