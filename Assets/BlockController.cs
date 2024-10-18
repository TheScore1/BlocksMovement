using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
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

    private int[,] grid; // 0 - empty
                         // 1 - wall
                         // 2 - block

    public float moveSpeed = 1.0f;

    [HideInInspector] public GameObject[] createdBlocks;

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

        createdBlocks = new GameObject[blocks.Length];

        for (int i = 0; i < blocks.Length; i++)
        {
            grid[blocks[i].Position.x, blocks[i].Position.y] = 2;
            var vector2intToVector2 = new Vector2(blocks[i].Position.x, blocks[i].Position.y);
            createdBlocks[i] = Instantiate(blocks[i].Prefab, vector2intToVector2, Quaternion.identity);
        }
    }

    void Update()
    {
        CheckBlocksFinish();

        if (blocks[selectedBlockIndex].IsControllable == true)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveBlock(createdBlocks[selectedBlockIndex], Vector2Int.up);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveBlock(createdBlocks[selectedBlockIndex], Vector2Int.down);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveBlock(createdBlocks[selectedBlockIndex], Vector2Int.left);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveBlock(createdBlocks[selectedBlockIndex], Vector2Int.right);
            }
        }
        else if (Input.GetAxis("Mouse ScrollWheel") > 0f && selectedBlockIndex + 1 <= maxIndex)
            selectedBlockIndex++;
        else if (Input.GetAxis("Mouse ScrollWheel") < 0f && selectedBlockIndex - 1 >= minIndex)
            selectedBlockIndex--;
    }

    void CheckBlocksFinish()
    {
        for (int i = 0; i < createdBlocks.Length; i++)
        {
            if (GetBlockPosition(createdBlocks[i]) == blocks[i].FinishPosition && blocks[i].IsControllable == true)
                blocks[i].IsControllable = false;
        }
    }

    void MoveBlock(GameObject block, Vector2Int direction)
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
