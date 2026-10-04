using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TextVerbatimEnlargeAnim : MonoBehaviour
{
	public bool IsPlayOnStart;
	private TMP_Text _tmp;
	private CancellationToken _token;

	private void Awake()
	{
		_tmp = GetComponent<TMP_Text>();
		_token = this.GetCancellationTokenOnDestroy();
	}

	public void Start()
	{
		if (IsPlayOnStart) PlayTextEnlargeAnim().Forget();
	}

	public async UniTask PlayTextEnlargeAnim(float beginOffset = -10f, float bigOffset = 30f,
		float speedFrame = 2f, float speed = 5f)
	{
		if (_tmp == null) return;
		_tmp.ForceMeshUpdate();
		TMP_MeshInfo[] cached = _tmp.textInfo.CopyMeshInfoVertexData();
		float elapsed = 0f;
		float duration = Mathf.Max(0.2f, (_tmp.textInfo.characterCount + 1) / Mathf.Max(1f, speed));

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			TMP_TextInfo info = _tmp.textInfo;
			for (int i = 0; i < info.characterCount; i++)
			{
				TMP_CharacterInfo character = info.characterInfo[i];
				if (!character.isVisible) continue;
				int material = character.materialReferenceIndex;
				int vertex = character.vertexIndex;
				Vector3[] source = cached[material].vertices;
				Vector3[] target = info.meshInfo[material].vertices;
				Vector3 center = (source[vertex] + source[vertex + 2]) * 0.5f;
				float phase = elapsed * speedFrame - i * 0.22f + beginOffset * 0.01f;
				float scale = 1f + Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI)) * bigOffset * 0.01f;
				for (int j = 0; j < 4; j++) target[vertex + j] = center + (source[vertex + j] - center) * scale;
			}
			for (int i = 0; i < info.meshInfo.Length; i++)
			{
				info.meshInfo[i].mesh.vertices = info.meshInfo[i].vertices;
				_tmp.UpdateGeometry(info.meshInfo[i].mesh, i);
			}
			await UniTask.Yield(PlayerLoopTiming.Update, _token);
		}
		_tmp.ForceMeshUpdate();
	}
}
