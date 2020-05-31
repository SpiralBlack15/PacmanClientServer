using Spiral.PacmanGame.Client;
using Spiral.PacmanGame.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Spiral.PacmanGame
{
    public class RemoteGameController : MonoBehaviour
    {
        public ClientMono client = null;
        public PlayerController playerController = null;
        public LevelBuilder builder = null;
        public Pacman pacman = null;
        public float cellStepTime = 0.1f; // время на один переход между клетками
        public float timeStep = 0.01f; // шаг корутины

        private int x = 0;
        private int y = 0;
        public bool inGame { get; private set; } = false;

        // MONO BEHAVIOUR =========================================================================
        // Моно
        //=========================================================================================
        private void Awake()
        {
            client.onConnected.AddListener(OnConnected);
            pacman.animator.speed = 0;
        }

        private float sendingTimer = 0;
        private void FixedUpdate()
        {
            if (client == null) return;
            if (!client.connected) return;
            if (!inGame) return;

            sendingTimer += Time.fixedDeltaTime;
            if (sendingTimer > 1f)
            {
                SendPosition();
                SendMovement();
            }
            GetPosition();
        }

        // CLIENT-SERVER INTERACTION ==============================================================
        // Отправка-приём
        //=========================================================================================
        private void SendPosition()
        {
            client.SendRequest("POS:" + PackSize(x, y)); 
        }

        private void SendMovement()
        {
            client.SendRequest("MOV:" + PackMovement());
        }

        private void GetPosition()
        {
            string position = client.PickLastAnswer();
            if (string.IsNullOrWhiteSpace(position)) return;

            try
            {
                position = position.Replace("POS:", "");
                string[] result = position.Split('x');
                int getX = Convert.ToInt32(result[0]);
                int getY = Convert.ToInt32(result[1]);

                // проверяем, что мы туда НЕ двигаемся
                bool notMovingHere = getX != newX || getY != newY;
                bool notHere = getX != x || getY != y;

                if (notMovingHere && notHere)
                {
                    // меняем курс только когда предыдущая корутина схлопнулась!
                    if (!inMovement)
                    {
                        inMovement = true;
                        newX = getX;
                        newY = getY;
                        StartCoroutine("MoveTo");
                    }
                }
            }
            catch (Exception error)
            {
                Debug.Log(error);
            }
        }

        // ON CONNECTED ===========================================================================
        // Действия при подключении
        //=========================================================================================
        private void OnConnected()
        {
            if (!inGame) GetLevel();
            else SendPosition(); // иначе отправляем серверу своё последнее положение
        }

        public async void GetLevel()
        {
            inGame = false;

            while (!inGame) 
            // если у нас карта не прочиталсь верно с первого раза, будем кидать
            // реквест карты до тех пор, пока не прочитаемся или пока клиент не отключится
            {
                try
                {
                    // отправляем запрос на карту
                    client.SendRequest("MAP");
                    List<string> map = await client.PickLastPackedAnswer();

                    // читаем размеры карты
                    var mapSize = UnpackSize(map[0]);
                    int width = mapSize.x;
                    int height = mapSize.y;
                    bool[,] bmap = new bool[width, height];
                    map.RemoveAt(0); // удаляем из пакета в целях удобства

                    // читаем положение пакмана
                    var pacPos = UnpackSize(map[0]);
                    map.RemoveAt(0); // удаляем из пакета в целях удобства

                    // читаем остальные строчки, грузимся в массив
                    Debug.Log($"Loading Map: {width}x{height}");
                    for (int x = 0; x < width; x++)
                    {
                        char[] line = map[x].ToCharArray();
                        for (int y = 0; y < height; y++)
                        {
                            bmap[x, y] = line[y] == '1' ? true : false;
                        }
                    }

                    // отправляем карту билдиться 
                    builder.LoadMap(bmap);

                    // выставляем позицию
                    ForceSetCellPosition(pacPos.x, pacPos.y);

                    // начинаем игру
                    inGame = true;
                }
                catch (Exception error)
                {
                    inGame = false; // на всякий случай
                    Debug.LogWarning($"MAP ERROR: {error}");
                    if (!client.connected) return;
                }
            }
        }

        private void ForceSetCellPosition(int x, int y)
        {
            this.x = x; this.y = y;
            ForceSetWorldPos(GetDesiredPosition(x, y));
            SendPosition();
        }

        private void ForceSetWorldPos(Vector3 target)
        {
            pacman.transform.position = target;
        }

        private Vector3 GetWorldPos()
        {
            return pacman.transform.position;
        }

        private Vector3 GetDesiredPosition(int x, int y)
        {
            return new Vector3(x + 0.5f, y + 0.5f, 0);
        }

        // MOVEMENT CONTROLLER ====================================================================
        // Контроль положения
        //=========================================================================================
        private void SetDirection()
        {
            Vector3 euler = new Vector3(0, 0, 0);
            if (newX > x) // идём вправо
            {
                pacman.sprite.flipX = false;
                pacman.sprite.flipY = false;
                euler.z = 0;
            }
            else if (newX < x) // идём влево
            {
                pacman.sprite.flipX = true;
                pacman.sprite.flipY = false;
                euler.z = 0;
            }
            else if (newY > y) // идём вверх
            {
                pacman.sprite.flipX = false;
                pacman.sprite.flipY = false;
                euler.z = 90;
            }   
            else if (newY < y) // идём вниз
            {
                pacman.sprite.flipX = false;
                pacman.sprite.flipY = true;
                euler.z = -90;
            }
            pacman.transform.eulerAngles = euler;
        }

        private int newX = -1, newY = -1;
        private bool inMovement = false;
        private IEnumerator MoveTo()
        {
            // фиксим недоразумения, если нам что-то очевидно не то приехало
            int dirX = newX - x;
            int distX = Mathf.Abs(dirX);
            if (distX > 1) // слишком длинный шаг по X
            {
                newX = x + Mathf.RoundToInt(Mathf.Sign(dirX));
            }

            int dirY = newY - y;
            int distY = Mathf.Abs(dirY);
            if (distY > 1) // слишком длинный шаг по Y
            {
                newY = y + Mathf.RoundToInt(Mathf.Sign(dirY));
            }

            if (newX != x && newY != y) // реквестирована ходьба наисокосок
            {
                bool wallNextX = builder.map[newX, y];
                if (wallNextX) newX = x;
                else newY = y;
            }

            // если после фиксов выяснилось, что мы слишком хороши
            if (newY == y && newX == x) yield break;

            SetDirection();

            inMovement = true;

            pacman.animator.speed = 1;

            float movementTimer = 0;
            Vector3 positionStart = GetWorldPos();
            Vector3 targetPosition = GetDesiredPosition(newX, newY);
            while (movementTimer < cellStepTime)
            {
                movementTimer += timeStep;
                float t = movementTimer / cellStepTime;
                ForceSetWorldPos(Vector3.Lerp(positionStart, targetPosition, t));
                if (t > 0.5f) { x = newX; y = newY; } // мы считаемся уже на новой клетке
                yield return new WaitForSeconds(timeStep);
            }

            client.KillRecievingStreak();
            client.KillSendingStreak();

            ForceSetCellPosition(newX, newY);

            pacman.animator.speed = 0;

            inMovement = false;
        }

        // SERVICE ================================================================================
        // Сервисные функции
        //=========================================================================================
        private string PackMovement()
        {
            bool[] input = playerController.controlInput;
            char[] charstr = new char[4];
            charstr[0] = input[0] ? '1' : '0'; // up
            charstr[1] = input[1] ? '1' : '0'; // down
            charstr[2] = input[2] ? '1' : '0'; // left
            charstr[3] = input[3] ? '1' : '0'; // right
            return new string(charstr);
        }

        private string PackSize(int x, int y) { return $"{x}x{y}"; }

        private (int x, int y) UnpackSize(string size)
        {
            string[] splitsize = size.Split('x');
            try // да, это накладно, но иногда бывает ошибка здесь
            {
                int x = Convert.ToInt32(splitsize[0]);
                int y = Convert.ToInt32(splitsize[1]);
                return (x, y);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"{error}");
                return (1, 1);
            }
        }
    }

}