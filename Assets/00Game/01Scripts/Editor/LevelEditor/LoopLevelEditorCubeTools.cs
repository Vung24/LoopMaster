#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class LoopLevelEditorWindow
{
    [SerializeField] private int _manualCubeCount = 1;
    [SerializeField] private int _manualMixSwaps = 3;
    [SerializeField] private List<int> _manualPinnedBoxes = new List<int>();

    private void DrawManualCubeTools()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("Thêm màu & trộn cube thủ công", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Chọn hộp bằng công cụ Hộp & Cổng (W), Shift+Click để chọn nhiều, rồi quay lại Tô màu (E).", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _manualCubeCount = EditorGUILayout.IntSlider("Số cube thêm / hộp", _manualCubeCount, 1, 4);
                using (new EditorGUI.DisabledScope(_canvasSelection.Count == 0))
                {
                    if (GUILayout.Button("Thêm nhanh màu mới (A)"))
                        TryQuickAddUnusedColor();
                    if (GUILayout.Button("Thêm màu cọ vào chỗ trống"))
                        WorkspaceCommand("Thêm màu cọ", AddManualCubeColor);
                    if (GUILayout.Button("Điền màu thiếu (đủ bộ 4)"))
                        WorkspaceCommand("Điền màu thiếu theo bộ 4", FillMissingCubeColors);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _manualMixSwaps = EditorGUILayout.IntSlider("Số lượt đổi chéo", _manualMixSwaps, 1, 20);
                _variantSeed = EditorGUILayout.IntField("Seed", _variantSeed, GUILayout.MaxWidth(220));
                using (new EditorGUI.DisabledScope(_canvasSelection.Count < 2))
                {
                    if (GUILayout.Button("Xem trước trộn hộp đã chọn"))
                        PreviewWorkspaceCommand("Trộn cube giữa các hộp đã chọn", MixManualCubes);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ghim hộp đã chọn khi trộn"))
                {
                    foreach (int index in _canvasSelection)
                        if (!_manualPinnedBoxes.Contains(index)) _manualPinnedBoxes.Add(index);
                }
                if (GUILayout.Button("Bỏ ghim hộp đã chọn"))
                    foreach (int index in _canvasSelection) _manualPinnedBoxes.Remove(index);
                if (GUILayout.Button("Bỏ tất cả ghim")) ClearManualCubePins();
            }
            if (_manualPinnedBoxes.Count > 0)
                EditorGUILayout.LabelField("Hộp ghim khi trộn: " + string.Join(", ", _manualPinnedBoxes) + ". Ghim được xóa khi đổi màn, Undo/Redo hoặc thêm/xóa hộp.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Trộn giữ số cube/hộp, tổng từng màu và hộp trống; bỏ qua hộp có cơ chế đặc biệt. Thêm màu có thể làm lệch bộ 4. Dùng kiểm tra và chơi thử để đánh giá màn.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Chuyển / đổi cube: mở bảng chi tiết"))
            {
                _showBoardTable = true;
                _canvasTool = 0;
            }
        }
    }

    private void AddManualCubeColor(JObject root)
    {
        LoopLevelCubeEditing.AddColor(root, _canvasSelection, _brushColor, _manualCubeCount);
    }

    private void DrawWorkspaceCubeSecrets(JObject box)
    {
        GUILayout.Label("Hidden / Secret — khối ẩn", EditorStyles.boldLabel);
        JArray cubes = LoopLevelAuthoring.Array(box, "Cubes");
        if (cubes.Count == 0)
        {
            EditorGUILayout.LabelField("Hộp chưa có khối. Thêm màu trước khi đặt Hidden.", EditorStyles.wordWrappedMiniLabel);
            return;
        }
        for (int index = 0; index < cubes.Count; index++)
        {
            JObject cube = (JObject)cubes[index];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Khối " + (index + 1), GUILayout.Width(60));
                Color color = LoopLevelAuthoring.ColorFor(LoopLevelAuthoring.Int(cube, "Type"));
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18)), color);
                bool hidden = LoopLevelAuthoring.Int(cube, "IsSecret") > 0;
                if (index == cubes.Count - 1)
                {
                    EditorGUILayout.LabelField("Đỉnh — không Hidden", EditorStyles.miniLabel);
                    if (hidden && GUILayout.Button("Bỏ Hidden")) cube["IsSecret"] = 0;
                    continue;
                }
                bool nextHidden = EditorGUILayout.ToggleLeft("Hidden", hidden);
                if (nextHidden != hidden) cube["IsSecret"] = nextHidden ? 1 : 0;
            }
        }
        EditorGUILayout.LabelField("Khối 1 ở đáy; khối cuối ở đỉnh. Trên bàn, dấu ? là khối Hidden.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4);
    }

    private static int DrawCubeIndexPopup(string label, JArray cubes, int selected)
    {
        var options = new string[cubes.Count];
        for (int index = 0; index < cubes.Count; index++)
            options[index] = "Khối " + (index + 1);
        return EditorGUILayout.Popup(label, Mathf.Clamp(selected, 0, cubes.Count - 1), options);
    }

    private void FillMissingCubeColors(JObject root)
    {
        LoopLevelCubeEditing.FillMissingColors(root, _canvasSelection, _brushColor);
    }

    private void MixManualCubes(JObject root)
    {
        int unpinned = 0;
        foreach (int index in _canvasSelection)
            if (!_manualPinnedBoxes.Contains(index)) unpinned++;
        LoopLevelCubeEditing.Mix(root, _canvasSelection, _variantSeed, _manualMixSwaps, _manualPinnedBoxes);
        _proposalReport = "Trộn trong " + unpinned + " hộp chưa ghim; hộp trống/có cơ chế được giữ nguyên. Seed: " + _variantSeed + ".";
    }

    private void PreviewColorBalance(JObject root)
    {
        LoopLevelCommands.BalanceCubesToSetsOfFour(root, out int modified, out string report);
        _proposalReport = "Thay đổi " + modified + " cube.\n" + report;
    }

    private void ClearManualCubePins()
    {
        _manualPinnedBoxes.Clear();
    }

    public bool TryQuickAddUnusedColor()
    {
        if (_document == null || EditorApplication.isPlaying || HasRawChanges) return false;

        List<int> selection = new List<int>();
        if (_canvasSelection != null && _canvasSelection.Count > 0)
            selection.AddRange(_canvasSelection);
        else if (_box >= 0)
            selection.Add(_box);

        if (selection.Count == 0)
        {
            _status = "Chưa chọn hộp nào để thêm màu. Hãy click chọn 1 hộp trước.";
            return false;
        }

        try
        {
            JObject root = Root();
            JObject next = (JObject)root.DeepClone();
            int added = LoopLevelCubeEditing.QuickAddUnusedColor(next, selection, out int lastColor);
            if (added > 0)
            {
                if (lastColor >= 1 && lastColor <= 14) _brushColor = lastColor;
                string actionName = selection.Count == 1
                    ? "Thêm nhanh màu " + lastColor + " vào Hộp " + selection[0]
                    : "Thêm nhanh màu mới vào " + selection.Count + " hộp";
                Undo.IncrementCurrentGroup();
                Commit(next, actionName);
                Undo.IncrementCurrentGroup();
                _canvasSelection = new List<int>(LoopLevelCommands.Selection(next, _canvasSelection));
                _status = "Đã thêm nhanh màu " + lastColor + " (" + added + " khối) vào " +
                    (selection.Count == 1 ? "Hộp " + selection[0] : selection.Count + " hộp") + ".";
                return true;
            }
        }
        catch (Exception ex)
        {
            _status = ex.Message;
        }

        return false;
    }

    private void CompleteTableCubeDrop(JObject layout, int targetBox, int targetCube)
    {
        try
        {
            LoopLevelCubeEditing.TransferCube(layout, _tableDragBox, _tableDragCube, targetBox, targetCube);
            _box = targetBox;
        }
        catch (FormatException exception)
        {
            _status = exception.Message;
        }
        finally
        {
            _tableDragBox = _tableDragCube = -1;
            if (GUIUtility.hotControl == _tableDragControl) GUIUtility.hotControl = 0;
        }
    }
}
#endif
