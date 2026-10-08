using UnityEngine;

public static class RoadGeometry
{
	public const int SectionVertexCount = 14;
	private static readonly float[] Side =
		{ 0.35f, -0.35f, 0.35f, 0.35f, 0.37f, 0.48f, 0.5f, 0.5f,
		  -0.5f, -0.5f, -0.48f, -0.37f, -0.35f, -0.35f };
	private static readonly float[] Height =
		{ 0.1f, 0.1f, -0.2f, 0.2f, 0.22f, 0.22f, 0.2f, -0.2f,
		  -0.2f, 0.2f, 0.22f, 0.22f, 0.2f, -0.2f };

	public static Vector3 GetVertex(int index, Vector3 point, Vector3 right) =>
		point + right * Side[index] + new Vector3(0f, Height[index], 0f);

	public static void WriteRoadLink(int[] tris, int offset, int from, int to)
	{
		tris[offset] = from + 1; tris[offset + 1] = from; tris[offset + 2] = to + 1;
		tris[offset + 3] = from; tris[offset + 4] = to; tris[offset + 5] = to + 1;
	}

	public static void WriteWallLink(int[] tris, int offset, int from, int to)
	{
		for (int side = 0; side < 2; side++)
			for (int edge = 0; edge < 5; edge++)
			{
				int k = (side == 0 ? 2 : 8) + edge;
				int t = offset + (side * 5 + edge) * 6;
				tris[t] = from + k; tris[t + 1] = from + k + 1; tris[t + 2] = to + k;
				tris[t + 3] = from + k + 1; tris[t + 4] = to + k + 1; tris[t + 5] = to + k;
			}
	}
}