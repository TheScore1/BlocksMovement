using UnityEngine;
using TMPro;

public class DynamicTextManager : MonoBehaviour
{
    private GameObject textObject;
    public TextMeshPro textMeshPro;

    public Font font;

    void Awake()
    {
        textObject = new GameObject("Text");
        textObject.transform.position = new Vector3(0, 0, 0);

        textMeshPro = textObject.AddComponent<TextMeshPro>();

        textMeshPro.text = "This is <color=red>red</color> and this is <color=blue>blue</color>!";
        textMeshPro.fontSize = 90;
        textMeshPro.fontStyle = FontStyles.Bold;
        textMeshPro.alignment = TextAlignmentOptions.Center;

        textMeshPro.textWrappingMode = TextWrappingModes.NoWrap;
        textMeshPro.sortingOrder = 10;

        textMeshPro.characterSpacing = -4;

        textMeshPro.transform.localScale = new Vector3(1f, 1.3f, 1f);

        Material newMaterial = new Material(textMeshPro.fontMaterial);
        newMaterial.SetInt(ShaderUtilities.ID_OutlineMode, 1);
        textMeshPro.fontMaterial = newMaterial;

        textMeshPro.fontMaterial.EnableKeyword("OUTLINE_ON");
        textMeshPro.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.4f); // ������ �������
        textMeshPro.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(88f/255f, 60f/255f, 29f/225f, 1)); // ���� �������
        textMeshPro.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f); // ����������� ������� ��������
    }

    public void ChangeTextForLevel(string text)
    {
        if (text != null)
            textMeshPro.text = "LEVEL <color=#D9D9D9>" + text + "</color>";
    }

    public void ChangeTextForMoves(string text)
    {
        if (text != null)
            textMeshPro.text = "MOVE: <color=#EEC20C>" + text + "</color>";
    }

    public void ChangeTextPosition(Vector3 pos)
    {
        textMeshPro.transform.position = pos;
    }

    public float CalculateAdaptiveFontSize(float maxWidth, float maxHeight)
    {
        float fontSize = 10f; // ��������� ������ ������
        textMeshPro.enableAutoSizing = false; // ��������� �������������� ��������� �������
        textMeshPro.fontSize = fontSize;

        // ���������� ����������� ������ ������, ���� ����� ���������� � �������
        while (true)
        {
            textMeshPro.fontSize = fontSize;

            // ��������� ������� ������
            textMeshPro.ForceMeshUpdate();
            var textBounds = textMeshPro.GetRenderedValues(false);

            // ���������, ���������� �� ����� � ��������� �������
            if (textBounds.x > maxWidth || textBounds.y > maxHeight)
            {
                fontSize -= 0.5f; // ���������, ���� ������� �� �������
                break;
            }

            fontSize += 0.5f; // �����������, ���� ����������
        }
        return fontSize;
    }


    void Update()
    {
    }
}
