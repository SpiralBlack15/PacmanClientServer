using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacmanServerConsole
{
    public class PacmanMap
    {
        public const string defaultPath = "pacman_field.txt";

        public bool[,] bmap { get; private set; } = new bool[23, 22];
        public int height { get; private set; }
        public int width  { get; private set; }

        public event Action onMapLoading;
        public event Action onMapLoaded;
        public event Action onMapLoadingFailed;

        public PacmanMap()
        {
            onMapLoaded += PacmanMap_onMapLoaded;
        }

        private bool ReadMapFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) path = defaultPath;

            if (!File.Exists(path))
            {
                Logger.ColorLog($"ERROR map file could not be found in path: {path}", ConsoleColor.Red);
                return false;
            }

            try
            {
                var lines = File.ReadAllLines(path);

                bool sizeSetted = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.Contains("//")) continue;
                    if (!line.Contains(";")) continue;

                    var readcell = line.Split(';');
                    int x = Convert.ToInt32(readcell[0]);
                    int y = Convert.ToInt32(readcell[1]);
                    if (sizeSetted) // читаем только блоки
                    {
                        bmap[x, y] = true;
                    }
                    else
                    {
                        bmap = new bool[x, y]; // по умолчанию все false, так что не паримся
                        height = y; width = x;
                        sizeSetted = true;
                    }
                }
                return true;
            }
            catch (Exception error)
            {
                Logger.ColorLog($"ERROR reading map file: {error}", ConsoleColor.Red);
                return false;
            }
        }

        public void ReadMap(string path)
        {
            onMapLoading?.Invoke();
            bool result = ReadMapFile(path);
            if (result) onMapLoaded?.Invoke(); else onMapLoadingFailed?.Invoke();
        }

        public async void ReadMapAsync(string path)
        {
            onMapLoading?.Invoke();
            bool result = await Task.Run(() => { return ReadMapFile(path); });
            if (result) onMapLoaded?.Invoke(); else onMapLoadingFailed?.Invoke();
        }

        public List<string> GetText()
        {
            List<string> answer = new List<string>();
            StringBuilder stringBuilder = new StringBuilder();
            for (int x = 0; x < width; x++) // передаем карту в обычном формате
            {
                for (int y = 0; y < height; y++)
                {
                    stringBuilder.Append(bmap[x, y] ? "1" : "0");
                }
                answer.Add(stringBuilder.ToString());
                stringBuilder.Clear();
            }
            return answer;
        }

        /// <summary>
        /// Просто выводит в консоль карту, повёрнутую так, чтобы она отображалась как в юнити
        /// </summary>
        private void PacmanMap_onMapLoaded()
        {
            Logger.Space();
            Logger.ColorLog("MAP loaded", ConsoleColor.DarkCyan);
            StringBuilder stringBuilder = new StringBuilder();
            for (int y = bmap.GetLength(1) - 1; y > -1; y--)
            {
                for (int x = 0; x < bmap.GetLength(0); x++)
                {
                    string str = bmap[x, y] ? "# " : "  ";
                    stringBuilder.Append(str);
                }
                stringBuilder.Append("\n");
            }
            Logger.ColorLog(stringBuilder.ToString(), ConsoleColor.DarkCyan);
            Logger.Space();
        }
    }
}
