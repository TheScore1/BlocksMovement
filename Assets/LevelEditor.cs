using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static Unity.Collections.AllocatorManager;
using UnityEngine.UIElements;
using UnityEngine.U2D;
using System.Linq;

[CustomEditor(typeof(LevelEditor))]
public class LevelEditorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelEditor generator = (LevelEditor)target;

        if (GUILayout.Button("Generate Level"))
        {
            generator.GenerateLevel();
        }

        if (GUILayout.Button("Clear Level"))
        {
            generator.ClearLevel();
        }
    }
}

[System.Serializable]
public class BoundsTilesSprites
{
    public Sprite SideBot;
    public Sprite SideLeft;
    public Sprite SideRight;
    public Sprite SideTop;

    public Sprite CornerBotLeft;
    public Sprite CornerBotRight;
    public Sprite CornerTopLeft;
    public Sprite CornerTopRight;
}

public enum BoundsSpriteNumbers
{
    SideBot,
    SideLeft,
    SideRight,
    SideTop,

    CornerBotLeft,
    CornerBotRight,
    CornerTopLeft,
    CornerTopRight
}

[System.Serializable]
public class Wall
{
    public Vector2Int Position;
}

public enum TileTypes
{
    None,
    Teleport,
    Pit,
    Invisibility,
    Breaker,
    Stopper
}

[System.Serializable]
public class AdditionalTiles
{
    public TileTypes TileType;
    public Vector2Int Position;
    public Vector2Int Position2;
    public bool IsActive;
    public int VisibilityFreq;
    public bool IsSymmetryMoving;
}

[System.Serializable]
public class BlockParams
{
    public GameObject Prefab;
    public Vector2Int Position;
    public Vector2Int FinishPosition;
    public Vector2Int[] AdditionalFinishPositions;
    public Sprite sprite;
    public Sprite finishSprite;
    public float moveSpeed = 5.0f;

    public bool UniversalBlock = false;
    public bool CollideWithBlocks = true;
    public bool CollideWithWalls = true;
    [HideInInspector] public bool IsControllable = true;
    [HideInInspector] public bool Finished = false;
}

public class LevelEditor : MonoBehaviour
{
    public BlockController blockController;

    [Header("General settings")]
    public Vector2Int levelSize;
    [MinVector2Int(1,1)] public Vector2Int tileScale;
    [HideInInspector] public Vector2 tileSize;

    [Space(5)]
    public Sprite BackgroundSprite;

    [Space(5)]
    public BoundsTilesSprites tileBoundsSprites;

    public List<Wall> initialWalls = new List<Wall>();
    private Dictionary<BoundsSpriteNumbers, Sprite> wallsSpriteMap;

    public Sprite PitSprite;
    public Sprite TeleportSprite;
    public Sprite StopperSprite;

    [Space(5)]
    public bool StarsForLevel;
    public int MovesForLevel;
    [DisabledIf("StarsForLevel")] public int TwoStarsMoves;
    [DisabledIf("StarsForLevel")] public int ThreeStarsMoves;

    [Space(5)]
    public BlockParams[] initialBlocks;
    public AdditionalTiles[] uniqueTiles;
    [HideInInspector] public GameObject[] createdBlocks;
    [HideInInspector] public Vector2[] createdBlocksPositions;
    [HideInInspector] public GameObject[] createdWalls;
    [HideInInspector] public GameObject[] createdFinishBlocks;
    [HideInInspector] public GameObject[] createdAdditionalFinishBlocks;
    [HideInInspector] public HashSet<GameObject> UniquePrefabs;
    [HideInInspector] public int UniquePrefabsCount;

    [Header("Optional settings")]
    [CompactHeader("Can be leaved as default")]
    [Space(5)]
    public Camera camera;
    [Space(5)]
    public float CustomPaddingHorizontal = 0f;
    public float CustomPaddingVertical = 0f;

    [HideInInspector] public float paddingHorizontal;
    [HideInInspector] public float paddingVertical;

    [HideInInspector] public float TopHeightSpaceBeforeLevel;
    [HideInInspector] public float BotHeightSpaceAfterLevel;
    [HideInInspector] public float SpaceHeightForLevel;
    [HideInInspector] public float TotalSpaceHeightForLevel;

    [Space(5)]
    [Header("Background settings")]
    public bool chessGrid;
    public GameObject BackgroundPrefab;
    [DisabledIf("chessGrid")] public GameObject BackgroundPrefab2;

    public int[,] grid; // 0 - empty
                         // 1 - wall
                         // 2 - block

    public void GenerateLevel()
    {
        ClearLevel();

        InitializeGridAndWalls();
        InitializeInterfacelValues();
        InitializeWallsSpriteMap();
        DrawCompositeWalls();
        DrawAdditionalTiles();
        DrawBackgroundOfLevel();
        DrawBlocks();
        DrawFinishTiles();
        DrawBlocksMenu();

#if UNITY_EDITOR
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            // Перемещаем камеру
            sceneView.pivot = new Vector3(Screen.width / 2, Screen.height / 2);
            sceneView.Repaint(); // Обновляем SceneView
        }
#endif
    }

    public void ClearLevel()
    {
        var blocksParent = transform.Find("Blocks");
        if (blocksParent != null)
            DestroyImmediate(blocksParent.gameObject);

        var finishParent = transform.Find("FinishTiles");
        if (finishParent != null)
            DestroyImmediate(finishParent.gameObject);

        var backgroundLevelParent = transform.Find("Background of level");
        if (backgroundLevelParent != null)
            DestroyImmediate(backgroundLevelParent.gameObject);

        var outerWallsParent = transform.Find("Bounds of Level");
        if (outerWallsParent != null)
            DestroyImmediate(outerWallsParent.gameObject);

        var blocksMenuParent = transform.Find("Blocks Menu");
        if (blocksMenuParent != null)
            DestroyImmediate(blocksMenuParent.gameObject);

        var background = GameObject.Find("Background");
        if (background != null)
            DestroyImmediate(background.gameObject);

        var additionalTiles = GameObject.Find("Additional Tiles");
        if (additionalTiles != null)
            DestroyImmediate(additionalTiles.gameObject);

        grid = null;
    }

    void Start()
    {
        if (blockController == null)
            blockController = FindFirstObjectByType<BlockController>();

        if (camera == null)
            camera = Camera.main;

        ClearLevel();

        InitializeGridAndWalls();
        createdBlocksPositions = new Vector2[initialBlocks.Length];
        for (var i = 0; i <= initialBlocks.Length - 1; i++)
            createdBlocksPositions[i] = new Vector2(initialBlocks[i].Position.x, initialBlocks[i].Position.y);
        CalculateTileSize();
        InitializeInterfacelValues();
        InitializeWallsSpriteMap();
        CalculateTileSize();
        SetupCamera();
        DrawBackground();
        DrawCompositeWalls();
        DrawAdditionalTiles();
        DrawBackgroundOfLevel();
        DrawBlocks();
        DrawFinishTiles();
        DrawBlocksMenu();
    }

    private void InitializeGridAndWalls()
    {
        grid = new int[levelSize.x, levelSize.y];
        for (var i = 0; i < initialWalls.Count; i++)
            grid[initialWalls[i].Position.x, initialWalls[i].Position.y] = 1;

        for (var x = 0; x < levelSize.x; x++)
        {
            grid[x, 0] = 1;
            grid[x, levelSize.y - 1] = 1;
        }

        for (var y = 0; y < levelSize.y; y++)
        {
            grid[0, y] = 1;
            grid[levelSize.x - 1, y] = 1;
        }
    }

    private void InitializeWallsSpriteMap()
    {
        wallsSpriteMap = new Dictionary<BoundsSpriteNumbers, Sprite>
        {
            { BoundsSpriteNumbers.SideBot, tileBoundsSprites.SideBot },
            { BoundsSpriteNumbers.SideLeft , tileBoundsSprites.SideLeft },
            { BoundsSpriteNumbers.SideRight, tileBoundsSprites.SideRight },
            { BoundsSpriteNumbers.SideTop, tileBoundsSprites.SideTop },

            { BoundsSpriteNumbers.CornerBotLeft, tileBoundsSprites.CornerBotLeft },
            { BoundsSpriteNumbers.CornerBotRight, tileBoundsSprites.CornerBotRight },
            { BoundsSpriteNumbers.CornerTopLeft, tileBoundsSprites.CornerTopLeft },
            { BoundsSpriteNumbers.CornerTopRight, tileBoundsSprites.CornerTopRight }
        };
    }

    private void DrawCompositeWalls()
    {
        var boundsParent = new GameObject("Bounds of Level");
        boundsParent.transform.parent = transform;
        boundsParent.transform.localPosition = Vector3.zero;

        for (int x = 0; x < levelSize.x; x++)
        {
            for (int y = 0; y < levelSize.y; y++)
            {
                if (grid[x, y] == 1)
                    ProcessWall(x, y, boundsParent);
            }
        }
    }

    private void ProcessWall(int x, int y, GameObject boundsParent)
    {
        GameObject wallObject = new GameObject($"Wall ({x}, {y})");
        wallObject.transform.parent = boundsParent.transform;

        wallObject.transform.localPosition = new Vector3(
            x * tileSize.x,
            y * tileSize.y,
            0);

        if (!IsWall(x, y + 1))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.SideTop], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerTopLeft], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerTopRight], Vector2.zero);
        }

        // Нижняя стена
        if (!IsWall(x, y - 1))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.SideBot], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerBotLeft], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerBotRight], Vector2.zero);
        }

        // Левая стена
        if (!IsWall(x - 1, y))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.SideLeft], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerTopLeft], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerBotLeft], Vector2.zero);
        }

        // Правая стена
        if (!IsWall(x + 1, y))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.SideRight], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerBotRight], Vector2.zero);
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerTopRight], Vector2.zero);
        }

        // Углы:

        // Верхний левый угол
        if (IsWall(x - 1, y) && IsWall(x, y + 1) && !IsWall(x - 1, y + 1))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerTopLeft], Vector2.zero);
        }

        // Верхний правый угол
        if (IsWall(x + 1, y) && IsWall(x, y + 1) && !IsWall(x + 1, y + 1))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerTopRight], Vector2.zero);
        }

        // Нижний левый угол
        if (IsWall(x - 1, y) && IsWall(x, y - 1) && !IsWall(x - 1, y - 1))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerBotLeft], Vector2.zero);
        }

        // Нижний правый угол
        if (IsWall(x + 1, y) && IsWall(x, y - 1) && !IsWall(x + 1, y - 1))
        {
            AddSpriteRenderer(wallObject, wallsSpriteMap[BoundsSpriteNumbers.CornerBotRight], Vector2.zero);
        }

    }

    private void AddSpriteRenderer(GameObject parent, Sprite sprite, Vector2 localPosition)
    {
        GameObject spriteObject = new GameObject("Sprite");
        spriteObject.transform.parent = parent.transform;
        spriteObject.transform.localPosition = localPosition;

        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 1;
        spriteObject.transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);
    }

    private bool IsWall(int x, int y)
    {
        if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1))
        {
            return true;
        }

        return grid[x, y] == 1;
    }

    private void CalculateTileSize()
    {
        // Размеры уровня с учетом tileScale
        float levelWidth = levelSize.x * tileScale.x;
        float levelHeight = levelSize.y * tileScale.y;

        // Размеры доступного пространства (с учетом padding)
        float availableWidth = Screen.width - paddingHorizontal;
        float availableHeight = Screen.height - BotHeightSpaceAfterLevel - TopHeightSpaceBeforeLevel;

        // Вычисляем масштаб тайлов
        float scaleX = availableWidth / levelWidth;
        float scaleY = availableHeight / levelHeight;

        // Используем минимальный масштаб для сохранения адаптивности
        float finalScale = Mathf.Min(scaleX, scaleY);

        // Устанавливаем размеры тайлов
        tileSize = new Vector2(tileScale.x * finalScale, tileScale.y * finalScale);
    }

    private void InitializeInterfacelValues()
    {
        paddingHorizontal = paddingHorizontal + CustomPaddingHorizontal;
        paddingVertical = paddingVertical + CustomPaddingVertical;

        UniquePrefabsCount = CountUniquePrefabs();

        TopHeightSpaceBeforeLevel = Screen.height * 0.29f;
        SpaceHeightForLevel = Screen.height * 0.42f;
        BotHeightSpaceAfterLevel = Screen.height * 0.29f;
    }

    private int CountUniquePrefabs()
    {
        UniquePrefabs = new HashSet<GameObject>();

        foreach (var block in initialBlocks)
            if (block.Prefab != null)
                UniquePrefabs.Add(block.Prefab);

        return UniquePrefabs.Count;
    }

    private void SetupCamera()
    {
        float totalWidth = levelSize.x * tileSize.x + tileSize.x / 2 + tileSize.x * 2;
        float totalHeight = Screen.height;

        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        camera.transform.position = new Vector3(centerX, centerY, -10);

        float verticalSize = totalHeight / 2f;
        float horizontalSize = totalWidth / 2f;

        camera.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
    }

    private void DrawBackground()
    {
        GameObject background = new GameObject("Background");
        var spriteRenderer = background.AddComponent<SpriteRenderer>();

        if (BackgroundSprite != null)
        {
            spriteRenderer.sprite = BackgroundSprite;
            spriteRenderer.sortingOrder = -20;

            camera = Camera.main;

            // Рассчитываем размеры области игрового поля
            float fieldHeight = camera.orthographicSize * 2f;
            float fieldWidth = levelSize.x * tileSize.x;
            float padding = paddingHorizontal;
            float totalHeight = fieldHeight;
            float totalWidth = fieldWidth + tileSize.x * padding;

            // Рассчитываем масштаб изображения по вертикали
            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            float scaleY = totalHeight / spriteSize.y;

            // Масштабируем фон равномерно по оси Y
            background.transform.localScale = new Vector3(scaleY, scaleY, 1);

            // Позиционируем фон по центру
            float backgroundX = camera.transform.position.x;
            float backgroundY = camera.transform.position.y;

            background.transform.position = new Vector3(backgroundX, backgroundY, -7);

            // Обрезаем по ширине, если фон выходит за границы
            float scaledWidth = spriteSize.x * scaleY; // Масштабированная ширина фона
            if (scaledWidth > totalWidth)
            {
                // Вычисляем долю видимой ширины
                float visibleWidth = totalWidth / scaledWidth;

                // Устанавливаем обрезку спрайта
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                spriteRenderer.size = new Vector2(totalWidth, totalHeight);
                spriteRenderer.sprite = Sprite.Create(
                    BackgroundSprite.texture,
                    new Rect(
                        BackgroundSprite.rect.x + (BackgroundSprite.rect.width * (1 - visibleWidth) / 2f),
                        BackgroundSprite.rect.y,
                        BackgroundSprite.rect.width * visibleWidth,
                        BackgroundSprite.rect.height
                    ),
                    new Vector2(0.5f, 0.5f)
                );
            }
            return;
        }
        else
        {
            // Создание текстуры и спрайта
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(176f / 255f, 178f / 255f, 184f / 255f));
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = -20;

            Camera cam = Camera.main;

            float fieldWidth = levelSize.x * tileSize.x;
            float fieldHeight = levelSize.y * tileSize.y;

            float padding = paddingHorizontal;

            float totalWidth = fieldWidth + tileSize.x * padding;
            float screenHeight = cam.orthographicSize * 2f; // Высота экрана в мировых координатах

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            float scaleX = totalWidth / spriteSize.x;
            float scaleY = screenHeight / spriteSize.y;
            background.transform.localScale = new Vector3(scaleX, scaleY, 1);

            // Позиция фона (по центру игрового поля)
            float backgroundX = Screen.width / 2;
            float backgroundY = cam.transform.position.y; // Вертикальная позиция берётся из камеры
            background.transform.position = new Vector3(backgroundX, backgroundY, -7);
        }
    }

    public void DrawAdditionalTiles()
    {
        GameObject additionalTileParent = new GameObject("Additional Tiles");
        additionalTileParent.transform.parent = transform;
        additionalTileParent.transform.parent.position = new Vector3(Screen.width / 2 - levelSize.x * tileSize.x / 2 + tileSize.x / 2, BotHeightSpaceAfterLevel + tileSize.y / 2);

        var createdAdditionalTiles = new List<GameObject>();

        for (int i = 0; i < uniqueTiles.Length; i++)
        {
            grid[uniqueTiles[i].Position.x, uniqueTiles[i].Position.y] = 0;
            Vector2 position = new Vector2(additionalTileParent.transform.parent.position.x + uniqueTiles[i].Position.x * tileSize.x, additionalTileParent.transform.parent.position.y + uniqueTiles[i].Position.y * tileSize.y);
            Vector2 position2 = new Vector2(additionalTileParent.transform.parent.position.x + uniqueTiles[i].Position2.x * tileSize.x, additionalTileParent.transform.parent.position.y + uniqueTiles[i].Position2.y * tileSize.y);
            var thisTile = new GameObject();
            
            
            thisTile.transform.parent = transform;
            thisTile.transform.position = position;
            thisTile.transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);

            if (uniqueTiles[i].TileType == TileTypes.Teleport)
            {
                Debug.Log("teleport");
                var teleportTile = new GameObject();
                teleportTile.transform.position = position2;
                teleportTile.transform.parent = transform;
                teleportTile.transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);

                var rendererThisTile = thisTile.AddComponent<SpriteRenderer>();
                var rendererTeleportTile = teleportTile.AddComponent<SpriteRenderer>();

                rendererThisTile.sprite = TeleportSprite;
                rendererTeleportTile.sprite = TeleportSprite;

                teleportTile.transform.parent = additionalTileParent.transform;
                createdAdditionalTiles.Add(teleportTile);
            }

            else if (uniqueTiles[i].TileType == TileTypes.Pit)
            {
                Debug.Log("pit");
                var rendererThisTile = thisTile.AddComponent<SpriteRenderer>();
                rendererThisTile.sprite = PitSprite;
            }
                    
            else if (uniqueTiles[i].TileType == TileTypes.Stopper)
            {
                Debug.Log("stopper");
                var rendererThisTile = thisTile.AddComponent<SpriteRenderer>();
                rendererThisTile.sprite = StopperSprite;
            }

            thisTile.transform.parent = additionalTileParent.transform;
            createdAdditionalTiles.Add(thisTile);
        }
    }

    private void DrawBackgroundOfLevel()
    {
        GameObject backgroundParent = new GameObject("Background of level");
        backgroundParent.transform.parent = transform;
        backgroundParent.transform.parent.position = new Vector3(Screen.width / 2 - levelSize.x * tileSize.x / 2 + tileSize.x / 2, BotHeightSpaceAfterLevel + tileSize.y / 2);

        for (int i = 0; i < levelSize.x; i++)
        {
            for (int j = 0; j < levelSize.y; j++)
            {
                if (i == 0 || j == 0 || i == levelSize.x - 1 || j == levelSize.y - 1 || grid[i, j] == 1)
                    continue;

                GameObject backgroundTile;
                Vector2 position = new Vector2(backgroundParent.transform.parent.position.x + i * tileSize.x, backgroundParent.transform.parent.position.y + j * tileSize.y);

                if (chessGrid)
                {
                    if ((i + j) % 2 == 0)
                        backgroundTile = Instantiate(BackgroundPrefab, position, Quaternion.identity);
                    else
                        backgroundTile = Instantiate(BackgroundPrefab2, position, Quaternion.identity);
                }
                else
                {
                    backgroundTile = Instantiate(BackgroundPrefab, position, Quaternion.identity);
                }

                backgroundTile.transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);

                backgroundTile.transform.parent = backgroundParent.transform;
            }
        }
    }

    private void DrawBlocks()
    {
        GameObject BlocksParent = new GameObject("Blocks");
        BlocksParent.transform.parent = transform;
        BlocksParent.transform.parent.position = new Vector3(Screen.width / 2 - levelSize.x * tileSize.x / 2 + tileSize.x / 2, BotHeightSpaceAfterLevel + tileSize.y / 2);

        createdBlocks = new GameObject[initialBlocks.Length];

        for (int i = 0; i < initialBlocks.Length; i++)
        {
            grid[initialBlocks[i].Position.x, initialBlocks[i].Position.y] = 2;
            Vector2 position = new Vector2(BlocksParent.transform.parent.position.x + initialBlocks[i].Position.x * tileSize.x, BlocksParent.transform.parent.position.y + initialBlocks[i].Position.y * tileSize.y);

            createdBlocks[i] = Instantiate(initialBlocks[i].Prefab, position, Quaternion.identity);
            createdBlocks[i].transform.parent = transform;

            createdBlocks[i].transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);

            if (initialBlocks[i].sprite != null)
            {
                createdBlocks[i].GetComponent<SpriteRenderer>().sprite = initialBlocks[i].sprite;
            }

            createdBlocks[i].transform.parent = BlocksParent.transform;
        }
    }

    private void DrawFinishTiles()
    {
        GameObject FinishParent = new GameObject("FinishTiles");
        FinishParent.transform.parent = transform;
        FinishParent.transform.parent.position = new Vector3(Screen.width / 2 - levelSize.x * tileSize.x / 2 + tileSize.x / 2, BotHeightSpaceAfterLevel + tileSize.y / 2);

        createdFinishBlocks = new GameObject[createdBlocks.Length];

        var totalAdditionalFinishBlocks = 0;
        for (int i = 0; i < initialBlocks.Length; i++)
            totalAdditionalFinishBlocks += initialBlocks[i].AdditionalFinishPositions.Length;
      
        createdAdditionalFinishBlocks = new GameObject[totalAdditionalFinishBlocks];

        for (int i = 0; i < initialBlocks.Length; i++)
        {
            grid[initialBlocks[i].FinishPosition.x, initialBlocks[i].FinishPosition.y] = 0;
            Vector2 position = new Vector2(FinishParent.transform.parent.position.x + initialBlocks[i].FinishPosition.x * tileSize.x, FinishParent.transform.parent.position.y + initialBlocks[i].FinishPosition.y * tileSize.y);

            createdFinishBlocks[i] = Instantiate(initialBlocks[i].Prefab, position, Quaternion.identity);
            createdFinishBlocks[i].transform.parent = transform;

            createdFinishBlocks[i].transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);

            if (initialBlocks[i].finishSprite != null)
            {
                createdFinishBlocks[i].GetComponent<SpriteRenderer>().sprite = initialBlocks[i].finishSprite;
                
            }
            else
            {
                var tempColor = initialBlocks[i].Prefab.GetComponent<SpriteRenderer>().color;
                createdFinishBlocks[i].GetComponent<SpriteRenderer>().color = new Color(tempColor.r, tempColor.g, tempColor.b, 0.5f);
                createdFinishBlocks[i].GetComponent<SpriteRenderer>().sprite = initialBlocks[i].sprite;
            }
            createdFinishBlocks[i].GetComponent<SpriteRenderer>().sortingOrder = -5;
            createdFinishBlocks[i].transform.parent = FinishParent.transform;
            
            for (int j = 0; j < initialBlocks[i].AdditionalFinishPositions.Length; j++)
            {
                var thisAdditionalFinishPositions = initialBlocks[i].AdditionalFinishPositions;
                grid[thisAdditionalFinishPositions[j].x, thisAdditionalFinishPositions[j].y] = 0;

                position = new Vector2(FinishParent.transform.parent.position.x + thisAdditionalFinishPositions[j].x * tileSize.x, FinishParent.transform.parent.position.y + thisAdditionalFinishPositions[j].y * tileSize.y);

                createdAdditionalFinishBlocks[j] = Instantiate(initialBlocks[i].Prefab, position, Quaternion.identity);
                createdAdditionalFinishBlocks[j].transform.parent = transform;

                createdAdditionalFinishBlocks[j].transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);

                if (initialBlocks[i].finishSprite != null)
                {
                    createdAdditionalFinishBlocks[j].GetComponent<SpriteRenderer>().sprite = initialBlocks[i].finishSprite;
                }
                else
                {
                    var tempColor = initialBlocks[i].Prefab.GetComponent<SpriteRenderer>().color;
                    createdAdditionalFinishBlocks[j].GetComponent<SpriteRenderer>().color = new Color(tempColor.r, tempColor.g, tempColor.b, 0.5f);
                    createdAdditionalFinishBlocks[j].GetComponent<SpriteRenderer>().sprite = initialBlocks[i].sprite;
                }
                createdAdditionalFinishBlocks[j].GetComponent<SpriteRenderer>().sortingOrder = -5;
                createdAdditionalFinishBlocks[j].transform.parent = FinishParent.transform;
            }
        }
    }

    public void DrawBlocksMenu()
    {
        var BlockMenuParent = new GameObject("Blocks Menu");
        BlockMenuParent.transform.parent = transform;

        var createdMenuBlocks = new GameObject[UniquePrefabsCount];
        
    }

    void Update()
    {
    }

    public class MinVector2IntAttribute : PropertyAttribute
    {
        public int MinX;
        public int MinY;

        public MinVector2IntAttribute(int minX, int minY)
        {
            MinX = minX;
            MinY = minY;
        }
    }

    [CustomPropertyDrawer(typeof(MinVector2IntAttribute))]
    public class MinVector2IntDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            MinVector2IntAttribute minAttr = (MinVector2IntAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.Vector2Int)
            {
                EditorGUI.BeginProperty(position, label, property);

                Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
                EditorGUI.LabelField(labelRect, label);

                Rect fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, position.height);
                Vector2Int value = property.vector2IntValue;

                float halfWidth = fieldRect.width / 2f - 2f;
                Rect xRect = new Rect(fieldRect.x, fieldRect.y, halfWidth, fieldRect.height);
                Rect yRect = new Rect(fieldRect.x + halfWidth + 4f, fieldRect.y, halfWidth, fieldRect.height);

                value.x = Mathf.Max(EditorGUI.IntField(xRect, value.x), minAttr.MinX);
                value.y = Mathf.Max(EditorGUI.IntField(yRect, value.y), minAttr.MinY);

                property.vector2IntValue = value;

                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use MinVector2Int with Vector2Int.");
            }
        }
    }


    [CustomPropertyDrawer(typeof(CompactHeaderAttribute))]
    public class CompactHeaderDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            CompactHeaderAttribute header = (CompactHeaderAttribute)attribute;

            EditorGUI.LabelField(position, header.HeaderText, EditorStyles.boldLabel);
        }

        public override float GetHeight()
        {
            return EditorGUIUtility.singleLineHeight + 2f;
        }
    }

    public class CompactHeaderAttribute : PropertyAttribute
    {
        public string HeaderText;

        public CompactHeaderAttribute(string headerText)
        {
            HeaderText = headerText;
        }
    }


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
}
