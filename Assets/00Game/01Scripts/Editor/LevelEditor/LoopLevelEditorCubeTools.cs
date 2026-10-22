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
                    if (GUILayout.Button("Thêm màu cọ vào chỗ trống"))
                        WorkspaceCommand("Thêm màu cọ", AddManualCubeColor);
                    if (GUILayout.Button("Điền chỗ trống bằng màu cọ"))
                        WorkspaceCommand("Điền màu cọ vào chỗ trống", FillManualCubeColor);
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

    private void FillManualCubeColor(JObject root)
    {
        LoopLevelCubeEditing.AddColor(root, _canvasSelection, _brushColor, 64);
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
