using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static Unity.Collections.AllocatorManager;
using UnityEngine.UIElements;

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
public class BlockParams
{
    public GameObject Prefab;
    public Vector2Int Position;
    public Vector2Int FinishPosition;
    public Sprite sprite;
    public Sprite finishSprite;
    public float moveSpeed = 5.0f;

    public bool UniversalBlock = false;
    public bool CollideWithBlocks = true;
    public bool CollideWithWalls = true;
    [HideInInspector] public bool IsControllable;
}

public class LevelEditor : MonoBehaviour
{
    public BlockController blockController;

    [Header("General settings")]
    public Vector2Int levelSize;
    [MinVector2Int(1,1)] public Vector2Int tileSize;
    
    public bool StarsForLevel;
    public int MovesForLevel;
    [DisabledIf("StarsForLevel")] public int TwoStarsMoves;
    [DisabledIf("StarsForLevel")] public int ThreeStarsMoves;

    [Space(5)]
    public BlockParams[] initialBlocks;
    public Vector2Int[] walls;
    public Sprite wallSprite;
    [HideInInspector] public GameObject[] createdBlocks;
    [HideInInspector] public GameObject[] createdWalls;
    [HideInInspector] public GameObject[] createdFinishBlocks;

    [Header("Optional settings")]
    [CompactHeader("Can be leaved as default")]
    [Space(5)]
    public Camera camera;
    [Space(5)]
    public float paddingTop = 10f;
    public float paddingBottom = 10f;
    public float paddingLeft = 10f;
    public float paddingRight = 10f;

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

        grid = new int[levelSize.x, levelSize.y];

        DrawBackground();
        DrawWalls();
        DrawBlocks();
        DrawFinishTiles();
    }

    public void ClearLevel()
    {
        var blocksParent = transform.Find("Blocks");
        if (blocksParent != null)
        {
            DestroyImmediate(blocksParent.gameObject);
        }

        var wallsParent = transform.Find("Walls");
        if (wallsParent != null)
        {
            DestroyImmediate(wallsParent.gameObject);
        }

        var finishParent = transform.Find("FinishTiles");
        if (finishParent != null)
        {
            DestroyImmediate(finishParent.gameObject);
        }

        var backgroundParent = transform.Find("Background");
        if (backgroundParent != null)
        {
            DestroyImmediate(backgroundParent.gameObject);
        }

        grid = null;
    }

    void Start()
    {
        if (blockController == null)
            blockController = FindFirstObjectByType<BlockController>();

        if (camera == null)
            camera = Camera.main;


        grid = new int[levelSize.x, levelSize.y];

        SetupCamera();
        DrawBackground();
        DrawWalls();
        DrawBlocks();
        DrawFinishTiles();
    }

    private void SetupCamera()
    {
        float screenAspect = (float)Screen.width / Screen.height;

        float totalWidth = levelSize.x * tileSize.x + paddingLeft + paddingRight;
        float totalHeight = levelSize.y * tileSize.y + paddingTop + paddingBottom;

        float centerX = totalWidth / 2f - tileSize.x / 2f;
        float centerY = totalHeight / 2f - tileSize.y / 2f;

        camera.transform.position = new Vector3(centerX - paddingLeft, centerY - paddingBottom, -10);

        float verticalSize = totalHeight / 2f;
        float horizontalSize = totalWidth / 2f / screenAspect;

        camera.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
    }

    private void DrawBackground()
    {
        GameObject backgroundParent = new GameObject("Background");
        backgroundParent.transform.parent = transform;

        for (int i = 0; i < levelSize.x; i++)
        {
            for (int j = 0; j < levelSize.y; j++)
            {
                GameObject backgroundTile;
                Vector2 position = new Vector2(i * tileSize.x, j * tileSize.y);

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

    private void DrawWalls()
    {
        if (walls != null)
        {
            GameObject WallsParent = new GameObject("Walls");
            WallsParent.transform.parent = transform;

            createdWalls = new GameObject[walls.Length];
            for (int i = 0; i < walls.Length; i++)
            {
                grid[walls[i].x, walls[i].y] = 1;
                Vector2 position = new Vector2(walls[i].x * tileSize.x, walls[i].y * tileSize.y);

                createdWalls[i] = new GameObject($"Wall {i}");
                createdWalls[i].transform.parent = transform;

                var spriteRenderer = createdWalls[i].AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = wallSprite;

                createdWalls[i].transform.position = position;

                createdWalls[i].transform.localScale = new Vector3(tileSize.x, tileSize.y, 1);

                createdWalls[i].transform.parent = WallsParent.transform;
            }
        }
    }

    private void DrawBlocks()
    {
        GameObject BlocksParent = new GameObject("Blocks");
        BlocksParent.transform.parent = transform;

        createdBlocks = new GameObject[initialBlocks.Length];

        for (int i = 0; i < initialBlocks.Length; i++)
        {
            grid[initialBlocks[i].Position.x, initialBlocks[i].Position.y] = 2;
            Vector2 position = new Vector2(initialBlocks[i].Position.x * tileSize.x, initialBlocks[i].Position.y * tileSize.y);

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


        createdFinishBlocks = new GameObject[createdBlocks.Length];

        for (int i = 0; i < initialBlocks.Length; i++)
        {
            grid[initialBlocks[i].FinishPosition.x, initialBlocks[i].FinishPosition.y] = 0;
            Vector2 position = new Vector2(initialBlocks[i].FinishPosition.x * tileSize.x, initialBlocks[i].FinishPosition.y * tileSize.y);

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
            createdFinishBlocks[i].transform.parent = FinishParent.transform;
        }
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
