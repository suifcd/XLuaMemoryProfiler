using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using XLua;
using System.Security.Cryptography;
using System.Text;

public class EditorTools
{
    static public Texture2D blankTexture
    {
        get
        {
            return EditorGUIUtility.whiteTexture;
        }
    }

    
    static public void RegisterUndo(string name, params UnityEngine.Object[] objects)
    {
        if (objects != null && objects.Length > 0)
        {
            UnityEditor.Undo.RecordObjects(objects, name);

            foreach (UnityEngine.Object obj in objects)
            {
                if (obj == null) continue;
                EditorUtility.SetDirty(obj);
            }
        }
    }

    static public void DrawSeparator(float width = 0)
    {
        GUILayout.Space(12f);
        if(width == 0)
        {
            width = Screen.width;
        }

        if (Event.current.type == EventType.Repaint)
        {
            Texture2D tex = blankTexture;
            Rect rect = GUILayoutUtility.GetLastRect();
            GUI.color = new Color(0f, 0f, 0f, 0.25f);
            GUI.DrawTexture(new Rect(0f, rect.yMin + 6f, width, 4f), tex);
            GUI.DrawTexture(new Rect(0f, rect.yMin + 6f, width, 1f), tex);
            GUI.DrawTexture(new Rect(0f, rect.yMin + 9f, width, 1f), tex);
            GUI.color = Color.white;
        }
    }

    static public void DrawOutline(Rect rect, Color color)
    {
        if (Event.current.type == EventType.Repaint)
        {
            Texture2D tex = blankTexture;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), tex);
            GUI.DrawTexture(new Rect(rect.xMax, rect.yMin, 1f, rect.height), tex);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), tex);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax, rect.width, 1f), tex);
            GUI.color = Color.white;
        }
    }
    
}