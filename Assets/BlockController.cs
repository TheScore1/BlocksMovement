using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Android.Gradle;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static Unity.Collections.AllocatorManager;


[CustomPropertyDrawer(typeof(DisabledIfAttribute))]
public class DisabledIfDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        DisabledIfAttribute disabledIf = (DisabledIfAttribute)attribute;
        SerializedProperty conditionProperty = property.serializedObject.FindProperty(disabledIf.ConditionField);

        bool isDisabled = conditionProperty != null && !conditionProperty.boolValue;

        EditorGUI.BeginDisabledGroup(isDisabled);
        EditorGUI.PropertyField(position, property, label);
        EditorGUI.EndDisabledGroup();
    }
}

public class DisabledIfAttribute : PropertyAttribute
{
    public string ConditionField;

    public DisabledIfAttribute(string conditionField)
    {
        ConditionField = conditionField;
    }
}

public class BlockController : MonoBehaviour
{
    public LevelEditor levelEditor;
    public SceneController sceneController;

    [Header("General settings")]
    public bool sameSpeedForAllBlocks;
    [DisabledIf("sameSpeedForAllBlocks")] public float moveSpeed;
    public bool DisableMovementForBlockOnFinish;

    [Space(10)]

    public Sprite SelectedSprite;
    [HideInInspector] GameObject selectedObject;

    [HideInInspector] private bool isMoving = false;

    [HideInInspector] public GameObject[] createdBlocks;
    [HideInInspector] public Vector2[] createdBlocksPositions;
    [HideInInspector] public GameObject[] createdFinishBlocks;
    [HideInInspector] public GameObject[] createdWalls;
    [HideInInspector] public int finishedBlocks;

    [HideInInspector] public Vector2 tileSize;

    [HideInInspector] public int selectedBlockIndex = 0;
    private int maxIndex;
    private int minIndex;
    [HideInInspector] public BlockParams[] blocks;
    [HideInInspector] public int[,] grid;
    [HideInInspector] public int movesLeft;
    [HideInInspector] public int appliedMoves;
    [HideInInspector] public int blocksLeftToFinish;
    [HideInInspector] public Vector2Int moveDirection;

    [HideInInspector] public bool isBreakerTileReached;
    private List<IEnumerator> movingBlocks = new List<IEnumerator>();

    [Header("Controls")]
    public KeyCode moveUpKey = KeyCode.UpArrow;
    public KeyCode moveDownKey = KeyCode.DownArrow;
    public KeyCode moveLeftKey = KeyCode.LeftArrow;
    public KeyCode moveRightKey = KeyCode.RightArrow;

    // сенсорные экраны и свайпы мышью
    private Vector2 startTouchPosition;
    private Vector2 endTouchPosition;

    private Vector2 startMousePosition;
    private Vector2 endMousePosition;

    public float minSwipeDistance = 50f;
    

    void Start()
    {
        if (levelEditor == null)
            levelEditor = FindFirstObjectByType<LevelEditor>();

        blocks = levelEditor.initialBlocks;
        grid = levelEditor.grid;
        createdWalls = levelEditor.createdWalls;
        createdBlocks = levelEditor.createdBlocks;
        createdBlocksPositions = levelEditor.createdBlocksPositions;
        createdFinishBlocks = levelEditor.createdFinishBlocks;
        tileSize = levelEditor.tileSize;
        blocksLeftToFinish = levelEditor.createdBlocks.Length;

        for (int i = 0; i <= blocks.Length - 1; i++)
        {
            blocks[i].IsControllable = true;
        }

        maxIndex = blocks.Length > 0 ? blocks.Length - 1 : 0;
        minIndex = 0;

        if (blocks.Length != 0)
        {
            selectedObject = new GameObject("Selected Block Icon");
            var spriteRender = selectedObject.AddComponent<SpriteRenderer>();
            spriteRender.sortingOrder = 2;
            spriteRender.sprite = SelectedSprite;
            selectedObject.transform.position = createdBlocks[selectedBlockIndex].transform.position;
            selectedObject.transform.localScale = createdBlocks[selectedBlockIndex].transform.localScale;
        }

        movesLeft = levelEditor.MovesForLevel;
    }

    private void CountSelectedBlockPrefabIndex()
    {
        maxIndex = levelEditor.UniquePrefabsCount > 0 ? levelEditor.UniquePrefabsCount - 1 : 0;
        minIndex = 0;

        Debug.Log($"CountSelectedBlockPrefabIndex - UniquePrefabsCount: {levelEditor.UniquePrefabsCount}, MaxIndex: {maxIndex}");
    }

    void Update()
    {
        if (blocks.Length != 0)
        {
            if (selectedBlockIndex >= blocks.Length)
                selectedBlockIndex = blocks.Length - 1;

            Debug.Log("Finished = " + blocks[selectedBlockIndex].Finished);
            Debug.Log("Blocks left to finish = " + blocksLeftToFinish);

            // движение блоков
            if (!isMoving && blocks[selectedBlockIndex].IsControllable)
            {
                // клавиатура
                if (Input.GetKeyDown(moveUpKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.up));
                }
                else if (Input.GetKeyDown(moveDownKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.down));
                }
                else if (Input.GetKeyDown(moveLeftKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.left));
                }
                else if (Input.GetKeyDown(moveRightKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.right));
                }
                // сенсорное
                if (Input.touchCount > 0)
                {
                    Touch touch = Input.GetTouch(0);

                    if (touch.phase == TouchPhase.Began)
                    {
                        startTouchPosition = touch.position;
                    }
                    else if (touch.phase == TouchPhase.Ended)
                    {
                        endTouchPosition = touch.position;
                        ProcessSwipe(startTouchPosition, endTouchPosition);
                    }
                }
                // мышью
                if (Input.GetMouseButtonDown(0))
                {
                    startMousePosition = Input.mousePosition;
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    endMousePosition = Input.mousePosition;
                    ProcessSwipe(startMousePosition, endMousePosition);
                }
            }

            // когда движение закончено, то можно сменить активный блок и проверка встал ли на финиш
            if (!isMoving)
            {
                if (Input.GetAxis("Mouse ScrollWheel") > 0f && selectedBlockIndex + 1 <= maxIndex)
                    selectedBlockIndex++;
                else if (Input.GetAxis("Mouse ScrollWheel") < 0f && selectedBlockIndex - 1 >= minIndex)
                    selectedBlockIndex--;
                CheckBlocksFinish();
            }

            if (movesLeft == 0 && !isAllBlocksFinished())
            {
                isMoving = true;
                sceneController.ReloadSceneWithDelay();
            }

            // подсчёт звёзд
            if (isAllBlocksFinished())
            {
                LevelFinished.IsLevelFinished = true;
                if (levelEditor.StarsForLevel == true & appliedMoves <= levelEditor.ThreeStarsMoves)
                    Debug.Log("Three Stars");
                else if (levelEditor.StarsForLevel == true & appliedMoves <= levelEditor.TwoStarsMoves)
                    Debug.Log("Two Stars");
                else if (appliedMoves == levelEditor.MovesForLevel || isAllBlocksFinished())
                    Debug.Log("One star (or stars disabled)");
                sceneController.CheckAndLoadScene(blocksLeftToFinish == 0);
            }

            // если уровень пройден выключаем управление блоками
            if (LevelFinished.IsLevelFinished)
            {
                for (int i = 0; i < blocks.Length; i++)
                    blocks[i].IsControllable = false;
            }

            if (createdBlocks[selectedBlockIndex] != null)
                selectedObject.transform.position = createdBlocks[selectedBlockIndex].transform.position;
        }
        else
        {
            selectedBlockIndex = 0;
            levelEditor.UniquePrefabs = null;
            levelEditor.UniquePrefabsCount = 0;
            Destroy(selectedObject);
        }
    }

    private void UpdateInvisibilityTiles()
    {
        foreach (var tile in levelEditor.uniqueTiles.Where(t => t.TileType == TileTypes.Invisibility))
        {
            // Обновление прозрачности
            if (appliedMoves % 2 == 0)
            {
                float alpha = 1; // Значение от 0 до 1
                var renderer = levelEditor.createdAdditionalFinishBlocks.FirstOrDefault(w => (Vector2)tile.Position == (Vector2)w.transform.position)?.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    var color = renderer.color;
                    renderer.color = new Color(color.r, color.g, color.b, alpha);
                }
            }

            if (appliedMoves % 2 == 1)
            {
                float alpha = 0; // Значение от 0 до 1
                var renderer = levelEditor.createdAdditionalFinishBlocks.FirstOrDefault(w => (Vector2)tile.Position == (Vector2)w.transform.position)?.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    var color = renderer.color;
                    renderer.color = new Color(color.r, color.g, color.b, alpha);
                }
            }

            // Перемещение вместе с активным блоком
            if (tile.IsSymmetryMoving)
            {
                var newPos = tile.Position - createdBlocksPositions[selectedBlockIndex];
                Vector2Int offset = new Vector2Int((int)newPos.x, (int)newPos.y);
                var newPos1 = createdBlocksPositions[selectedBlockIndex] - offset;
                tile.Position = new Vector2Int((int)(newPos1.x), (int)(newPos1.y));
            }
            else
            {
                if (isMoving)
                    tile.Position += (Vector2Int)moveDirection; // Двигаться с блоком
            }
        }
    }

    private void ProcessSwipe(Vector2 start, Vector2 end)
    {
        if (isMoving) return;

        float distance = Vector2.Distance(start, end);
        if (distance >= minSwipeDistance)
        {
            Vector2 direction = end - start;
            direction.Normalize();

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                // Горизонтальный свайп
                if (direction.x > 0)
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.right));
                else
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.left));
            }
            else
            {
                // Вертикальный свайп
                if (direction.y > 0)
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.up));
                else
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], createdBlocksPositions[selectedBlockIndex], Vector2Int.down));
            }
        }
    }

    IEnumerator MoveBlockSmoothly(GameObject block, Vector2 positionInGrid, Vector2Int direction)
    {
        Vector2 startBlockPosition = positionInGrid;
        Vector2 targetBlockPosition = startBlockPosition;

        while (true)
        {
            // Проверяем, можем ли двигаться дальше
            if (!CanMove(targetBlockPosition, direction))
            {
                break;
            }

            // Рассчитываем следующую позицию
            Vector2 nextPosition = targetBlockPosition + (Vector2)direction;

            //if (levelEditor.uniqueTiles.Count(z => z.TileType == TileTypes.Invisibility && z.IsActive) > 0)
            //{

            //}

            AdditionalTiles teleportTile = levelEditor.uniqueTiles
                .FirstOrDefault(t => ((Vector2)t.Position == nextPosition || (Vector2)t.Position2 == nextPosition) && t.TileType == TileTypes.Teleport && t.IsActive);
            AdditionalTiles stopperTile = levelEditor.uniqueTiles.FirstOrDefault(z => (Vector2)z.Position == nextPosition && z.TileType == TileTypes.Stopper && z.IsActive);
            AdditionalTiles pitTile = levelEditor.uniqueTiles.FirstOrDefault(z => (Vector2)z.Position == nextPosition && z.TileType == TileTypes.Pit && z.IsActive);
            AdditionalTiles breakerTile = levelEditor.uniqueTiles.FirstOrDefault(z => (Vector2)z.Position == nextPosition && z.TileType == TileTypes.Breaker && z.IsActive);

            if (teleportTile != null)
            {
                yield return MoveToPosition(block, block.transform.position, CalculateWorldPosition(new Vector2Int((int)nextPosition.x, (int)nextPosition.y)));

                Vector2Int teleportTarget = (Vector2)teleportTile.Position == nextPosition ? teleportTile.Position2 : teleportTile.Position;

                block.transform.position = CalculateWorldPosition(teleportTarget);
                targetBlockPosition = teleportTarget;

                // Проверяем, можем ли продолжить движение после телепорта
                nextPosition = targetBlockPosition + (Vector2)direction;

                if (!CanMove(targetBlockPosition, direction))
                {
                    break;
                }
            }
            else if (stopperTile != null)
            {
                yield return MoveToPosition(block, block.transform.position, CalculateWorldPosition(new Vector2Int((int)nextPosition.x, (int)nextPosition.y)));
                Vector2Int stopperTarget = stopperTile.Position;
                block.transform.position = CalculateWorldPosition(stopperTarget);
                targetBlockPosition = stopperTarget;
                break;
            }
            else if (pitTile != null)
            {
                yield return MoveToPosition(block, block.transform.position, CalculateWorldPosition(new Vector2Int((int)nextPosition.x, (int)nextPosition.y)));

                sceneController.ReloadSceneWithDelay();
                isMoving = true;
                yield break;
            }
            else if (breakerTile != null)
            {
                yield return MoveToPosition(block, block.transform.position, CalculateWorldPosition(new Vector2Int((int)nextPosition.x, (int)nextPosition.y)));
                targetBlockPosition = nextPosition;

                if (isBreakerTileReached)
                {
                    isBreakerTileReached = false;
                    break;
                }
                else
                {
                    isBreakerTileReached = true;

                    //StartAllBlocksMovement(direction);
                }
            }
            else
            {
                //просто продолжаем движение
                targetBlockPosition = nextPosition;
            }
        }
        moveDirection = direction;
        if (block != null)
        {
            // Анимация блока к конечной позиции
            yield return MoveToPosition(block, block.transform.position, CalculateWorldPosition(new Vector2Int((int)targetBlockPosition.x, (int)targetBlockPosition.y)));

            // Фиксируем финальную позицию
            UpdateGrid(startBlockPosition, targetBlockPosition);
            createdBlocksPositions[selectedBlockIndex] = targetBlockPosition;
        }

        appliedMoves++;
        movesLeft--;

        isMoving = false;
    }

    //private void StartAllBlocksMovement(Vector2Int direction)
    //{
    //    for (int blockIndex = 0; blockIndex < createdBlocksPositions.Length; blockIndex++)
    //    {
    //        if (blocks[blockIndex] != null)
    //        {
    //            IEnumerator moveCoroutine = MoveBlockSmoothly(
    //                createdBlocks[blockIndex],
    //                createdBlocksPositions[blockIndex],
    //                direction
    //            );
    //            movingBlocks.Add(moveCoroutine);
    //            StartCoroutine(moveCoroutine);
    //        }
    //    }

    //    // Ожидаем завершения движения всех блоков
    //    StartCoroutine(WaitForAllBlocksToFinish());
    //}

    private IEnumerator WaitForAllBlocksToFinish()
    {
        while (movingBlocks.Any(coroutine => coroutine != null))
        {
            yield return null;
        }

        movingBlocks.Clear();
        isMoving = false;
    }

    IEnumerator MoveToPosition(GameObject block, Vector3 currentPosition, Vector3 targetPosition)
    {
        if (block != null)
        {
            isMoving = true;
            Vector3 startPosition = currentPosition;
            Vector3 endPosition = targetPosition;

            float distance = Vector3.Distance(startPosition, endPosition);
            float duration = sameSpeedForAllBlocks ? moveSpeed : distance / blocks[selectedBlockIndex].moveSpeed;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                block.transform.position = Vector3.Lerp(startPosition, endPosition, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            block.transform.position = endPosition;
            isMoving = false;
        }
    }

    // Метод для расчёта позиции блока в мире
    Vector3 CalculateWorldPosition(Vector2Int gridPosition)
    {
        return new Vector3(
            Screen.width / 2 - (levelEditor.levelSize.x * tileSize.x / 2) + tileSize.x * gridPosition.x + tileSize.x / 2,
            levelEditor.BotHeightSpaceAfterLevel + tileSize.y * gridPosition.y + tileSize.y / 2
        );
    }

    bool isAllBlocksFinished()
    {
        return blocksLeftToFinish == 0;
    }

    void CheckBlocksFinish()
    {
        for (int i = 0; i < createdBlocks.Length; i++)
        {
            if (blocks[i].IsControllable == true)
            {
                if (blocks[i].Finished == false)
                {
                    if (blocks[i].UniversalBlock)
                    {
                        for (int a = 0; a < createdBlocks.Length; a++)
                        {
                            if (GetBlockPosition(createdBlocks[i]) == blocks[a].FinishPosition ||
                                blocks[a].AdditionalFinishPositions.Any(pos => pos == GetBlockPosition(createdBlocks[i])))
                            {
                                if (DisableMovementForBlockOnFinish)
                                    blocks[i].IsControllable = false;
                                finishedBlocks++;
                                blocksLeftToFinish--;
                                blocks[i].Finished = true;
                                break;
                            }
                        }
                    }
                    else if (!blocks[i].UniversalBlock &&
                             (GetBlockPosition(createdBlocks[i]) == blocks[i].FinishPosition ||
                              blocks[i].AdditionalFinishPositions.Any(pos => pos == GetBlockPosition(createdBlocks[i]))))
                    {
                        if (DisableMovementForBlockOnFinish)
                            blocks[i].IsControllable = false;
                        finishedBlocks++;
                        blocksLeftToFinish--;
                        blocks[i].Finished = true;
                    }
                }

                if (blocks[i].Finished == true)
                {
                    if (blocks[i].UniversalBlock)
                    {
                        bool stillOnAnyFinish = false;
                        for (int a = 0; a < createdBlocks.Length; a++)
                        {
                            if (GetBlockPosition(createdBlocks[i]) == blocks[a].FinishPosition ||
                                blocks[a].AdditionalFinishPositions.Any(pos => pos == GetBlockPosition(createdBlocks[i])))
                            {
                                stillOnAnyFinish = true;
                                break;
                            }
                        }

                        if (!stillOnAnyFinish)
                        {
                            finishedBlocks--;
                            blocksLeftToFinish++;
                            blocks[i].Finished = false;
                        }
                    }
                    else if (!blocks[i].UniversalBlock &&
                             GetBlockPosition(createdBlocks[i]) != blocks[i].FinishPosition &&
                             !blocks[i].AdditionalFinishPositions.Any(pos => pos == GetBlockPosition(createdBlocks[i])))
                    {
                        finishedBlocks--;
                        blocksLeftToFinish++;
                        blocks[i].Finished = false;
                    }
                }
            }
        }
    }

    bool CanMove(Vector2 currentPosition, Vector2Int direction)
    {
        Vector2 newPosition = currentPosition + direction;

        if (newPosition.x >= 0 && newPosition.x < grid.GetLength(0) &&
            newPosition.y >= 0 && newPosition.y < grid.GetLength(1))
        {
            if (levelEditor.uniqueTiles.FirstOrDefault(t => (Vector2)t.Position == newPosition && t.TileType == TileTypes.Invisibility) == null)
            if (grid[(int)newPosition.x, (int)newPosition.y] == 0)
                return true;
            if (grid[(int)newPosition.x, (int)newPosition.y] == 1 && blocks[selectedBlockIndex].CollideWithWalls == false)
                return true;
            if (grid[(int)newPosition.x, (int)newPosition.y] == 2 && blocks[selectedBlockIndex].CollideWithBlocks == false)
                return true;
        }

        return false;
    }

    Vector2Int GetBlockPosition(GameObject block)
    {
        // Смещение сетки (учитываем, что LevelSize включает границы уровня)
        float gridOriginX = Screen.width / 2 - (levelEditor.levelSize.x * tileSize.x / 2);
        float gridOriginY = levelEditor.BotHeightSpaceAfterLevel;

        // Позиция блока в сетке
        int gridX = Mathf.FloorToInt((block.transform.position.x - gridOriginX) / tileSize.x);
        int gridY = Mathf.FloorToInt((block.transform.position.y - gridOriginY) / tileSize.y);

        return new Vector2Int(gridX, gridY);
    }


    private void UpdateGrid(Vector2 oldPosition, Vector2 newPosition)
    {
        grid[(int)oldPosition.x, (int)oldPosition.y] = 0;
        grid[(int)newPosition.x, (int)newPosition.y] = 2;

        DebugGrid();
    }

    private void DebugGrid()
    {
        string gridState = "Current Grid State:\n";
        for (int y = grid.GetLength(1) - 1; y >= 0; y--)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                gridState += grid[x, y] == 2 ? "[X]" : "[  ]";
            }
            gridState += "\n";
        }
        //Debug.Log(gridState);
    }


}
