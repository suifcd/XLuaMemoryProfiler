using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.IO;
using XLua;

public class LuaMemoryProfile : EditorWindow
{
    [MenuItem("XLua/MemoryProfile")]
    private static void ShowWindow()
    {
        GetWindow<LuaMemoryProfile>(false, "LuaMemoryProfile", true);
    }

    [System.Serializable]
    private class LuaMemory
    {
        public string Id;
        public string Name;
        public string Time;
        public List<LuaMemoryItem> MemoryList;
        public LuaMemoryItem SelectItem;

        public LuaMemory(int count)
        {
            Id = DateTime.UtcNow.Ticks.ToString();
            Name = string.Format("Snap{0}", count);
            Time = string.Format("{0:MM/dd-HH:mm}", DateTime.Now);
            MemoryList = new List<LuaMemoryItem>();
        }
    }

    [System.Serializable]
    private class LuaMemoryItem
    {
        public string RawText;

        public string Name;
        public long Size;
        public string Type;
        public string ID;
        public string Info;
    }

    private enum ItemSortType
    {
        Name = 1,
        Size = 2,
        Type = 3,
        ID = 4,
        Info = 5,
    }

    private enum ChangeType
    {
        Add = 1,
        Remove = 2,
        SizeAdd = 3,
        SizeSub = 4,
        None = 5,
    }

    private class ChangeItem
    {
        public ChangeType CType;
        public LuaMemoryItem Item;
        public string SizeChangeStr;
        public long SizeChange;
    }

    private int m_snapCount;
    private List<LuaMemory> m_snaps = new List<LuaMemory>();
    private LuaMemory compSnap1;
    private LuaMemory compSnap2;
    private LuaMemory m_selectSnap;
    private Vector2 m_snapScroll;
    private Vector2 m_itemScroll;

    private ItemSortType m_sortType = ItemSortType.Name;
    private bool m_sortDir = true;//排序方向
    private float itemWidth = 160;
    private bool m_showChange = false;
    private List<ChangeItem> changeList = new List<ChangeItem>();
    private int difSelectIndex = 0;

    private bool m_ignoreRegistry = true;
    private bool m_showNoneChange = false;
    private Color m_addColor = Color.red;
    private Color m_removeColor = Color.green;
    private Color m_sizeAddColor = new Color(170f / 255, 28f / 255, 28f / 255);
    private Color m_sizeSubColor = new Color(28f / 255, 170f / 255, 28f / 255);
    private Color m_noneColor = Color.grey;

    private GUIStyle m_titleStyle = new GUIStyle();
    private int m_diffSelect;

    private void OnEnable()
    {
        m_titleStyle.fontSize = 24;
        m_titleStyle.fontStyle = FontStyle.Bold;

        m_showChange = false;
        m_snapCount = 0;
    }

    private void OnGUI()
    {
        float height = Screen.height - 120;
        UpView();
        LeftView(height);
        RightView(height);
    }


    private void UpView()
    {
        if (m_showChange && compSnap1 != null && compSnap2 != null)
        {
            GUI.Label(new Rect(600, 20, 200, 20), "Diff<" + compSnap1.Name + "," + compSnap2.Name + ">", m_titleStyle);
        }
        else
        {
            if (m_selectSnap != null)
            {
                GUI.Label(new Rect(600, 20, 200, 20), m_selectSnap.Name, m_titleStyle);
            }
        }
        GUILayout.BeginArea(new Rect(30, 30, Screen.width, 40));
        UpBtnView();
        ColorView();
        GUILayout.EndArea();
    }

    private void UpBtnView()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SnapShot", GUILayout.Height(20), GUILayout.Width(80)))
        {
            SnapShot();
        }

        LuaMemory[] otherSnaps = null;
        if (m_selectSnap != null && m_snaps.Count > 1)
        {
            string[] otherSnapNames = new string[m_snaps.Count - 1];
            otherSnaps = new LuaMemory[m_snaps.Count - 1];
            int j = 0;
            for (int i = 0; i < m_snaps.Count; i++)
            {
                if (m_snaps[i].Id == m_selectSnap.Id)
                {
                    continue;
                }
                otherSnapNames[j] = m_snaps[i].Name;
                otherSnaps[j] = m_snaps[i];
                j++;
            }
            difSelectIndex = EditorGUI.Popup(new Rect(82, 2, 100, 20), difSelectIndex, otherSnapNames);
            GUILayout.Space(100);
        }
        else
        {
            difSelectIndex = EditorGUI.Popup(new Rect(82, 2, 100, 20), difSelectIndex, new string[] { });
            GUILayout.Space(100);
        }

        if (GUILayout.Button("Diff", GUILayout.Height(20), GUILayout.Width(80)))
        {
            if (m_snaps.Count < 2)
            {
                Debug.LogError("请至少获得两个快照");
                return;
            }
            LuaMemory lm = otherSnaps[difSelectIndex];

            m_showChange = true;
            int count = m_snaps.Count;
            compSnap1 = lm;
            compSnap2 = m_selectSnap;
            CalculateDiff(compSnap1, compSnap2);
        }

        if (GUILayout.Button("Clear", GUILayout.Height(20), GUILayout.Width(80)))
        {
            Clear();
        }

        if (GUILayout.Button("Load", GUILayout.Height(20), GUILayout.Width(80)))
        {
            string filePath = EditorUtility.OpenFilePanel("加载Lua内存分析文件", "", "txt");
            string json = File.ReadAllText(filePath);
            if (!string.IsNullOrEmpty(json))
            {
                LuaMemory lm = JsonUtility.FromJson<LuaMemory>(json);
                if (lm != null)
                {
                    for (int i = 0; i < m_snaps.Count; i++)
                    {
                        if (m_snaps[i].Id == lm.Id)
                        {
                            m_selectSnap = m_snaps[i];

                            Debug.LogError("已经存在该文件");
                            return;
                        }
                    }
                    m_selectSnap = lm;
                    m_snaps.Add(lm);
                    SetSortType(m_sortType);
                }
            }
        }

        if (GUILayout.Button("Save", GUILayout.Height(20), GUILayout.Width(80)))
        {
            Debug.Log("Save");
            if (m_selectSnap != null)
            {
                string filePath = EditorUtility.SaveFilePanel("保存Lua内存分析文件", "", m_selectSnap.Name, "txt");
                string json = JsonUtility.ToJson(m_selectSnap);
                File.WriteAllText(filePath, json);
            }
        }
        GUILayout.EndHorizontal();
    }

    private void ColorView()
    {
        GUILayout.BeginHorizontal();
        m_ignoreRegistry = GUILayout.Toggle(m_ignoreRegistry, "忽略REGISTRY的项", GUILayout.Width(100));
        m_showNoneChange = GUILayout.Toggle(m_showNoneChange, "显示未改变的项", GUILayout.Width(100));

        float startPos = 250;
        GUI.Label(new Rect(startPos, 24, 100, 20), "新加的项:");
        m_addColor = EditorGUI.ColorField(new Rect(startPos + 56, 22, 100, 20), m_addColor);
        GUI.Label(new Rect(startPos + 200, 24, 100, 20), "减去的项:");
        m_removeColor = EditorGUI.ColorField(new Rect(startPos + 256, 22, 100, 20), m_removeColor);
        GUI.Label(new Rect(startPos + 400, 24, 100, 20), "内存增加的项:");
        m_sizeAddColor = EditorGUI.ColorField(new Rect(startPos + 476, 22, 100, 20), m_sizeAddColor);
        GUI.Label(new Rect(startPos + 600, 24, 100, 20), "内存减少的项:");
        m_sizeSubColor = EditorGUI.ColorField(new Rect(startPos + 676, 22, 100, 20), m_sizeSubColor);
        GUI.Label(new Rect(startPos + 800, 24, 100, 20), "没有改变的项:");
        m_noneColor = EditorGUI.ColorField(new Rect(startPos + 876, 22, 100, 20), m_noneColor);
        GUILayout.EndHorizontal();
    }

    private void LeftView(float height)
    {
        GUI.Box(new Rect(30, 74, 140, height), "");
        GUILayout.BeginArea(new Rect(30, 75, 140, height));
        m_snapScroll = GUILayout.BeginScrollView(m_snapScroll);
        for (int i = 0; i < m_snaps.Count; i++)
        {
            if (!m_showChange && m_snaps[i].Id == m_selectSnap.Id)
            {
                GUI.contentColor = Color.green;
            }
            if (GUILayout.Button(m_snaps[i].Name + "(" + m_snaps[i].Time + ")"))
            {
                m_showChange = false;
                m_selectSnap = m_snaps[i];
            }
            GUI.contentColor = Color.white;
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void RightView(float height)
    {
        float width = Screen.width - 200;
        GUI.Box(new Rect(180, 74, width, height), "");
        GUILayout.BeginArea(new Rect(180, 70, width, height));

        float expandWidth = width - 4 * itemWidth;
        DrawTitle(expandWidth);
        EditorTools.DrawSeparator();
        DrawItemRow(expandWidth);

        GUILayout.EndArea();
    }

    private void DrawTitle(float width)
    {
        GUILayout.Space(10);
        GUILayout.BeginHorizontal(GUILayout.Height(10));
        ItemSortType type = (ItemSortType)m_sortType;
        if (type == ItemSortType.Name)
            GUI.color = Color.green;
        if (GUILayout.Button("Name", EditorStyles.label, GUILayout.Width(itemWidth + 40)))
        {
            SetSortType(ItemSortType.Name);
        }
        GUI.color = Color.white;

        if (type == ItemSortType.Size)
            GUI.color = Color.green;
        string sizeTitle = "Size";
        if (m_showChange)
        {
            long size = 0;
            for (int i = 0; i < changeList.Count; i++)
            {
                size += changeList[i].SizeChange;
            }
            sizeTitle += "(" + size + ")";
        }
        else
        {
            if (m_selectSnap != null)
            {
                long size = 0;
                for (int i = 0; i < m_selectSnap.MemoryList.Count; i++)
                {
                    size += m_selectSnap.MemoryList[i].Size;
                }
                sizeTitle += "(" + size + ")";
            }
        }
        if (GUILayout.Button(sizeTitle, EditorStyles.label, GUILayout.Width(itemWidth - 40)))
        {
            SetSortType(ItemSortType.Size);
        }
        GUI.color = Color.white;

        if (type == ItemSortType.Type)
            GUI.color = Color.green;
        if (GUILayout.Button("Type", EditorStyles.label, GUILayout.Width(itemWidth)))
        {
            SetSortType(ItemSortType.Type);
        }
        GUI.color = Color.white;

        if (type == ItemSortType.ID)
            GUI.color = Color.green;
        if (GUILayout.Button("ID", EditorStyles.label, GUILayout.Width(itemWidth)))
        {
            SetSortType(ItemSortType.ID);
        }
        GUI.color = Color.white;

        if (type == ItemSortType.Info)
            GUI.color = Color.green;
        if (GUILayout.Button("Info", EditorStyles.label, GUILayout.Width(width)))
        {
            SetSortType(ItemSortType.Info);
        }
        GUI.color = Color.white;

        GUILayout.EndHorizontal();
    }

    private void DrawItemRow(float width)
    {
        m_itemScroll = GUILayout.BeginScrollView(m_itemScroll);
        if (m_showChange)
        {
            List<ChangeItem> showList = null;
            if (m_showNoneChange)
            {
                showList = changeList;
            }
            else
            {
                if (m_ignoreRegistry)
                {
                    showList = changeList.FindAll(p => { return (p.CType != ChangeType.None && !p.Item.Type.Equals("REGISTRY")); });
                }
                else
                {
                    showList = changeList.FindAll(p => { return p.CType != ChangeType.None; });
                }
            }

            for (int i = 0; i < showList.Count; i++)
            {
                LuaMemoryItem item = showList[i].Item;

                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
                if (m_selectSnap != null && m_selectSnap.SelectItem == item)
                {
                    GUI.backgroundColor = new Color(120 / 255f, 146 / 255f, 190 / 255f);
                }

                Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
                if (GUI.Button(rect, "", EditorStyles.textArea) && m_selectSnap != null)
                {
                    m_selectSnap.SelectItem = item;
                }

                GUI.backgroundColor = Color.white;
                //GUILayout.BeginHorizontal();


                SetItemColor(showList[i].CType);
                GUILayout.Label(item.Name, GUILayout.Width(itemWidth + 40));
                if (showList[i].CType == ChangeType.SizeAdd || showList[i].CType == ChangeType.SizeSub)
                {
                    GUILayout.Label(showList[i].SizeChange + "(" + showList[i].SizeChangeStr + ")", GUILayout.Width(itemWidth - 40));
                }
                else
                {
                    GUILayout.Label(item.Size.ToString(), GUILayout.Width(itemWidth - 40));
                }
                GUILayout.Label(item.Type, GUILayout.Width(itemWidth));
                GUILayout.Label(item.ID, GUILayout.Width(itemWidth));
                GUILayout.Label(item.Info);
                GUILayout.EndHorizontal();
            }
        }
        else if (m_selectSnap != null)
        {
            List<LuaMemoryItem> showList = null;
            if (m_ignoreRegistry)
            {
                showList = m_selectSnap.MemoryList.FindAll(p => { return !p.Type.Equals("REGISTRY"); });
            }
            else
            {
                showList = m_selectSnap.MemoryList;
            }

            for (int i = 0; i < showList.Count; i++)
            {
                LuaMemoryItem item = showList[i];
                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
                //GUILayout.BeginHorizontal("AS TextArea", GUILayout.MinHeight(20f));
                if (m_selectSnap.SelectItem == item)
                {
                    GUI.backgroundColor = new Color(120 / 255f, 146 / 255f, 190 / 255f);
                }

                Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
                if (GUI.Button(rect, "", EditorStyles.textArea))
                {
                    m_selectSnap.SelectItem = item;
                }

                GUI.backgroundColor = Color.white;
                //GUILayout.BeginHorizontal();

                GUILayout.Label(item.Name, GUILayout.Width(itemWidth + 40));
                GUILayout.Label(item.Size.ToString(), GUILayout.Width(itemWidth - 40));
                GUILayout.Label(item.Type, GUILayout.Width(itemWidth));
                GUILayout.Label(item.ID, GUILayout.Width(itemWidth));
                GUILayout.Label(item.Info);
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();
        GUI.contentColor = Color.white;
    }

    private void SetSortType(ItemSortType type, List<LuaMemoryItem> itemList = null)
    {
        if (type == m_sortType)
        {
            m_sortDir = !m_sortDir;
        }

        m_sortType = type;

        if (itemList == null)
        {
            if (m_selectSnap == null)
                return;

            itemList = m_selectSnap.MemoryList;
        }

        int sortDir = m_sortDir ? 1 : -1;
        switch (type)
        {
            case ItemSortType.Name:
                itemList.Sort((p, q) =>
                {
                    return sortDir * string.Compare(p.Name, q.Name);
                });
                break;
            case ItemSortType.Size:
                itemList.Sort((p, q) =>
                {
                    if (p.Size > q.Size)
                    {
                        return sortDir;
                    }
                    else if (p.Size < q.Size)
                    {
                        return -sortDir;
                    }
                    else
                    {
                        return 0;
                    }
                });
                break;
            case ItemSortType.Type:
                itemList.Sort((p, q) =>
                {
                    return sortDir * string.Compare(p.Type, q.Type);
                });
                break;
            case ItemSortType.ID:
                itemList.Sort((p, q) =>
                {
                    return sortDir * string.Compare(p.ID, q.ID);
                });
                break;
            case ItemSortType.Info:
                itemList.Sort((p, q) =>
                {
                    return sortDir * string.Compare(p.Info, q.Info);
                });
                break;
            default:
                Debug.Log("未定义的类型");
                break;
        }

        SortChangeList();
    }

    private void SetItemColor(ChangeType type)
    {
        switch (type)
        {
            case ChangeType.None:
                GUI.contentColor = m_noneColor;
                break;
            case ChangeType.Add:
                GUI.contentColor = m_addColor;
                break;
            case ChangeType.Remove:
                GUI.contentColor = m_removeColor;
                break;
            case ChangeType.SizeAdd:
                GUI.contentColor = m_sizeAddColor;
                break;
            case ChangeType.SizeSub:
                GUI.contentColor = m_sizeSubColor;
                break;
            default:
                break;
        }
    }


    private void SnapShot()
    {
        if (!Application.isPlaying)
        {
            ShowNotification(new GUIContent("请先运行游戏"));
            return;
        }

        LuaEnv luaEnv = MemoryProfileTest.GetLuaEnv();
        LuaFunction lf = luaEnv.Global.GetInPath<LuaFunction>("memory.snapshot");
        if (lf == null)
        {
            Debug.LogError("不存在lua函数");
        }
        string text = lf.Func<string, string>("");


        LuaMemory memory = new LuaMemory(++m_snapCount);
        string[] lines = text.Split(new string[] { "\n" }, StringSplitOptions.None);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line))
                continue;

            string[] fields = line.Split(new char[] { ':' }, 5);
            LuaMemoryItem item = new LuaMemoryItem();
            item.RawText = line;
            item.Name = fields[0].Trim();
            item.Size = long.Parse(fields[1].Trim());
            item.Type = fields[2].Trim();
            item.ID = fields[3].Trim();
            item.Info = fields[4].Trim();

            //Debug.Log(line);
            //Debug.Log(item.Name + "  " + item.Size + "  " + item.Type + "  " + item.ID + "  " + item.Info);
            //Debug.Log(item.Name + ":" + item.Size + ":" + item.Type + ":" + item.ID + ":" + item.Info);

            memory.MemoryList.Add(item);
        }

        m_selectSnap = memory;
        m_snaps.Add(memory);
        SetSortType(m_sortType);
    }

    private void CalculateDiff(LuaMemory lm1, LuaMemory lm2)
    {
        if (lm1 == null || lm2 == null)
            return;

        CalculateDiffByID(lm1, lm2);
    }

    private void CalculateDiffByID(LuaMemory lm1, LuaMemory lm2)
    {
        ItemSortType rawSort = m_sortType;
        if (ItemSortType.ID == m_sortType)
        {
            m_sortDir = !m_sortDir;
        }
        SetSortType(ItemSortType.ID, lm1.MemoryList);

        if (ItemSortType.ID == m_sortType)
        {
            m_sortDir = !m_sortDir;
        }
        SetSortType(ItemSortType.ID, lm2.MemoryList);

        changeList.Clear();
        for (int i = 0, j = 0; i < lm1.MemoryList.Count || j < lm2.MemoryList.Count;)
        {
            if (i >= lm1.MemoryList.Count)
            {
                ChangeItem addItem = new ChangeItem();
                addItem.CType = ChangeType.Add;
                addItem.Item = lm2.MemoryList[j];
                addItem.SizeChange = lm2.MemoryList[j].Size;
                j++;
                changeList.Add(addItem);
                continue;
            }

            if (j >= lm2.MemoryList.Count)
            {
                ChangeItem removeItem = new ChangeItem();
                removeItem.CType = ChangeType.Remove;
                removeItem.Item = lm1.MemoryList[i];
                removeItem.SizeChange = -lm1.MemoryList[i].Size;
                i++;
                changeList.Add(removeItem);
                continue;
            }

            LuaMemoryItem item1 = lm1.MemoryList[i];
            LuaMemoryItem item2 = lm2.MemoryList[j];

            ChangeItem changeItem = new ChangeItem();
            changeList.Add(changeItem);
            if (string.Compare(item1.ID, item2.ID) > 0)//add
            {
                changeItem.CType = ChangeType.Add;
                changeItem.Item = item2;
                changeItem.SizeChange = item2.Size;
                j++;
            }
            else if (string.Compare(item1.ID, item2.ID) < 0)//remove
            {
                changeItem.CType = ChangeType.Remove;
                changeItem.Item = item1;
                changeItem.SizeChange = -item1.Size;
                i++;
            }
            else if (string.Compare(item1.ID, item2.ID) == 0)
            {
                if (item1.Size > item2.Size)
                {
                    changeItem.CType = ChangeType.SizeSub;
                    changeItem.SizeChangeStr = item1.Size + "->" + item2.Size;
                }
                else if (item1.Size < item2.Size)
                {
                    changeItem.CType = ChangeType.SizeAdd;
                    changeItem.SizeChangeStr = item1.Size + "->" + item2.Size;
                }
                else
                {
                    changeItem.CType = ChangeType.None;
                }
                changeItem.SizeChange = item2.Size - item1.Size;
                changeItem.Item = item1;
                i++;
                j++;
            }
        }

        m_sortType = rawSort;
        SortChangeList();
    }

    private void SortChangeList()
    {
        if (!m_showChange)
            return;

        changeList.Sort((p, q) =>
        {
            if ((int)p.CType > (int)q.CType)
            {
                return 1;
            }
            else if ((int)p.CType < (int)q.CType)
            {
                return -1;
            }
            else
            {
                int sortDir = m_sortDir ? 1 : -1;
                switch (m_sortType)
                {
                    case ItemSortType.Name:
                        return sortDir * string.Compare(p.Item.Name, q.Item.Name);
                    case ItemSortType.Size:
                        if (p.Item.Size > q.Item.Size)
                        {
                            return sortDir;
                        }
                        else if (p.Item.Size < q.Item.Size)
                        {
                            return -sortDir;
                        }
                        else
                        {
                            return 0;
                        }
                    case ItemSortType.Type:
                        return sortDir * string.Compare(p.Item.Type, q.Item.Type);
                    case ItemSortType.ID:
                        return sortDir * string.Compare(p.Item.ID, q.Item.ID);
                    case ItemSortType.Info:
                        return sortDir * string.Compare(p.Item.Info, q.Item.Info);
                    default:
                        Debug.Log("未定义的类型");
                        return 0;
                }
            }
        });
    }

    private void Clear()
    {
        m_snaps.Clear();
        compSnap1 = null;
        compSnap2 = null;
        m_selectSnap = null;
    }
}
