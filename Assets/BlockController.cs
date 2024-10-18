using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static Unity.Collections.AllocatorManager;

[System.Serializable]
public class Block
{
    public GameObject Prefab;
    public Vector2Int Position;
    public Vector2Int FinishPosition;
    [HideInInspector] public bool IsControllable;
}

public class BlockController : MonoBehaviour
{
    [Header("General settings")]
    [SerializeField] public Vector2Int levelSize;
    [SerializeField] public Block[] blocks;
    [SerializeField] public Vector2Int[] walls;

    private int[,] grid; // 0 - empty
                         // 1 - wall
                         // 2 - block

    [SerializeField] public Sprite finishIconSprite;
    [SerializeField] public Sprite wallSprite;

    public float moveDuration = 0.2f;
    public float moveSpeed = 1.0f;

    [HideInInspector] private bool isMoving = false;

    [HideInInspector] public GameObject[] createdBlocks;
    [HideInInspector] public GameObject[] finishBlocks;
    [HideInInspector] public GameObject[] wallsBlocks;

    public int selectedBlockIndex = 0;
    private int maxIndex;
    private int minIndex;

    void Start()
    {
        for (int i = 0; i <= blocks.Length - 1; i++)
        {
            blocks[i].IsControllable = true;
        }

        maxIndex = blocks.Length - 1;
        minIndex = 0;

        grid = new int[levelSize.x, levelSize.y];

        if (walls != null)
        {
            wallsBlocks = new GameObject[walls.Length];
            for (int i = 0; i < walls.Length; i++)
            {
                grid[walls[i].x, walls[i].y] = 1;
                var vector2intToVector2 = new Vector2(walls[i].x, walls[i].y);
                wallsBlocks[i] = new GameObject();
                var spriteRenderer = wallsBlocks[i].AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = wallSprite;
                wallsBlocks[i].transform.position = vector2intToVector2;
            }
        }

        createdBlocks = new GameObject[blocks.Length];

        for (int i = 0; i < blocks.Length; i++)
        {
            grid[blocks[i].Position.x, blocks[i].Position.y] = 2;
            var vector2intToVector2 = new Vector2(blocks[i].Position.x, blocks[i].Position.y);
            createdBlocks[i] = Instantiate(blocks[i].Prefab, vector2intToVector2, Quaternion.identity);
        }

        finishBlocks = new GameObject[blocks.Length];

        for (int i = 0; i < blocks.Length; i++)
        {
            grid[blocks[i].FinishPosition.x, blocks[i].FinishPosition.y] = 0;
            var vector2intToVector2 = new Vector2(blocks[i].FinishPosition.x, blocks[i].FinishPosition.y);
            finishBlocks[i] = Instantiate(blocks[i].Prefab, vector2intToVector2, Quaternion.identity);
            finishBlocks[i].GetComponent<SpriteRenderer>().sprite = finishIconSprite;
        }
    }

    void Update()
    {
        if (!isMoving && blocks[selectedBlockIndex].IsControllable)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], Vector2Int.up));
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], Vector2Int.down));
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                StartCoroutine(MoveBlockSmoothly(createdBlocks[selectedBlockIndex], Vector2Int.left));
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
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

        UpdateGrid(startBlockPosition, targetBlockPosition);

        isMoving = true;

        Vector3 startPosition = block.transform.position;
        Vector3 targetPosition = new Vector3(targetBlockPosition.x, targetBlockPosition.y, block.transform.position.z);

        float elapsedTime = 0f;
        while (elapsedTime < moveDuration)
        {
            block.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        block.transform.position = targetPosition;

        isMoving = false;
    }

    void CheckBlocksFinish()
    {
        for (int i = 0; i < createdBlocks.Length; i++)
        {
            if (GetBlockPosition(createdBlocks[i]) == blocks[i].FinishPosition && blocks[i].IsControllable == true)
                blocks[i].IsControllable = false;
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
            newPosition.y >= 0 && newPosition.y < grid.GetLength(1) &&
            grid[newPosition.x, newPosition.y] == 0)
        {
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
