using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
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

    [Header("General settings")]
    public bool sameSpeedForAllBlocks;
    [DisabledIf("sameSpeedForAllBlocks")] public float moveSpeed;

    [Space(10)]

    public Sprite SelectedSprite;
    [HideInInspector] GameObject selectedObject;

    [HideInInspector] private bool isMoving = false;

    [HideInInspector] public GameObject[] createdBlocks;
    [HideInInspector] public GameObject[] createdFinishBlocks;
    [HideInInspector] public GameObject[] createdWalls;
    [HideInInspector] public int finishedBlocks;

    [HideInInspector] public int selectedBlockIndex = 0;
    private int maxIndex;
    private int minIndex;

    [Header("Controls")]
    public KeyCode moveUpKey = KeyCode.UpArrow;
    public KeyCode moveDownKey = KeyCode.DownArrow;
    public KeyCode moveLeftKey = KeyCode.LeftArrow;
    public KeyCode moveRightKey = KeyCode.RightArrow;

    [HideInInspector] public BlockParams[] blocks;
    [HideInInspector] public int[,] grid;
    [HideInInspector] public int appliedMoves;

    void Start()
    {
        if (levelEditor == null)
            levelEditor = FindFirstObjectByType<LevelEditor>();

        blocks = levelEditor.initialBlocks;
        grid = levelEditor.grid;
        createdWalls = levelEditor.createdWalls;
        createdBlocks = levelEditor.createdBlocks;
        createdFinishBlocks = levelEditor.createdFinishBlocks;

        for (int i = 0; i <= blocks.Length - 1; i++)
        {
            blocks[i].IsControllable = true;
        }

        maxIndex = blocks.Length > 0 ? blocks.Length - 1 : 0;
        minIndex = 0;

        if (blocks.Length != 0)
        {
            selectedObject = new GameObject();
            var spriteRender = selectedObject.AddComponent<SpriteRenderer>();
            spriteRender.sortingOrder = 2;
            spriteRender.sprite = SelectedSprite;
            var vector2intToVector2 = new Vector2(createdBlocks[selectedBlockIndex].transform.position.x, createdBlocks[selectedBlockIndex].transform.position.y);
            selectedObject.transform.position = vector2intToVector2;
        }
    }

    void Update()
    {
        if (blocks.Length != 0)
        {
            var vector2intToVector2 = new Vector2(
                createdBlocks[selectedBlockIndex].transform.position.x,
                createdBlocks[selectedBlockIndex].transform.position.y);
            selectedObject.transform.position = vector2intToVector2;

            //if (finishedBlocks == blocks.Length)
            //{
            //    LevelFinished.IsLevelFinished = true;
            //    Debug.Log("Level Finished");
            //}

            if (!isMoving && blocks[selectedBlockIndex].IsControllable)
            {
                if (Input.GetKeyDown(moveUpKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], Vector2Int.up));
                }
                else if (Input.GetKeyDown(moveDownKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], Vector2Int.down));
                }
                else if (Input.GetKeyDown(moveLeftKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], Vector2Int.left));
                }
                else if (Input.GetKeyDown(moveRightKey))
                {
                    StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], Vector2Int.right));
                }
            }
            if (!isMoving)
            {
                if (Input.GetAxis("Mouse ScrollWheel") > 0f && selectedBlockIndex + 1 <= maxIndex)
                    selectedBlockIndex++;
                else if (Input.GetAxis("Mouse ScrollWheel") < 0f && selectedBlockIndex - 1 >= minIndex)
                    selectedBlockIndex--;
                CheckBlocksFinish();
            }

            if (isAllBlocksFinished())
            {
                LevelFinished.IsLevelFinished = true;
                if (levelEditor.ThreeStarsMoves != 0 & appliedMoves <= levelEditor.ThreeStarsMoves)
                    Debug.Log("Three Stars");
                else if (levelEditor.TwoStarsMoves != 0 & appliedMoves <= levelEditor.TwoStarsMoves)
                    Debug.Log("Two Stars");
                else if (appliedMoves <= levelEditor.MovesForLevel)
                    Debug.Log("One star (or stars disabled)");
            }
            // если закончились ходы
            if (appliedMoves >= levelEditor.MovesForLevel)
            {
                if (isAllBlocksFinished())
                    LevelFinished.IsLevelFinished = true;
                else
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        blocks[i].IsControllable = false;
                    }
            }
        }
    }

    IEnumerator MoveBlockSmoothly(GameObject block, Vector2Int direction)
    {
        Vector2Int startBlockPosition = GetBlockPosition(block);
        Vector2Int targetBlockPosition = startBlockPosition;

        while (CanMove(targetBlockPosition, direction))
        {
            targetBlockPosition += direction;
            
        }

        if (targetBlockPosition == startBlockPosition)
        {
            yield break;
        }

        appliedMoves++;

        UpdateGrid(startBlockPosition, targetBlockPosition);

        isMoving = true;

        Vector3 startPosition = block.transform.position;
        Vector3 targetPosition = new Vector3(targetBlockPosition.x, targetBlockPosition.y, block.transform.position.z);

        float distance = Vector3.Distance(startPosition, targetPosition);
        float duration = sameSpeedForAllBlocks ? moveSpeed : distance / blocks[selectedBlockIndex].moveSpeed;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            block.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        block.transform.position = targetPosition;

        isMoving = false;
    }

    bool isAllBlocksFinished()
    {
        int q = blocks.Length;
        for (int i = 0; i < blocks.Length; i++)
        {
            if (GetBlockPosition(createdBlocks[i]) == blocks[i].FinishPosition)
                q--;
        }
        if (q == 0)
            return true;
        return false;
    }

    void CheckBlocksFinish()
    {
        for (int i = 0; i < createdBlocks.Length; i++)
        {
            if (blocks[i].IsControllable == true)
            {
                if (blocks[i].UniversalBlock)
                {
                    for(int a = 0; a < createdBlocks.Length; a++)
                    {
                        if (GetBlockPosition(createdBlocks[i]) == blocks[a].FinishPosition)
                        {
                            blocks[i].IsControllable = false;
                            finishedBlocks++;
                            break;
                        }
                    }
                }
                else if (blocks[i].UniversalBlock == false & GetBlockPosition(createdBlocks[i]) == blocks[i].FinishPosition)
                {
                    blocks[i].IsControllable = false;
                    finishedBlocks++;
                }
            }
        }
    }

    void MoveBlockFast(GameObject block, Vector2Int direction)
    {
        Vector2Int blockPosition = GetBlockPosition(block);

        while (CanMove(blockPosition, direction))
        {
            blockPosition += direction;
        }

        UpdateGrid(GetBlockPosition(block), blockPosition);
        block.transform.position = new Vector3(blockPosition.x, blockPosition.y, block.transform.position.z);
    }

    bool CanMove(Vector2Int currentPosition, Vector2Int direction)
    {
        Vector2Int newPosition = currentPosition + direction;

        if (newPosition.x >= 0 && newPosition.x < grid.GetLength(0) &&
            newPosition.y >= 0 && newPosition.y < grid.GetLength(1))
        {
            if (grid[newPosition.x, newPosition.y] == 0)
                return true;
            if (grid[newPosition.x, newPosition.y] == 1 && blocks[selectedBlockIndex].CollideWithWalls == false)
                return true;
            if (grid[newPosition.x, newPosition.y] == 2 && blocks[selectedBlockIndex].CollideWithBlocks == false)
                return true;
        }

        return false;
    }

    Vector2Int GetBlockPosition(GameObject block)
    {
        return new Vector2Int(Mathf.RoundToInt(block.transform.position.x), Mathf.RoundToInt(block.transform.position.y));
    }

    void UpdateGrid(Vector2Int oldPosition, Vector2Int newPosition)
    {
        grid[oldPosition.x, oldPosition.y] = 0;
        grid[newPosition.x, newPosition.y] = 2;
    }
}
