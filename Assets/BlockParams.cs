using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BlockParamsTest : MonoBehaviour // ���� �� ��������, ��� �� � level editor. ���� ���������� ������������ ���� �����, �������� ������ ������ ����� � ����������
{
    public GameObject Prefab;
    public Vector2Int Position;
    public Vector2Int FinishPosition;
    public Sprite sprite;
    public Sprite finishSprite;
    public float moveSpeed = 5.0f;

    public bool CollideWithBlocks = true;
    public bool CollideWithWalls = true;
    [HideInInspector] public bool IsControllable;

    void Awake()
    {

    }
}