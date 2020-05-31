using System;
using System.Text;

namespace PacmanServerConsole
{
    public enum Direction { Left, Right, Up, Down, None }

    public class PacmanGame
    {
        // GAME INSTANCE ==========================================================================
        // Экземпляр игры
        //=========================================================================================
        public PacmanMap map { get; private set; }
        public int pacmanX { get; private set; } = 0;
        public int pacmanY { get; private set; } = 0;

        public PacmanGame()
        {
            map = new PacmanMap();
            map.onMapLoaded += SetDefaultPosition;
            map.ReadMap(""); // пытается прочитать дефолтную карту
        }

        // PACMAN POSITION ========================================================================
        // Наше положение на карте
        //=========================================================================================
        public void SetPosition(int x, int y)
        {
            pacmanX = x;
            pacmanY = y;
        }

        private void SetDefaultPosition()
        {
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    if (!map.bmap[x, y])
                    {
                        pacmanX = x;
                        pacmanY = y;
                        return;
                    }
                }
            }
        }

        // MOVEMENT RESOLVER ======================================================================
        // Решалка для движения
        //=========================================================================================
        #region MOVEMENT RESOLVER
        private Direction ResolveVertical(bool up, bool down)
        {
            if (!up && !down) return Direction.None;

            bool allowUp   = CheckCell(Direction.Up);
            bool allowDown = CheckCell(Direction.Down);

            if (up)
            {
                if (allowUp)
                {
                    if (prevDirection == Direction.Down)
                    {
                        if (down && allowDown) return Direction.Down;
                    }
                    else return Direction.Up;
                }
            }

            if (down)
            {
                if (allowDown) return Direction.Down;
            }

            return Direction.None;
        }

        private Direction ResolveHorizontal(bool left, bool right)
        {
            if (!left && !right) return Direction.None;

            bool allowLeft = CheckCell(Direction.Left);
            bool allowRight = CheckCell(Direction.Right);

            if (left)
            {
                if (allowLeft)
                {
                    if (prevDirection == Direction.Right)
                    {
                        if (right && allowRight) return Direction.Right;
                    }
                    else return Direction.Left;
                }
            }

            if (right)
            {
                if (allowRight) return Direction.Right;
            }

            return Direction.None;
        }

        private Direction FinalResolve(Direction vertical, Direction horizontal)
        {
            if (vertical == Direction.None) return horizontal;
            if (horizontal == Direction.None) return vertical;
            return prevDirection == vertical ? horizontal : vertical; // приоритет выше у поворота, чем у продолжения движения
        }

        public Direction prevDirection { get; private set; } = Direction.None;
        /// <summary>
        /// Решает движение
        /// </summary>
        /// <param name="movement">Строка вида 0011, 1000 и пр.</param>
        /// <returns>В какую сторону мы подвинемся (если подвинемся) вследствие движения</returns>
        public (int x, int y, Direction move) ResolveMovement(string movement)
        {
            if (movement == "0000")
            {
                prevDirection = Direction.None;
                return (pacmanX, pacmanY, Direction.None);
            }

            bool up    = movement[0] == '1';
            bool down  = movement[1] == '1';
            bool left  = movement[2] == '1';
            bool right = movement[3] == '1';

            Direction verticalResolve = ResolveVertical(up, down);
            Direction horizontalResolve = ResolveHorizontal(left, right);
            prevDirection = FinalResolve(verticalResolve, horizontalResolve);

            int x = pacmanX;
            int y = pacmanY;
            switch (prevDirection)
            {
                case Direction.Left:  x--; break;
                case Direction.Right: x++; break;
                case Direction.Up:    y++; break;
                case Direction.Down:  y--; break;
                case Direction.None: break;

                default: throw new ArgumentException("Enum changed?");
            }

            return (x, y, prevDirection);
        }

        /// <summary>
        /// Проверяет клетку в нужной стороне от нас
        /// </summary>
        /// <param name="direction">Направление</param>
        /// <returns>Заполнена ли клетка</returns>
        public bool CheckCell(Direction direction)
        {
            int checkX = pacmanX, checkY = pacmanY;
            switch (direction)
            {
                case Direction.Left:  checkX -= 1; break; // проверяем клетку слева от нас
                case Direction.Right: checkX += 1; break; // проверяем клетку справа от нас
                case Direction.Up:    checkY += 1; break; // проверяем клетку вверх от нас
                case Direction.Down:  checkY -= 1; break; // проверяем клетку вниз от нас
                case Direction.None:  return true; // на месте мы всегда топтаться можем
                default: throw new ArgumentException("What the hell?");
            }

            // если мы внезапно на пустом краю - это проблема
            if (checkX < 0 || checkX >= map.width || checkY < 0 || checkY >= map.height) return false;

            // просто возвращаем инвертированную клетку битовой карты
            return !map.bmap[checkX, checkY]; 
        }
        #endregion
    }
}
