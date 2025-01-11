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
    public Sprite SelectedObjectMenuSprite;
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
    [HideInInspector] bool wasClickedBlocksMenu;

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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            sceneController.ReloadSceneWithNoDelay();

        if (blocks.Length != 0)
        {
            if (selectedBlockIndex >= blocks.Length)
                selectedBlockIndex = blocks.Length - 1;

            // движение блоков
            if (!isMoving && blocks[selectedBlockIndex].IsControllable)
            {
                // клавиатура
                if (Input.GetKeyDown(moveUpKey))
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.up));
                else if (Input.GetKeyDown(moveDownKey))
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.down));
                else if (Input.GetKeyDown(moveLeftKey))
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.left));
                else if (Input.GetKeyDown(moveRightKey))
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.right));

                else if (Input.GetKeyDown(KeyCode.Alpha1) && blocks[0] != null)
                    selectedBlockIndex = 0;
                else if (Input.GetKeyDown(KeyCode.Alpha2) && blocks[1] != null)
                    selectedBlockIndex = 1;
                else if (Input.GetKeyDown(KeyCode.Alpha3) && blocks[2] != null)
                    selectedBlockIndex = 2;
                else if (Input.GetKeyDown(KeyCode.Alpha4) && blocks[3] != null)
                    selectedBlockIndex = 3;

                // сенсорное
                if (Input.touchCount > 0)
                {
                    Touch touch = Input.GetTouch(0);
                    if (touch.phase == TouchPhase.Began)
                        startTouchPosition = touch.position;
                    else if (touch.phase == TouchPhase.Ended)
                    {
                        endTouchPosition = touch.position;

                        // Проверяем выбор блока
                        Vector2 touchWorldPosition = Camera.main.ScreenToWorldPoint(endTouchPosition);
                        for (int i = 0; i < levelEditor.createdBlocksMenuPrefabs.Count; i++)
                        {
                            var prefab = levelEditor.createdBlocksMenuPrefabs[i];
                            var renderer = prefab.obj.GetComponent<SpriteRenderer>();

                            if (renderer != null && IsMouseOverObject(touchWorldPosition, renderer))
                            {
                                selectedBlockIndex = i;
                                wasClickedBlocksMenu = true;
                                break;
                            }
                        }

                        // Если блок не был выбран, обрабатываем свайп
                        if (!wasClickedBlocksMenu)
                            ProcessSwipe(startTouchPosition, endTouchPosition);

                        wasClickedBlocksMenu = false;
                    }
                }

                // мышью
                if (Input.GetMouseButtonDown(0))
                {
                    startMousePosition = Input.mousePosition;
                    Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

                    for (int i = 0; i < levelEditor.createdBlocksMenuPrefabs.Count; i++)
                    {
                        var prefab = levelEditor.createdBlocksMenuPrefabs[i];
                        var renderer = prefab.obj.GetComponent<SpriteRenderer>();

                        if (renderer != null && IsMouseOverObject(mousePosition, renderer))
                        {
                            selectedBlockIndex = i;
                            wasClickedBlocksMenu = true;
                            break;
                        }
                    }
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (!wasClickedBlocksMenu)
                    {
                        endMousePosition = Input.mousePosition;
                        ProcessSwipe(startMousePosition, endMousePosition);
                    }
                    wasClickedBlocksMenu = false;
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
                levelEditor.ChangeStarsSprite(-1);
                sceneController.ReloadSceneWithDelay();
            }

            // подсчёт звёзд
            if (isAllBlocksFinished())
            {
                LevelFinished.IsLevelFinished = true;
                if (levelEditor.StarsForLevel)
                {
                    if (appliedMoves <= levelEditor.ThreeStarsMoves)
                        levelEditor.ChangeStarsSprite(2);
                    else if (appliedMoves <= levelEditor.TwoStarsMoves)
                        levelEditor.ChangeStarsSprite(1);
                    else if (appliedMoves <= levelEditor.MovesForLevel)
                        levelEditor.ChangeStarsSprite(0);
                }
                else if (!levelEditor.StarsForLevel && appliedMoves <= levelEditor.MovesForLevel)
                    levelEditor.ChangeStarsSprite(2);
                sceneController.CheckAndLoadScene(blocksLeftToFinish == 0);
            }
            else
            {
                if (levelEditor.StarsForLevel)
                {
                    if (appliedMoves <= levelEditor.ThreeStarsMoves)
                        levelEditor.ChangeStarsSprite(2);
                    else if (appliedMoves <= levelEditor.TwoStarsMoves)
                        levelEditor.ChangeStarsSprite(1);
                    else if (appliedMoves <= levelEditor.MovesForLevel)
                        levelEditor.ChangeStarsSprite(0);
                }
                else
                    if (appliedMoves <= levelEditor.MovesForLevel)
                        levelEditor.ChangeStarsSprite(2);
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

    bool IsMouseOverObject(Vector2 mousePos, SpriteRenderer renderer)
    {
        Vector3 objectCenter = renderer.bounds.center;
        Vector3 objectExtents = renderer.bounds.extents;

        return mousePos.x >= objectCenter.x - objectExtents.x &&
               mousePos.x <= objectCenter.x + objectExtents.x &&
               mousePos.y >= objectCenter.y - objectExtents.y &&
               mousePos.y <= objectCenter.y + objectExtents.y;
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
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.right));
                else
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.left));
            }
            else
            {
                // Вертикальный свайп
                if (direction.y > 0)
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.up));
                else
                    StartCoroutine(MoveBlockAndTiles(createdBlocks[selectedBlockIndex], Vector2ToVector2Int(createdBlocksPositions[selectedBlockIndex]), Vector2Int.down));
            }
        }
    }

    public Vector2Int Vector2ToVector2Int(Vector2 vector)
    {
        return new Vector2Int((int)vector.x, (int)vector.y);
    }

    private IEnumerator MoveBlockAndTiles(GameObject block, Vector2Int blockPosition, Vector2Int direction)
    {
        yield return MoveBlockSmoothly(block, blockPosition, direction);
        yield return MoveInvisibilityTiles(direction);
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

                levelEditor.ChangeStarsSprite(-1);
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
                    isBreakerTileReached = true;
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

        levelEditor.TextMovesCount.ChangeTextForMoves(appliedMoves.ToString());

        isMoving = false;
    }

    private IEnumerator MoveInvisibilityTiles(Vector2 direction)
    {
        foreach (var invisibilityTile in levelEditor.uniqueTiles.Where(t => t.TileType == TileTypes.Invisibility && t.IsActive))
        {
            Vector2Int directionToMove = invisibilityTile.IsSymmetryMoving
                ? new Vector2Int((int)direction.x, (int)direction.y)
                : new Vector2Int(-(int)direction.x, -(int)direction.y);

            while (true)
            {
                Vector2Int nextPosition = invisibilityTile.Position + directionToMove;

                // Проверка на столкновение с препятствием
                if (!CanMoveToPosition(nextPosition))
                    break;

                yield return HandleInvisibilityTileRendering(invisibilityTile, nextPosition);

                // Двигаем тайл
                invisibilityTile.Position = nextPosition;
            }
        }
    }

    // Проверяет, можно ли переместиться на указанную позицию
    private bool CanMoveToPosition(Vector2Int position)
    {
        // Проверка на выход за границы уровня
        if (position.x < 0 || position.x >= levelEditor.levelSize.x || position.y < 0 || position.y >= levelEditor.levelSize.y)
            return false;

        // Проверка на столкновение с другими тайлами
        if (levelEditor.uniqueTiles.Any(t => t.Position == position && t.IsActive))
            return false;

        if (grid[position.x, position.y] == 1 || grid[position.x, position.y] == 2)
            return false;

        return true;
    }

    private IEnumerator HandleInvisibilityTileRendering(AdditionalTiles invisibilityTile, Vector2Int nextPosition)
    {
        if (invisibilityTile.IsActive)
        {
            isMoving = true;
            var associatedObject = levelEditor.createdAdditionalTiles.FirstOrDefault(obj => GetBlockPosition(obj) == invisibilityTile.Position);
            if (associatedObject == null) yield break;

            var renderer = associatedObject.GetComponent<SpriteRenderer>();
            if (renderer == null) yield break;

            var color = renderer.color;
            color.a = appliedMoves % invisibilityTile.VisibilityFreq == 0 ? 1f : 0f;
            renderer.color = color;

            Vector3 startPosition = associatedObject.transform.position;
            Vector3 endPosition = CalculateWorldPosition(nextPosition);

            float moveSpeed = blocks[selectedBlockIndex].moveSpeed; // Скорость из основного блока

            float distance = Vector3.Distance(startPosition, endPosition);

            while (distance > 0.01f) // Проверка, чтобы остановить анимацию, когда объект почти на месте
            {
                float step = moveSpeed * Time.deltaTime;
                associatedObject.transform.position = Vector3.MoveTowards(associatedObject.transform.position, endPosition, step);
                distance = Vector3.Distance(associatedObject.transform.position, endPosition);

                yield return null;
            }

            associatedObject.transform.position = CalculateWorldPosition(nextPosition);
            isMoving = false;
        }
        yield return null;
    }

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
            if (levelEditor.uniqueTiles.FirstOrDefault(t => (Vector2)t.Position == newPosition && t.TileType == TileTypes.Invisibility && t.IsActive) != null)
            {
                return false; // Тайл препятствует движению
            }
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
