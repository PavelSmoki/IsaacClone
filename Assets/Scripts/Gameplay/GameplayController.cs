using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using JetBrains.Annotations;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Zenject;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Game.Gameplay
{
    [UsedImplicitly]
    public class GameplayController : IInitializable
    {
        private const string StartingRoomKey = "StartingRoom";
        private const string LeftRightTransitionKey = "LeftRightTransition";
        private const string UpDownTransitionKey = "UpDownTransition";
        private const int GridSize = 32;
        private const int TransitionToGridOffset = 16;
        private const int RoomsAmount = 8;
        private readonly Vector2Int roomToGridOffset = new(4, 4);

        private GameObject[,] _spawnedRooms;
        private RoomData[,] _spawnedRoomsData;

        private readonly EnemyFactory _enemyFactory;
        private readonly DiContainer _container;
        private readonly Grid _grid;

        private Vector2Int _treasureRoomPlace;

        public GameplayController(EnemyFactory enemyFactory, DiContainer container, Grid grid)
        {
            _enemyFactory = enemyFactory;
            _container = container;
            _grid = grid;
        }

        public void Initialize()
        {
            _spawnedRooms = new GameObject[9, 9];
            _spawnedRoomsData = new RoomData[9, 9];

            var startingRoomPrefab = Addressables.LoadAssetAsync<GameObject>(StartingRoomKey).WaitForCompletion();
            var startingRoomObj = Object.Instantiate(startingRoomPrefab);
            var startingRoomData = startingRoomObj.GetComponent<RoomData>();

            _spawnedRooms[4, 4] = startingRoomObj;
            _spawnedRoomsData[4, 4] = startingRoomData;
            _enemyFactory.Create(startingRoomData);

            for (var i = 0; i < RoomsAmount; i++)
            {
                PlaceOneRoom();
            }

            PlaceTreasureRoom();
            PlaceBossRoom();

            startingRoomData.OpenAllTransitions();
        }

        private HashSet<Vector2Int> FindVacantPlaces()
        {
            var vacantPlaces = new HashSet<Vector2Int>();
            for (var x = 0; x < _spawnedRooms.GetLength(0); x++)
            {
                for (var y = 0; y < _spawnedRooms.GetLength(1); y++)
                {
                    if (_spawnedRooms[x, y] == null) continue;

                    var maxX = _spawnedRooms.GetLength(0) - 1;
                    var maxY = _spawnedRooms.GetLength(1) - 1;

                    if (x > 0 && _spawnedRooms[x - 1, y] == null) vacantPlaces.Add(new Vector2Int(x - 1, y));
                    if (x < maxX && _spawnedRooms[x + 1, y] == null) vacantPlaces.Add(new Vector2Int(x + 1, y));
                    if (y > 0 && _spawnedRooms[x, y - 1] == null) vacantPlaces.Add(new Vector2Int(x, y - 1));
                    if (y < maxY && _spawnedRooms[x, y + 1] == null) vacantPlaces.Add(new Vector2Int(x, y + 1));
                }
            }

            return vacantPlaces;
        }

        private GameObject InstantiateRoom(HashSet<Vector2Int> vacantPlaces, out RoomData newRoomData,
            out Vector2Int position, string roomKey)
        {
            var newRoomPrefab = Addressables.LoadAssetAsync<GameObject>(roomKey)
                .WaitForCompletion();
            var newRoomObj = Object.Instantiate(newRoomPrefab);

            newRoomData = newRoomObj.GetComponent<RoomData>();

            position = vacantPlaces.ElementAt(Random.Range(0, vacantPlaces.Count));
            newRoomObj.transform.position = new Vector3((position.x - roomToGridOffset.x) * GridSize,
                (position.y - roomToGridOffset.y) * GridSize, 0);

            return newRoomObj;
        }

        private void SetArraysElements(Vector2Int position, GameObject newRoomObj, RoomData newRoomData)
        {
            _spawnedRooms[position.x, position.y] = newRoomObj;
            _spawnedRoomsData[position.x, position.y] = newRoomData;

            _spawnedRoomsData[position.x, position.y].OnRoomEnter += OnRoomEntered;
        }

        private void PlaceOneRoom()
        {
            var vacantPlaces = FindVacantPlaces();
            var newRoomObj = InstantiateRoom(vacantPlaces, out var newRoomData, out var position,
                "Room" + Random.Range(1, 9));
            SetArraysElements(position, newRoomObj, newRoomData);
            PlaceTransitions(newRoomData, position);
        }

        private void PlaceTreasureRoom()
        {
            var vacantPlaces = FindVacantPlaces();
            var treasureRoomVacantPlaces = FindVacantPlacesWithOneNeighbour(vacantPlaces);
            var newRoomObj = InstantiateRoom(treasureRoomVacantPlaces, out var newRoomData, out var position,
                "TreasureRoom");
            SetArraysElements(position, newRoomObj, newRoomData);
            PlaceTransitions(newRoomData, position);
            _container.InjectGameObject(newRoomObj);

            _treasureRoomPlace = new Vector2Int(position.x, position.y);
        }

        private void PlaceBossRoom()
        {
            var allVacantPlaces = FindVacantPlaces();
            var bossRoomVacantPlaces = FindVacantPlacesWithOneNeighbour(allVacantPlaces);
            bossRoomVacantPlaces.RemoveWhere(pos =>
                pos == _treasureRoomPlace + Vector2Int.left || pos == _treasureRoomPlace + Vector2Int.right ||
                pos == _treasureRoomPlace + Vector2Int.up || pos == _treasureRoomPlace + Vector2Int.down);
            var newRoomObj = InstantiateRoom(bossRoomVacantPlaces, out var newRoomData, out var position,
                "BossRoom");
            SetArraysElements(position, newRoomObj, newRoomData);
            PlaceTransitions(newRoomData, position);
        }

        private HashSet<Vector2Int> FindVacantPlacesWithOneNeighbour(HashSet<Vector2Int> allVacantPlaces)
        {
            var maxX = _spawnedRooms.GetLength(0) - 1;
            var maxY = _spawnedRooms.GetLength(1) - 1;

            var vacantPlacesWithOneNeighbour = new HashSet<Vector2Int>();

            foreach (var pos in allVacantPlaces)
            {
                var neighboursAmount = 0;
                if (pos.x - 1 >= 0 && _spawnedRooms[pos.x - 1, pos.y] != null)
                {
                    neighboursAmount++;
                }

                if (pos.x + 1 <= maxX && _spawnedRooms[pos.x + 1, pos.y] != null)
                {
                    neighboursAmount++;
                }

                if (pos.y - 1 >= 0 && _spawnedRooms[pos.x, pos.y - 1] != null)
                {
                    neighboursAmount++;
                }

                if (pos.y + 1 <= maxY && _spawnedRooms[pos.x, pos.y + 1] != null)
                {
                    neighboursAmount++;
                }

                if (neighboursAmount == 1)
                {
                    vacantPlacesWithOneNeighbour.Add(new Vector2Int(pos.x, pos.y));
                }
            }

            return vacantPlacesWithOneNeighbour;
        }

        private void PlaceTransitions(RoomData room, Vector2Int pos)
        {
            var maxX = _spawnedRooms.GetLength(0) - 1;
            var maxY = _spawnedRooms.GetLength(1) - 1;

            if (pos.x - 1 >= 0)
            {
                var leftNeighbour = _spawnedRoomsData[pos.x - 1, pos.y];

                if (leftNeighbour != null && leftNeighbour.RightTransition == null)
                {
                    var transitionPrefab =
                        Addressables.LoadAssetAsync<GameObject>(LeftRightTransitionKey).WaitForCompletion();
                    var transition = Object.Instantiate(transitionPrefab, _grid.transform)
                        .GetComponent<RoomTransition>();
                    transition.transform.position = room.transform.position + Vector3.left * TransitionToGridOffset;

                    room.LeftTransition = transition;
                    leftNeighbour.RightTransition = transition;

                    room.RemoveHorizontalWallOnTransition(transition.transform.position + new Vector3(5.5f, 0.5f, 0f));
                    leftNeighbour.RemoveHorizontalWallOnTransition(transition.transform.position +
                                                                   new Vector3(-4.5f, 0.5f, 0f));
                }
            }

            if (pos.x + 1 <= maxX)
            {
                var rightNeighbour = _spawnedRoomsData[pos.x + 1, pos.y];
                if (rightNeighbour != null && rightNeighbour.LeftTransition == null)
                {
                    var transitionPrefab =
                        Addressables.LoadAssetAsync<GameObject>(LeftRightTransitionKey).WaitForCompletion();
                    var transition = Object.Instantiate(transitionPrefab, _grid.transform)
                        .GetComponent<RoomTransition>();
                    transition.transform.position = room.transform.position + Vector3.right * TransitionToGridOffset;

                    room.RightTransition = transition;
                    rightNeighbour.LeftTransition = transition;

                    room.RemoveHorizontalWallOnTransition(transition.transform.position + new Vector3(-4.5f, 0.5f, 0f));
                    rightNeighbour
                        .RemoveHorizontalWallOnTransition(transition.transform.position + new Vector3(5.5f, 0.5f, 0f));
                }
            }

            if (pos.y + 1 <= maxY)
            {
                var upperNeighbour = _spawnedRoomsData[pos.x, pos.y + 1];
                if (upperNeighbour != null && upperNeighbour.LowerTransition == null)
                {
                    var transitionPrefab =
                        Addressables.LoadAssetAsync<GameObject>(UpDownTransitionKey).WaitForCompletion();
                    var transition = Object.Instantiate(transitionPrefab, _grid.transform)
                        .GetComponent<RoomTransition>();
                    transition.transform.position = room.transform.position + Vector3.up * TransitionToGridOffset;

                    room.UpperTransition = transition;
                    upperNeighbour.LowerTransition = transition;

                    room.RemoveVerticalWallOnTransition(transition.transform.position + new Vector3(-0.5f, -4.5f, 0f));
                    upperNeighbour
                        .RemoveVerticalWallOnTransition(transition.transform.position + new Vector3(-0.5f, 5.5f, 0f));
                }
            }

            if (pos.y - 1 >= 0)
            {
                var lowerNeighbour = _spawnedRoomsData[pos.x, pos.y - 1];
                if (lowerNeighbour != null && lowerNeighbour.UpperTransition == null)
                {
                    var transitionPrefab =
                        Addressables.LoadAssetAsync<GameObject>(UpDownTransitionKey).WaitForCompletion();
                    var transition = Object.Instantiate(transitionPrefab, _grid.transform)
                        .GetComponent<RoomTransition>();
                    transition.transform.position = room.transform.position + Vector3.down * TransitionToGridOffset;

                    room.LowerTransition = transition;
                    lowerNeighbour.UpperTransition = transition;

                    room.RemoveVerticalWallOnTransition(transition.transform.position + new Vector3(-0.5f, 5.5f, 0f));
                    lowerNeighbour
                        .RemoveVerticalWallOnTransition(transition.transform.position + new Vector3(-0.5f, -4.5f, 0f));
                }
            }
        }

        private void OnRoomEntered(RoomData roomData)
        {
            if (roomData.IsRoomCleared) return;

            _enemyFactory.Create(roomData);

            roomData.CloseAllTransitions();
            roomData.EnemyCount.Subscribe(_ => CheckForEnemies(roomData));
        }

        private void CheckForEnemies(RoomData roomData)
        {
            if (roomData.EnemyCount.Value == 0)
            {
                roomData.OpenAllTransitions();
            }
        }
    }
}