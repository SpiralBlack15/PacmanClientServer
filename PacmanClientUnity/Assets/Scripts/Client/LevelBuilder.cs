using UnityEngine;
using UnityEngine.Tilemaps;

namespace Spiral.PacmanGame
{
    public class LevelBuilder : MonoBehaviour
    {
        public Camera orthoCamera = null;

        public Tilemap layerFloor = null;
        public Tilemap layerWall = null;
        public TileBase tileWall = null;
        public TileBase tileFloor = null;

        public bool[,] map { get; private set; } = new bool[23, 23];

        private void Awake()
        {
            BuildDefaultWalls(new bool[23, 23]); // TODO: заменить тем, что приходит с сервака
            RebuildTilemap();
        }

        public void LoadMap(bool[,] newmap)
        {
            map = newmap;
            RebuildTilemap();
        }

        private void RebuildTilemap()
        {
            layerFloor.ClearAllTiles();
            layerWall.ClearAllTiles();

            int w = map.GetLength(0); 
            int h = map.GetLength(1); 
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    layerFloor.SetTile(pos, tileFloor);
                    if (map[x, y]) layerWall.SetTile(pos, tileWall);
                }
            }

            Vector3 cameraPosition = orthoCamera.transform.position;
            cameraPosition.x = w / 2;
            cameraPosition.y = h / 2;
            orthoCamera.transform.position = cameraPosition;
            orthoCamera.orthographic = true;
            orthoCamera.orthographicSize = w * 0.6f;
        }

        private void BuildDefaultWalls(bool[,] wallMap)
        {
            map = wallMap;

            // пока что просто генерация
            int w = wallMap.GetLength(0); int w1 = w - 1;
            int h = wallMap.GetLength(1); int h1 = h - 1;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if ((x == 0) || (x == w1) || (y == 0) || (y == h1))
                    {
                        wallMap[x, y] = true;
                    }
                    else wallMap[x, y] = false;
                }
            }
        }
    }
}
