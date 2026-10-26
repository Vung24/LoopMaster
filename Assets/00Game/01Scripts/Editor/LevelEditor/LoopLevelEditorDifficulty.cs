#if UNITY_EDITOR
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using A = LoopLevelAuthoring;

public sealed partial class LoopLevelEditorWindow
{
    [SerializeField] private bool _showDifficultyTools = true, _difficultySelectedOnly;
    [SerializeField] private int _difficultyTarget = 1, _difficultyReservedBoxes;
    private JObject _difficultyMeasuredRoot;
    private LoopLevelDifficultyAnalysis _difficultyMeasurement;

    private void DrawDifficultyTools(JObject root)
    {
        _showDifficultyTools = EditorGUILayout.Foldout(_showDifficultyTools, "Fill đầy & phân bổ độ khó", true);
        if (!_showDifficultyTools) return;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (!ReferenceEquals(root, _difficultyMeasuredRoot))
            {
                _difficultyMeasurement = LoopLevelDifficultyAnalysis.Evaluate(root);
                _difficultyMeasuredRoot = root;
            }
            EditorGUILayout.LabelField(_difficultyMeasurement.Describe(), EditorStyles.wordWrappedMiniLabel);
            _difficultyTarget = GUILayout.Toolbar(_difficultyTarget, new[] { "Dễ", "Thường", "Khó", "Rất khó" });
            _difficultyReservedBoxes = EditorGUILayout.IntSlider("Giữ hộp trống sẵn có", _difficultyReservedBoxes, 0, 2);
            _variantSeed = EditorGUILayout.IntField("Seed fill & trộn", _variantSeed);
            _difficultySelectedOnly = EditorGUILayout.ToggleLeft("Chỉ fill/trộn các hộp đã chọn", _difficultySelectedOnly);
            EditorGUILayout.LabelField("Mặc định fill cả bàn, không chừa hộp dư. Giữ bố cục, Hidden, hộp khóa/nhóm/liên kết và hộp ghim. Mỗi màu thường đúng 4 cube.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(_drawingRoad || _canvasGesture != 0 || EditorApplication.isPlaying ||
                (_difficultySelectedOnly && _canvasSelection.Count < 2)))
            {
                if (GUILayout.Button("Xem trước fill đầy + trộn theo độ khó"))
                    PreviewWorkspaceCommand("Fill đầy & trộn theo độ khó", BuildDifficultyPreview, verifySolution: true);
            }
            EditorGUILayout.LabelField("Điểm so theo phân bố 500 level gốc, không thay nhãn HardLevel. Preview tự tìm lời giải trong giới hạn bên dưới; chưa tìm thấy không đồng nghĩa vô nghiệm.", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void BuildDifficultyPreview(JObject root)
    {
        var indices = new List<int>();
        if (_difficultySelectedOnly) indices.AddRange(_canvasSelection);
        else
        {
            JArray boxes = A.Array(A.Layout(root), "Boxes");
            for (int index = 0; index < boxes.Count; index++) indices.Add(index);
        }
        LoopLevelDifficultyShuffle.Generate(root, indices, _manualPinnedBoxes, _variantSeed,
            _difficultyTarget, _difficultyReservedBoxes, out _proposalReport);
    }
}
#endif
