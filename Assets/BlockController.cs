using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class BlockSettings
{
    [HideInInspector] public bool isSelected;
    [HideInInspector] public bool isControllable;

    public GameObject prefab;
    public Sprite sprite;
    public Vector2Int position;
    public Vector2Int finishPosition;
}

public class BlockController : MonoBehaviour
{
    [Header("General settings")]
    [SerializeField] private Vector2Int LevelSize;
    [SerializeField] private List<BlockSettings> blockSettingsList = new List<BlockSettings>();
    [SerializeField] Transform blocksParent;
    [SerializeField] private float movementSpeed = 5f;

    [Header("Controls")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private KeyCode moveUpKey = KeyCode.W;
    [SerializeField] private KeyCode moveDownKey = KeyCode.S;

    private List<GameObject> spawnedBlocks = new List<GameObject>();
    private List<GameObject> panelObjects = new List<GameObject>();

    void Start()
    {
        blockSettingsList[0].isSelected = true;
        blockSettingsList[0].isControllable = true;
        GenerateBlocks();
    }

    private void GenerateBlocks()
    {
        foreach (var block in spawnedBlocks)
            Destroy(block);
        spawnedBlocks.Clear();

        // level
        for (int i = 0; i < blockSettingsList.Count; i++)
        {
            if (i < blockSettingsList.Count && blockSettingsList[i].prefab != null)
            {
                GameObject newBlock = Instantiate(blockSettingsList[i].prefab, blocksParent);

                if (blockSettingsList[i].position != null &&
                    blockSettingsList[i].position.x < LevelSize.x && blockSettingsList[i].position.x >= 0 &&
                    blockSettingsList[i].position.y < LevelSize.y && blockSettingsList[i].position.y >= 0)
                {
                    Vector2 position = new Vector2(blockSettingsList[i].position.x, blockSettingsList[i].position.y);
                    newBlock.transform.position = position;
                }
                else
                    Debug.Log($"Something wrong with position of {i} element");

                BlockParams blockParams = newBlock.GetComponent<BlockParams>();

                blockParams.SetMovementSpeed(movementSpeed);
                blockParams.SetColliderSize(new Vector2(1, 1));
                blockParams.SetPosition(blockSettingsList[i].position);
                //blockParams.SetSpriteSkin(null);

                spawnedBlocks.Add(newBlock);
            }
        }

        // blocks panel
        for (int i = 0; i < spawnedBlocks.Count; i++)
        {
            GameObject panelObject = spawnedBlocks[i];
            BlockParams panelObjectParams = panelObject.GetComponent<BlockParams>();

            panelObjectParams.transform.position = new Vector2(i, -2);
            panelObjects.Add(panelObject);
        }
    }

    void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector2 direction = Vector2.zero;

        if (Input.GetKey(moveLeftKey)) direction = Vector2.left;
        if (Input.GetKey(moveRightKey)) direction = Vector2.right;
        if (Input.GetKey(moveUpKey)) direction = Vector2.up;
        if (Input.GetKey(moveDownKey)) direction = Vector2.down;

        foreach (GameObject block in spawnedBlocks)
        {
            if (block != null)
            {
                block.transform.Translate(direction * movementSpeed * Time.deltaTime);
            }
        }
    }
}