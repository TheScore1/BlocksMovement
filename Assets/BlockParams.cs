using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockParams : MonoBehaviour
{
    [SerializeField] private Vector2Int position;
    [SerializeField] private float movementSpeed;
    [SerializeField] private Vector2 colliderSize;
    [SerializeField] private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetPosition(Vector2Int pos)
    {
        position = pos;
    }

    public void SetMovementSpeed(float speed)
    {
        movementSpeed = speed;
    }

    public void ToggleCollider()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider != null)
            collider.enabled = !collider.enabled;
        else
            Debug.Log("BoxCollider2D is null");
    }

    public void SetColliderSize(Vector2 size)
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider != null)
            collider.size = size;
        else
            Debug.Log("BoxCollider2D is null");
    }

    public void SetSpriteSkin(Sprite newSprite)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = newSprite;
        }
    }
}