using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AdditionalTiles))]
public class AdditionalTilesDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Установка отступов
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 1;

        // Начало отрисовки
        float singleLineHeight = EditorGUIUtility.singleLineHeight + 2;
        Rect fieldRect = new Rect(position.x, position.y, position.width, singleLineHeight);

        // Получаем все свойства
        SerializedProperty tileTypeProp = property.FindPropertyRelative("TileType");
        SerializedProperty positionProp = property.FindPropertyRelative("Position");
        SerializedProperty position2Prop = property.FindPropertyRelative("Position2");
        SerializedProperty isActiveProp = property.FindPropertyRelative("IsActive");
        SerializedProperty visibilityFreqProp = property.FindPropertyRelative("VisibilityFreq");
        SerializedProperty isSymmetryMovingProp = property.FindPropertyRelative("IsSymmetryMoving");

        // Отрисовка всех полей
        EditorGUI.PropertyField(fieldRect, tileTypeProp);
        fieldRect.y += singleLineHeight;
        EditorGUI.PropertyField(fieldRect, positionProp);
        fieldRect.y += singleLineHeight;

        // Отображаем Position2 только если TileType == Teleport
        if ((TileTypes)tileTypeProp.enumValueIndex == TileTypes.Teleport)
        {
            EditorGUI.PropertyField(fieldRect, position2Prop);
            fieldRect.y += singleLineHeight;
        }

        EditorGUI.PropertyField(fieldRect, isActiveProp);
        fieldRect.y += singleLineHeight;

        if ((TileTypes)tileTypeProp.enumValueIndex == TileTypes.Invisibility)
        {
            EditorGUI.PropertyField(fieldRect, visibilityFreqProp);
            fieldRect.y += singleLineHeight;
        }

        if ((TileTypes)tileTypeProp.enumValueIndex == TileTypes.Invisibility)
        {
            EditorGUI.PropertyField(fieldRect, isSymmetryMovingProp);
            fieldRect.y += singleLineHeight;
        }

        // Восстанавливаем отступы
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Определяем высоту всех полей
        float singleLineHeight = EditorGUIUtility.singleLineHeight + 2;
        SerializedProperty tileTypeProp = property.FindPropertyRelative("TileType");

        // Высота базовых полей
        float totalHeight = singleLineHeight * 7;

        // Добавляем место для Position2, если TileType == Teleport
        if ((TileTypes)tileTypeProp.enumValueIndex == TileTypes.Teleport)
        {
            totalHeight += singleLineHeight;
        }

        return totalHeight;
    }
}
