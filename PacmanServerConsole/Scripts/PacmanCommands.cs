using System;
using System.Collections.Generic;
using System.Text;

namespace PacmanServerConsole
{
    static internal class PacmanCommands
    {
        private readonly static StringBuilder strbuilder = new StringBuilder();

        public static void MessageReceiver(ClientConnection client, string answer)
        {
            Console.WriteLine($"{client.endPoint}: {answer}");
            answer = answer.Replace(ClientConnection.terminatorStr, ""); // убираем терминатор
            int size = answer.Length;

            if (size < 3) return; // проверяем, что у нас есть хедер запроса
            string header = answer.Substring(0, 3); // первые три знака - заголовок
            string content = (size > 4) ? answer.Substring(4) : ""; // начинаем с 5-го знака, это 4-ая позиция

            switch (header)
            {
                case "MAP": // нам пришёл запрос карты
                    SendCurrentMap(client);
                    break;

                case "POS": // клиент посылает своё положение
                    SetPacmanPosition(client, content);
                    break;

                case "MOV": // клиент посылает запрос на изменение положения
                    ResolveMovement(client, content);
                    break;

                default: // неопознанный лунный кролик
                    Console.WriteLine($"Unknown request syntax");
                    return;
            }
        }

        public static void SendCurrentMap(ClientConnection client)
        {
            PacmanGame game = client.game;
            List<string> map = game.map.GetText();
            int w = game.map.width;
            int h = game.map.height;
            map.Insert(0, $"{game.pacmanX}x{game.pacmanY}");
            map.Insert(0, $"{w}x{h}");
            strbuilder.Clear();
            for (int i = 0; i < map.Count; i++)
            {
                strbuilder.Append(map[i]);
                char append = (i == map.Count - 1) ? ClientConnection.terminator : ClientConnection.delimiter;
                strbuilder.Append(append);
            }
            client.Send(strbuilder.ToString());
            strbuilder.Clear();
        }

        public static void SetPacmanPosition(ClientConnection client, string position)
        {
            string[] pos = position.Split('x');
            int x = Convert.ToInt32(pos[0]);
            int y = Convert.ToInt32(pos[1]);
            client.game.SetPosition(x, y);
        }

        public static void ResolveMovement(ClientConnection client, string movement)
        {
            string move = movement.Substring(0, 4);
            var result = client.game.ResolveMovement(move);
            if (result.move == Direction.None) return;
            client.Send($"POS:{result.x}x{result.y}");
        }
    }
}
