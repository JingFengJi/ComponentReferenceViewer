using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;

namespace ComponentReferenceViewer
{
    public static class TypeCache
    {
        private static List<Type> m_CacheTypes;
        
        static TypeCache()
        {
            m_CacheTypes = new List<Type>();
        }

        public static void LoadTypeCache()
        {
            m_CacheTypes.Clear();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types = assemblies[i].GetTypes();
                for (int j = 0; j < types.Length; j++)
                {
                    if(types[j].IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        m_CacheTypes.Add(types[j]);
                    }
                }
            }
        }

        public static void SearchType(string searchTypeName, List<Type> result)
        {
            result.Clear();
            for (int i = 0; i < m_CacheTypes.Count; i++)
            {
                Type type = m_CacheTypes[i];
                if (type.Name.ToLower() == searchTypeName || type.FullName.ToLower() == searchTypeName)
                {
                    result.Add(type);
                }
            }
        }
    }
    
    public class ComponentReferenceInfo
    {
        public string PrefabPath;
        public List<string> ComponentPaths = new List<string>();
    }

    public class ComponentReferenceViewerEditorWindow : EditorWindow
    {
        private bool m_SearchDirty;
        private string searchString;
        private List<Type> m_SearchTypeList;
        SearchField m_SearchField;
        private TreeViewState m_TreeState;
        private ComponentReferenceViewerTreeView m_TreeView;
        private GUIStyle m_ToolbarGUIStyle;
        private bool m_InitializedGUIStyle = false;
        private bool m_NeedUpdateAssetTree = false;
        private string m_CurrentSearchPath = "";

        [MenuItem("Window/Component Reference Viewer")]
        public static void ShowWindow()
        {
            ComponentReferenceViewerEditorWindow window = GetWindow<ComponentReferenceViewerEditorWindow>();
            window.titleContent = new GUIContent("Component Reference Viewer");
            window.Show();
        }

        private void OnEnable()
        {
            m_NeedUpdateAssetTree = true;
            m_SearchField = new SearchField ();
            m_SearchDirty = false;
            m_SearchTypeList = new List<Type>();
            TypeCache.LoadTypeCache();
        }

        //初始化GUIStyle
        private void InitGUIStyleIfNeeded()
        {
            if (!m_InitializedGUIStyle)
            {
                m_ToolbarGUIStyle = new GUIStyle("Toolbar");
                m_InitializedGUIStyle = true;
            }
        }
        
        private void UpdateAssetTree()
        {
            if (string.IsNullOrEmpty(m_CurrentSearchPath) || string.IsNullOrEmpty(searchString) || m_SearchDirty == false)
            {
                return;
            }

            if (m_NeedUpdateAssetTree)
            {
                searchString = searchString.ToLower();
                TypeCache.SearchType(searchString.ToLower(), m_SearchTypeList);
                if (m_SearchTypeList.Count == 0)
                {
                    return;
                }
                
                if (m_TreeView == null)
                {
                    if (m_TreeState == null)
                        m_TreeState = new TreeViewState();
                    var headerState = ComponentReferenceViewerTreeView.CreateDefaultMultiColumnHeaderState(position.width);
                    var multiColumnHeader = new MultiColumnHeader(headerState);
                    m_TreeView = new ComponentReferenceViewerTreeView(m_TreeState, multiColumnHeader);
                }
                
                m_TreeView.FindComponentReferenceInfo(m_CurrentSearchPath, m_SearchTypeList);
                var root = new ComponentReferenceViewerTreeViewItem {id = 0, depth = -1, displayName = "Root"};

                foreach (var info in m_TreeView.ComponentReferenceInfos)
                {
                    var item = new ComponentReferenceViewerTreeViewItem
                    {
                        id = info.GetHashCode(), depth = 0, displayName = info.PrefabPath, Path = info.PrefabPath,
                        ReferenceNum = info.ComponentPaths.Count
                    };
                    
                    for (int i = 0; i < info.ComponentPaths.Count; i++)
                    {
                        var child = new ComponentReferenceViewerTreeViewItem
                        {
                            id = (info.PrefabPath + info.ComponentPaths[i]).GetHashCode(), depth = 1,
                            displayName = info.ComponentPaths[i], Path = info.ComponentPaths[i], IsComponent = true
                        };
                        item.AddChild(child);
                    }

                    root.AddChild(item);
                }

                m_TreeView.Root = root;
                m_TreeView.CollapseAll();
                m_TreeView.Reload();
                m_NeedUpdateAssetTree = false;
                m_SearchDirty = false;
            }
        }

        private void OnGUI()
        {
            InitGUIStyleIfNeeded();
            DrawOptionBar();
            UpdateAssetTree();
            if (m_TreeView != null)
            {
                m_TreeView.OnGUI(new Rect(0, m_ToolbarGUIStyle.fixedHeight, position.width, position.height - m_ToolbarGUIStyle.fixedHeight));
            }
        }

        private void DrawOptionBar()
        {
            EditorGUILayout.BeginHorizontal(m_ToolbarGUIStyle);
            GUILayout.Label($"Search Path: {m_CurrentSearchPath}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Select Search Folder", GUILayout.Width(160)))
            {
                string folderPath = EditorUtility.OpenFolderPanel("Select Folder", "", "");
                if (!string.IsNullOrEmpty(folderPath))
                {
                    m_CurrentSearchPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
                    m_NeedUpdateAssetTree = true;
                }
            }
            Rect rect = GUILayoutUtility.GetRect (0, 120, 0, m_ToolbarGUIStyle.fixedHeight);
            Rect toolbarRect = new Rect (rect.x, rect.y, rect.width, m_ToolbarGUIStyle.fixedHeight);
            searchString = m_SearchField.OnGUI(toolbarRect, searchString);
            if (GUILayout.Button("Refresh"))
            {
                m_SearchDirty = true;
                m_NeedUpdateAssetTree = true;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    public class ComponentReferenceViewerTreeViewItem : TreeViewItem
    {
        public string Path;
        public int ReferenceNum;
        public bool IsComponent;
    }

    public class ComponentReferenceViewerTreeView : TreeView
    {
        //图标宽度
        const float kIconWidth = 18f;

        //列表高度
        const float kRowHeights = 20f;

        public ComponentReferenceViewerTreeViewItem Root;

        private GUIStyle m_StateGUIStyle;

        public List<ComponentReferenceInfo> ComponentReferenceInfos = new List<ComponentReferenceInfo>();
        
        private enum MyColumns
        {
            Path,
            ReferenceNum,
            Select
        }
        
        private enum SortOption
        {
            Path,
            ReferenceNum,
            Select
        }

        private SortOption[] m_SortOptions =
        {
            SortOption.Path,
            SortOption.ReferenceNum,
            SortOption.Select,
        };

        public ComponentReferenceViewerTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader) : base(state, multicolumnHeader)
        {
            m_StateGUIStyle = new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter};
            rowHeight = kRowHeights;
            columnIndexForTreeFoldouts = 0;
            showAlternatingRowBackgrounds = true;
            showBorder = false;
            customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f;
            extraSpaceBeforeIconAndLabel = kIconWidth;
            multiColumnHeader.sortingChanged += OnSortingChanged;
        }
        
        private void OnSortingChanged(MultiColumnHeader multicolumnheader)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        private void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count() <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
            {
                return;
            }
            
            SortByMultipleColumns();
            TreeToList(root, rows);
            Repaint();
            CollapseAll();
        }

        private void SortByMultipleColumns()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
            {
                return;
            }

            var myTypes = rootItem.children.Cast<ComponentReferenceViewerTreeViewItem>();
            var orderedQuery = InitialOrder(myTypes, sortedColumns);
            if (orderedQuery == null)
            {
                return;
            }

            for (int i = 0; i < sortedColumns.Length; i++)
            {
                SortOption sortOption = m_SortOptions[sortedColumns[i]];
                bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

                switch (sortOption)
                {
                    case SortOption.ReferenceNum:
                        orderedQuery = orderedQuery.ThenBy(l => l.ReferenceNum, ascending);
                        break;
                }
            }

            rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
        }

        private IOrderedEnumerable<ComponentReferenceViewerTreeViewItem> InitialOrder(IEnumerable<ComponentReferenceViewerTreeViewItem> myTypes, int[] history)
        {
            SortOption sortOption = m_SortOptions[history[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
            switch (sortOption)
            {
                case SortOption.ReferenceNum:
                    return myTypes.Order(l => l.ReferenceNum, ascending);
            }
            
            return null;
        }

        private static void TreeToList(TreeViewItem root, IList<TreeViewItem> result)
        {
            if (root == null)
                throw new NullReferenceException("root");
            if (result == null)
                throw new NullReferenceException("result");

            result.Clear();

            if (root.children == null)
                return;

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
            for (int i = root.children.Count - 1; i >= 0; i--)
                stack.Push(root.children[i]);

            while (stack.Count > 0)
            {
                TreeViewItem current = stack.Pop();
                result.Add(current);
                if (current.hasChildren && current.children[0] != null)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(current.children[i]);
                    }
                }
            }
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Path"),
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = treeViewWidth - 120 * 2,
                    minWidth = 500,
                    autoResize = false,
                    allowToggleVisibility = false,
                    canSort = false,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Reference Num"),
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 120,
                    minWidth = 120,
                    maxWidth = 120,
                    autoResize = false,
                    allowToggleVisibility = false,
                    canSort = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Select"),
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 120,
                    minWidth = 120,
                    maxWidth = 120,
                    autoResize = false,
                    allowToggleVisibility = false,
                    canSort = false
                },
            };
            var state = new MultiColumnHeaderState(columns);
            return state;
        }

        protected override TreeViewItem BuildRoot()
        {
            return Root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (ComponentReferenceViewerTreeViewItem) args.item;
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (MyColumns) args.GetColumn(i), ref args);
            }
        }

        //响应右击事件
        protected override void ContextClickedItem(int id)
        {
            SetExpanded(id, !IsExpanded(id));
        }

        private static void DiscardPrefabStageChanges()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                MethodInfo methodInfo = typeof(PrefabStage).GetMethod("DiscardChanges", BindingFlags.Instance | BindingFlags.NonPublic);
                if (methodInfo != null)
                {
                    methodInfo.Invoke(PrefabStageUtility.GetCurrentPrefabStage(), null);
                }
            }
        }
        
        protected override void DoubleClickedItem(int id)
        {
            SelectComponentReferenceTreeViewItem(id);
        }
        
        private void CellGUI(Rect cellRect, ComponentReferenceViewerTreeViewItem item, MyColumns column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);
            switch (column)
            {
                case MyColumns.Path:
                {
                    args.rowRect = cellRect;
                    base.RowGUI(args);
                }
                    break;
                case MyColumns.ReferenceNum:
                {
                    if (!item.IsComponent)
                    {
                        GUI.Label(cellRect, item.ReferenceNum.ToString(), m_StateGUIStyle);
                    }
                }
                    break;
                case MyColumns.Select:
                {
                    if (GUI.Button(cellRect, "Select"))
                    {
                        SelectComponentReferenceTreeViewItem(item.id);
                    }
                }
                    break;
            }
        }
        
        private void SelectComponentReferenceTreeViewItem(int id)
        {
            var item = (ComponentReferenceViewerTreeViewItem) FindItem(id, rootItem);
            if (item == null)
                return;
            var assetObject = AssetDatabase.LoadAssetAtPath<GameObject>(item.Path);
            if (assetObject != null)
            {
                DiscardPrefabStageChanges();
                PrefabStageUtility.OpenPrefab(item.Path);
                SetExpanded(id, !IsExpanded(id));
            }
            else
            {
                if (item.parent != null && item.parent is ComponentReferenceViewerTreeViewItem)
                {
                    ComponentReferenceViewerTreeViewItem parent = item.parent as ComponentReferenceViewerTreeViewItem;
                    assetObject = AssetDatabase.LoadAssetAtPath<GameObject>(parent.Path);
                    if (assetObject != null)
                    {
                        DiscardPrefabStageChanges();
                        PrefabStageUtility.OpenPrefab(parent.Path);
                        GameObject root = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;
                        Selection.activeGameObject = root.transform.Find(item.Path).gameObject;
                    }
                }
            }
        }

        public void FindComponentReferenceInfo(string folderPath, List<Type> searchComponentTypeList)
        {
            ComponentReferenceInfos.Clear();
            string[] guidArray = AssetDatabase.FindAssets("t:Prefab", new[] {folderPath});
            foreach (var guid in guidArray)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    List<Component> components = new List<Component>();
                    for (int i = 0; i < searchComponentTypeList.Count; i++)
                    {
                        var maskComponents = prefab.GetComponentsInChildren(searchComponentTypeList[i], true);
                        components.AddRange(maskComponents);
                    }
                    if (components.Count > 0)
                    {
                        ComponentReferenceInfo info = new ComponentReferenceInfo();
                        info.PrefabPath = prefabPath;
                        foreach (var com in components)
                        {
                            info.ComponentPaths.Add(GetGameObjectPathInPrefab(com.gameObject, prefab));
                        }
                        ComponentReferenceInfos.Add(info);
                    }
                }
            }
        }

        private string GetGameObjectPathInPrefab(GameObject obj, GameObject root)
        {
            string path = obj.name;
            while (obj.transform.parent != null && obj.transform.parent.gameObject != root)
            {
                obj = obj.transform.parent.gameObject;
                path = obj.name + "/" + path;
            }

            return path;
        }
    }
}