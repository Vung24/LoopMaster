#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using A = LoopLevelAuthoring;

public sealed partial class LoopLevelEditorWindow
{
    [SerializeField] private int _paletteCategory;
    [SerializeField] private bool _showPrefabPalette = true;
    [NonSerialized] private bool _isDraggingPrefab;
    [NonSerialized] private Vector3 _dragPreviewWorldPos;
    [NonSerialized] private GameObject _dragPreviewPrefab;
    [NonSerialized] private int _dragPreviewHitBox = -1;
    [NonSerialized] private Vector2 _dragStartMousePos;
    [NonSerialized] private GameObject _dragCandidatePrefab;
    [NonSerialized] private readonly Dictionary<string, GameObject> _cachedPrefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    private struct PaletteEntry
    {
        public string PrefabName;
        public string DisplayName;
        public string Subtitle;
        public string Icon;
        public Color AccentColor;

        public PaletteEntry(string prefabName, string displayName, string subtitle, string icon, Color accentColor)
        {
            PrefabName = prefabName;
            DisplayName = displayName;
            Subtitle = subtitle;
            Icon = icon;
            AccentColor = accentColor;
        }
    }

    private static readonly string[] PaletteCategoryNames = {
        "📦 Hộp cơ bản",
        "🔒 Khóa & Cơ chế",
        "🚪 Cổng & Bản đồ",
        "🎲 Khối & Chìa"
    };

    private static readonly PaletteEntry[][] PaletteCategories = {
        new[] {
            new PaletteEntry("Box", "Hộp thường", "4 ô (2x2)", "📦", new Color(0.2f, 0.7f, 1f)),
            new PaletteEntry("LongColorBox", "Hộp dài", "6 ô", "📏", new Color(0.7f, 0.4f, 1f)),
            new PaletteEntry("SingleTypeBox", "Hộp đơn", "Hàng đợi tối đa 64 khối", "📦", new Color(1f, 0.6f, 0.2f)),
            new PaletteEntry("OnlyEnterBox", "Hộp chỉ vào", "Một chiều vào", "⬇️", new Color(0.2f, 0.85f, 0.4f)),
            new PaletteEntry("LimitedColorBox", "Hộp hạn chế", "Màu cố định", "🎨", new Color(1f, 0.35f, 0.7f))
        },
        new[] {
            new PaletteEntry("BoxColorLock", "Khóa màu", "Mở theo màu cọ", "🎨", new Color(1f, 0.4f, 0.4f)),
            new PaletteEntry("BoxNumLock", "Khóa số", "Số lần ăn (4)", "🔢", new Color(1f, 0.75f, 0.2f)),
            new PaletteEntry("BoxAdjacentLock", "Khóa liền kề", "Mở từ hộp trước", "🔗", new Color(0.3f, 0.85f, 0.85f)),
            new PaletteEntry("KeyLockBox", "Cửa cuốn", "Gắn nhóm khóa", "🚪", new Color(0.95f, 0.8f, 0.2f))
        },
        new[] {
            new PaletteEntry("Capacity", "Capacity", "Thanh chứa ô", "📊", new Color(0.3f, 0.75f, 0.65f)),
            new PaletteEntry("MapEnter", "Cổng vào", "Bắt đầu đường", "🚪", new Color(0.2f, 0.8f, 0.35f)),
            new PaletteEntry("MapExit", "Cổng ra", "Kết thúc đường", "🏁", new Color(0.9f, 0.3f, 0.3f)),
            new PaletteEntry("MapArrow", "Mũi tên", "Chỉ hướng đi", "➡️", new Color(1f, 0.85f, 0.2f))
        },
        new[] {
            new PaletteEntry("Cube", "Khối Cube", "Tạo hộp / Gắn khối", "🎲", new Color(0.9f, 0.9f, 0.9f)),
            new PaletteEntry("Key", "Chìa khóa", "Gắn chìa vào hộp", "🔑", new Color(1f, 0.8f, 0.2f))
        }
    };

    private GameObject GetPrefab(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_cachedPrefabs.TryGetValue(name, out GameObject cached) && cached != null)
        {
            return cached;
        }
        string path = "Assets/00Game/Resources/CoreGamePrefabs/" + name + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("CoreGamePrefabs/" + name);
        }
        if (prefab != null)
        {
            _cachedPrefabs[name] = prefab;
        }
        return prefab;
    }

    public void DrawPrefabPalette(JObject root)
    {
        _showPrefabPalette = EditorGUILayout.Foldout(_showPrefabPalette, "📦 BẢNG PREFABS & ĐỐI TƯỢNG (Kéo thả lên Canvas / Scene hoặc Click để đặt)", true, EditorStyles.foldoutHeader);
        if (!_showPrefabPalette) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _paletteCategory = GUILayout.Toolbar(_paletteCategory, PaletteCategoryNames, GUILayout.Height(22));
            EditorGUILayout.Space(3);

            if (_paletteCategory < 0 || _paletteCategory >= PaletteCategories.Length)
            {
                _paletteCategory = 0;
            }

            PaletteEntry[] currentEntries = PaletteCategories[_paletteCategory];
            float cardWidth = 112f;
            float availableWidth = EditorGUIUtility.currentViewWidth > 100f ? EditorGUIUtility.currentViewWidth - 40f : 550f;
            int cardsPerRow = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (cardWidth + 6f)));

            for (int r = 0; r < currentEntries.Length; r += cardsPerRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = r; i < Mathf.Min(r + cardsPerRow, currentEntries.Length); i++)
                    {
                        DrawPaletteCard(root, currentEntries[i]);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }

    private void DrawPaletteCard(JObject root, PaletteEntry entry)
    {
        GameObject prefab = GetPrefab(entry.PrefabName);
        float width = 112f;
        float height = 54f;

        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
        Event e = Event.current;
        bool isHovered = rect.Contains(e.mousePosition);

        // Draw card background
        Color bgColor = isHovered ? new Color(0.24f, 0.30f, 0.38f, 0.95f) : new Color(0.16f, 0.19f, 0.25f, 0.95f);
        EditorGUI.DrawRect(rect, bgColor);

        // Left accent stripe
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), entry.AccentColor);

        // Border when hovered
        if (isHovered)
        {
            Handles.color = entry.AccentColor;
            Handles.DrawAAPolyLine(2f,
                new Vector3(rect.xMin, rect.yMin),
                new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin));
        }

        // Title with emoji
        GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 12f, 18f), entry.Icon + " " + entry.DisplayName, EditorStyles.boldLabel);

        // Subtitle
        GUI.Label(new Rect(rect.x + 8f, rect.y + 20f, rect.width - 12f, 16f), entry.Subtitle, EditorStyles.miniLabel);

        // Drag/Click hint
        GUI.Label(new Rect(rect.x + 8f, rect.y + 35f, rect.width - 12f, 14f), isHovered ? "👆 Kéo / Click" : "Kéo / Click", EditorStyles.centeredGreyMiniLabel);

        if (prefab == null) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (isHovered && e.button == 0)
                {
                    _dragStartMousePos = e.mousePosition;
                    _dragCandidatePrefab = prefab;
                    GUIUtility.hotControl = controlId;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId && _dragCandidatePrefab != null)
                {
                    if ((e.mousePosition - _dragStartMousePos).sqrMagnitude > 16f)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new UnityEngine.Object[] { _dragCandidatePrefab };
                        DragAndDrop.StartDrag(_dragCandidatePrefab.name);
                        GUIUtility.hotControl = 0;
                        _dragCandidatePrefab = null;
                        e.Use();
                    }
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    if (_dragCandidatePrefab != null && isHovered)
                    {
                        ApplyPrefabClick(root, _dragCandidatePrefab);
                        _dragCandidatePrefab = null;
                        e.Use();
                    }
                }
                break;
        }
    }

    private void ApplyPrefabClick(JObject root, GameObject prefab)
    {
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");

        // If Key is clicked and a box is selected, attach to that box
        if (IsKeyPrefab(prefab) && _box >= 0 && _box < boxes.Count)
        {
            ApplyPrefabDropSingle(root, prefab, Vector3.zero, _box);
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        // If Cube is clicked and a box is selected with available capacity, add cube to it
        if (IsCubePrefab(prefab) && _box >= 0 && _box < boxes.Count)
        {
            JObject selectedBox = (JObject)boxes[_box];
            if (A.Array(selectedBox, "Cubes").Count < A.CubeCapacity(selectedBox))
            {
                ApplyPrefabDropSingle(root, prefab, Vector3.zero, _box);
                Repaint();
                SceneView.RepaintAll();
                return;
            }
        }

        Vector3 newPos = Vector3.zero;
        if (IsMapPrefab(prefab, out string mapName))
        {
            Bounds bounds = LoopLevelBoardLayout.BoardBounds(layout, true);
            if (mapName == "Capacity")
            {
                newPos = new Vector3(bounds.center.x, 0.2f, bounds.max.z + 1.5f);
            }
            else
            {
                newPos = new Vector3(bounds.max.x + 1.5f, 0.2f, bounds.center.z);
            }
        }
        else if (boxes.Count > 0)
        {
            JObject lastBox = (JObject)boxes[boxes.Count - 1];
            Vector3 lastPos = A.Vector(lastBox, "Position");
            Vector3 lastRot = A.Vector(lastBox, "Rotation");
            if (Mathf.Abs(lastRot.y - 270f) < 10f || Mathf.Abs(lastRot.y - 90f) < 10f)
            {
                newPos = lastPos + new Vector3(0, 0, -1.7f);
            }
            else
            {
                newPos = lastPos + new Vector3(2.4f, 0, 0);
            }
        }

        if (_snap > 0)
        {
            newPos = Snap(newPos);
        }

        ApplyPrefabDropSingle(root, prefab, newPos, -1);
        Repaint();
        SceneView.RepaintAll();
    }

    private bool HandleSceneDragAndDrop(SceneView view, JObject root, JObject layout)
    {
        Event e = Event.current;
        if (e.type == EventType.DragExited)
        {
            _isDraggingPrefab = false;
            _dragPreviewPrefab = null;
            view.Repaint();
            return false;
        }

        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
        {
            return false;
        }

        List<GameObject> prefabs = GetDraggedPrefabs();
        if (prefabs.Count == 0)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, _placementHeight, 0));
        if (!groundPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 hitPoint = ray.GetPoint(distance);
        Vector3 worldPos = Snap(hitPoint);
        int hitBoxIndex = RaycastSceneBox(layout, ray);

        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            _dragPreviewWorldPos = worldPos;
            _dragPreviewPrefab = prefabs[0];
            _dragPreviewHitBox = hitBoxIndex;
            _isDraggingPrefab = true;

            DrawSceneDragPreview(_dragPreviewPrefab, worldPos, hitBoxIndex, layout);
            e.Use();
            view.Repaint();
            return true;
        }

        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            _isDraggingPrefab = false;
            _dragPreviewPrefab = null;

            ApplyPrefabDrops(root, prefabs, worldPos, hitBoxIndex);
            e.Use();
            Repaint();
            view.Repaint();
            return true;
        }

        return false;
    }

    private bool HandleCanvasDragAndDrop(Rect rect, JObject root, bool inside)
    {
        Event e = Event.current;
        if (e.type == EventType.DragExited)
        {
            _isDraggingPrefab = false;
            _dragPreviewPrefab = null;
            Repaint();
            return false;
        }

        if (!inside)
        {
            return false;
        }

        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
        {
            return false;
        }

        List<GameObject> prefabs = GetDraggedPrefabs();
        if (prefabs.Count == 0)
        {
            return false;
        }

        Vector3 worldPos = CanvasWorld(rect, e.mousePosition);
        if (_snap > 0)
        {
            worldPos = Snap(worldPos);
        }

        int hitBoxIndex = HitCanvasBox(rect, root, e.mousePosition, out _);

        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            _dragPreviewWorldPos = worldPos;
            _dragPreviewPrefab = prefabs[0];
            _dragPreviewHitBox = hitBoxIndex;
            _isDraggingPrefab = true;

            e.Use();
            Repaint();
            return true;
        }

        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            _isDraggingPrefab = false;
            _dragPreviewPrefab = null;

            ApplyPrefabDrops(root, prefabs, worldPos, hitBoxIndex);
            e.Use();
            Repaint();
            SceneView.RepaintAll();
            return true;
        }

        return false;
    }

    private void DrawCanvasDragPreview(Rect rect, JObject root)
    {
        if (!_isDraggingPrefab || _dragPreviewPrefab == null)
        {
            return;
        }

        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");

        if (_dragPreviewHitBox >= 0 && _dragPreviewHitBox < boxes.Count && (IsCubePrefab(_dragPreviewPrefab) || IsKeyPrefab(_dragPreviewPrefab)))
        {
            JObject targetBox = (JObject)boxes[_dragPreviewHitBox];
            Vector3 boxPos = A.Vector(targetBox, "Position");
            int boxType = A.Int(targetBox, "BoxType");
            Bounds b = LoopLevelBoardLayout.LocalBounds(boxType);
            Vector2 centerPx = CanvasProject(rect, boxPos + b.center) - rect.position;
            Vector2 sizePx = new Vector2(b.size.x, b.size.z) * _canvasScale;
            Rect targetRect = new Rect(centerPx.x - sizePx.x * 0.5f, centerPx.y - sizePx.y * 0.5f, sizePx.x, sizePx.y);

            EditorGUI.DrawRect(targetRect, new Color(0.2f, 0.9f, 0.4f, 0.35f));
            Handles.color = Color.green;
            Handles.DrawWireCube(new Vector3(centerPx.x, centerPx.y, 0), new Vector3(sizePx.x, sizePx.y, 0));
            GUI.Label(new Rect(targetRect.x, targetRect.y - 20, 220, 20), "Thêm vào Hộp #" + _dragPreviewHitBox, EditorStyles.whiteBoldLabel);
            return;
        }

        Bounds bounds = GetPrefabFootprint(_dragPreviewPrefab);
        Vector2 center = CanvasProject(rect, _dragPreviewWorldPos + bounds.center) - rect.position;
        Vector2 size = new Vector2(bounds.size.x, bounds.size.z) * _canvasScale;
        Rect previewRect = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);

        EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.75f, 1f, 0.35f));
        Handles.color = Color.cyan;
        Handles.DrawWireCube(new Vector3(center.x, center.y, 0), new Vector3(size.x, size.y, 0));
        GUI.Label(new Rect(previewRect.x, previewRect.y - 20, 220, 20), "Đặt: " + _dragPreviewPrefab.name, EditorStyles.whiteBoldLabel);
    }

    private void DrawSceneDragPreview(GameObject prefab, Vector3 worldPos, int hitBoxIndex, JObject layout)
    {
        JArray boxes = A.Array(layout, "Boxes");
        if (hitBoxIndex >= 0 && hitBoxIndex < boxes.Count && (IsCubePrefab(prefab) || IsKeyPrefab(prefab)))
        {
            JObject box = (JObject)boxes[hitBoxIndex];
            Vector3 pos = A.Vector(box, "Position");
            Vector3 rot = A.Vector(box, "Rotation");
            int type = A.Int(box, "BoxType");
            Bounds b = LoopLevelBoardLayout.LocalBounds(type);

            using (new Handles.DrawingScope(Matrix4x4.TRS(pos, Quaternion.Euler(rot), Vector3.one)))
            {
                Handles.color = Color.green;
                Handles.DrawWireCube(b.center, b.size);
            }
            Handles.Label(pos + Vector3.up * 1.5f, "Thêm " + prefab.name + " vào Hộp #" + hitBoxIndex);
            return;
        }

        Bounds footprint = GetPrefabFootprint(prefab);
        Quaternion rotation = Quaternion.Euler(0, _placementYaw, 0);
        using (new Handles.DrawingScope(Matrix4x4.TRS(worldPos, rotation, Vector3.one)))
        {
            Handles.color = Color.cyan;
            Handles.DrawWireCube(footprint.center, footprint.size);
        }
        Handles.Label(worldPos + Vector3.up * 1.2f, "Đặt: " + prefab.name);
    }

    private int RaycastSceneBox(JObject layout, Ray ray)
    {
        JArray boxes = A.Array(layout, "Boxes");
        float nearest = float.PositiveInfinity;
        int hitIndex = -1;
        for (int b = 0; b < boxes.Count; b++)
        {
            JObject box = (JObject)boxes[b];
            int type = A.Int(box, "BoxType");
            Vector3 pos = A.Vector(box, "Position");
            Vector3 rot = A.Vector(box, "Rotation");
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(rot), Vector3.one);
            Ray localRay = new Ray(matrix.inverse.MultiplyPoint3x4(ray.origin), matrix.inverse.MultiplyVector(ray.direction));
            Bounds bounds = LoopLevelBoardLayout.LocalBounds(type);
            if (bounds.IntersectRay(localRay, out float dist) && dist < nearest)
            {
                nearest = dist;
                hitIndex = b;
            }
        }
        return hitIndex;
    }

    private List<GameObject> GetDraggedPrefabs()
    {
        var list = new List<GameObject>();
        if (DragAndDrop.objectReferences == null)
        {
            return list;
        }

        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            if (DragAndDrop.objectReferences[i] is GameObject go && IsValidLevelPrefab(go))
            {
                list.Add(go);
            }
        }
        return list;
    }

    private bool IsValidLevelPrefab(GameObject go)
    {
        if (go == null) return false;
        if (IsMapPrefab(go, out _)) return true;
        if (IsCubePrefab(go) || IsKeyPrefab(go)) return true;
        if (go.GetComponent<Box>() != null) return true;
        string name = go.name.ToLowerInvariant();
        if (name.Contains("box")) return true;
        return false;
    }

    private bool IsMapPrefab(GameObject prefab, out string mapItemName)
    {
        mapItemName = null;
        if (prefab == null) return false;
        string name = prefab.name.ToLowerInvariant();
        if (name.Contains("mapenter") || name == "enter")
        {
            mapItemName = "MapEnter";
            return true;
        }
        if (name.Contains("mapexit") || name == "exit")
        {
            mapItemName = "MapExit";
            return true;
        }
        if (name.Contains("capacity"))
        {
            mapItemName = "Capacity";
            return true;
        }
        if (name.Contains("maparrow") || name.Contains("boxarrow") || name == "arrow")
        {
            mapItemName = "MapArrow";
            return true;
        }
        return false;
    }

    private bool IsCubePrefab(GameObject prefab)
    {
        if (prefab == null) return false;
        string name = prefab.name.ToLowerInvariant();
        return name == "cube" || (name.StartsWith("cube") && !name.Contains("numlock") && !name.Contains("finish") && !name.Contains("cable"));
    }

    private bool IsKeyPrefab(GameObject prefab)
    {
        if (prefab == null) return false;
        string name = prefab.name.ToLowerInvariant();
        return name == "key" || (name.StartsWith("key") && !name.Contains("lock"));
    }

    private Bounds GetPrefabFootprint(GameObject prefab)
    {
        string name = prefab.name.ToLowerInvariant();
        if (name.Contains("longcolorbox") || name.Contains("longbox"))
        {
            return LoopLevelBoardLayout.LocalBounds(1);
        }
        if (name.Contains("singletypebox") || name.Contains("singlebox"))
        {
            return LoopLevelBoardLayout.LocalBounds(2);
        }
        if (IsMapPrefab(prefab, out string mapName))
        {
            if (mapName == "Capacity") return new Bounds(Vector3.zero, new Vector3(2.4f, 0.4f, 1.2f));
            return new Bounds(Vector3.zero, new Vector3(1.5f, 0.4f, 1.5f));
        }
        if (IsCubePrefab(prefab) || IsKeyPrefab(prefab))
        {
            return new Bounds(Vector3.zero, new Vector3(0.8f, 0.8f, 0.8f));
        }
        return LoopLevelBoardLayout.LocalBounds(0);
    }

    private void ApplyPrefabDrops(JObject root, List<GameObject> prefabs, Vector3 startPos, int hitBoxIndex)
    {
        if (prefabs == null || prefabs.Count == 0) return;
        Vector3 offset = Vector3.zero;
        for (int i = 0; i < prefabs.Count; i++)
        {
            int boxHit = (i == 0) ? hitBoxIndex : -1;
            ApplyPrefabDropSingle(root, prefabs[i], startPos + offset, boxHit);
            offset += new Vector3(2.4f, 0, 0);
        }
    }

    private void ApplyPrefabDropSingle(JObject root, GameObject prefab, Vector3 worldPosition, int hitBoxIndex)
    {
        string name = prefab.name.ToLowerInvariant();
        JObject layout = A.Layout(root);
        JArray boxes = A.Array(layout, "Boxes");

        // 1. Cube dropped on an existing box
        if (IsCubePrefab(prefab) && hitBoxIndex >= 0 && hitBoxIndex < boxes.Count)
        {
            JObject targetBox = (JObject)boxes[hitBoxIndex];
            int color = _brushColor > 0 ? _brushColor : 1;
            A.PaintCube(targetBox, A.Array(targetBox, "Cubes").Count, color);
            _box = hitBoxIndex;
            SetCanvasSelection(new[] { _box });
            Commit(root, "Thêm Khối vào Hộp " + hitBoxIndex);
            _status = "Đã thêm khối màu " + color + " vào Hộp " + hitBoxIndex;
            return;
        }

        // 2. Key dropped on an existing box
        if (IsKeyPrefab(prefab) && hitBoxIndex >= 0 && hitBoxIndex < boxes.Count)
        {
            JObject targetBox = (JObject)boxes[hitBoxIndex];
            JArray cubes = A.Array(targetBox, "Cubes");
            int keyId = _keyBrush > 0 ? _keyBrush : 1;
            if (cubes.Count > 0)
            {
                JObject topCube = (JObject)cubes[cubes.Count - 1];
                topCube["GroupKeyId"] = keyId;
            }
            else
            {
                JObject newCube = A.NewCube(_brushColor > 0 ? _brushColor : 1);
                newCube["GroupKeyId"] = keyId;
                cubes.Add(newCube);
                targetBox["Cubes"] = cubes;
            }
            _box = hitBoxIndex;
            SetCanvasSelection(new[] { _box });
            Commit(root, "Gắn Chìa Khóa vào Hộp " + hitBoxIndex);
            _status = "Đã gắn chìa khóa (Nhóm " + keyId + ") vào Hộp " + hitBoxIndex;
            return;
        }

        // 3. Map Item (MapEnter, MapExit, Capacity, MapArrow)
        if (IsMapPrefab(prefab, out string mapItemName))
        {
            int mapIndex = AddMapItemDirect(layout, mapItemName, worldPosition);
            _canvasMap = mapIndex;
            _canvasSelection.Clear();
            _box = -1;
            Commit(root, "Thêm " + mapItemName);
            _status = "Đã thêm đồ họa: " + mapItemName;
            return;
        }

        // 4. Box
        JObject newBox = A.NewBox(worldPosition);
        newBox["Rotation"] = A.Vector(new Vector3(0, _placementYaw, 0));
        ConfigureBoxFromPrefab(newBox, prefab);

        if (IsCubePrefab(prefab))
        {
            int color = _brushColor > 0 ? _brushColor : 1;
            A.PaintCube(newBox, 0, color);
        }

        boxes.Add(newBox);
        layout["Boxes"] = boxes;
        _box = boxes.Count - 1;
        SetCanvasSelection(new[] { _box });

        if (_autoBind && _slots.Count > 0)
        {
            newBox["PathIndexV2"] = A.NearestSlot(newBox, _slots);
        }

        if (name.Contains("keylockbox"))
        {
            AddBoxToNewLockedGroup(layout, _box);
        }

        Commit(root, "Thêm " + prefab.name);
        _status = "Đã thêm " + prefab.name + " tại (" + worldPosition.x.ToString("F1") + ", " + worldPosition.z.ToString("F1") + ")";
    }

    private void ConfigureBoxFromPrefab(JObject box, GameObject prefab)
    {
        string name = prefab.name.ToLowerInvariant();
        if (name.Contains("longcolorbox") || name.Contains("longbox"))
        {
            box["BoxType"] = 1;
            return;
        }
        if (name.Contains("singletypebox") || name.Contains("singlebox"))
        {
            box["BoxType"] = 2;
            return;
        }
        if (name.Contains("onlyenter") || name.Contains("enteronly"))
        {
            box["BoxType"] = 0;
            box["IsEnterOnly"] = 1;
            return;
        }
        if (name.Contains("limitedcolor"))
        {
            box["BoxType"] = 0;
            box["LimitedColor"] = 1;
            return;
        }
        if (name.Contains("boxcolorlock"))
        {
            box["BoxType"] = 0;
            box["LockColor"] = _brushColor > 0 ? _brushColor : 1;
            return;
        }
        if (name.Contains("boxnumlock"))
        {
            box["BoxType"] = 0;
            box["NumLock"] = 4;
            return;
        }
        if (name.Contains("boxadjacentlock"))
        {
            box["BoxType"] = 0;
            box["IsAdjacentLocked"] = 1;
            return;
        }
        box["BoxType"] = 0;
    }

    private void AddBoxToNewLockedGroup(JObject layout, int boxIndex)
    {
        JArray groups = A.Array(layout, "Groups");
        int nextId = 1;
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] is JObject g)
            {
                nextId = Math.Max(nextId, A.Int(g, "Id") + 1);
            }
        }
        groups.Add(new JObject
        {
            ["Id"] = nextId,
            ["BoxIndexes"] = new JArray(boxIndex),
            ["IsLocked"] = 1
        });
        layout["Groups"] = groups;
    }

    private int AddMapItemDirect(JObject layout, string mapItemName, Vector3 position)
    {
        JArray map = A.Array(layout, "MapData");
        JObject item = new JObject
        {
            ["Name"] = mapItemName,
            ["Position"] = A.Vector(position),
            ["Rotation"] = A.Vector(new Vector3(0, _placementYaw, 0)),
            ["Scale"] = A.Vector(Vector3.one)
        };
        map.Add(item);
        layout["MapData"] = map;
        return map.Count - 1;
    }
}
#endif
