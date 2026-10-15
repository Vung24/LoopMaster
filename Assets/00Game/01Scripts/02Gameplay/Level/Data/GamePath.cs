using System;
using System.Collections.Generic;
using UnityEngine;

public class GamePath
{
	public int IsCircle;
	public List<ControlPoint> ControlPoints;

	public GamePath()
	{
		ControlPoints = new List<ControlPoint>();
	}
}

[Serializable]
public class ControlPoint
{
	public Vector3 Position;
	public Vector3 InTangent;
	public Vector3 OutTangent;

	public ControlPoint()
	{
	}

	public ControlPoint(Vector3 pos, Vector3 inTangent, Vector3 outTangent)
	{
		Position = pos;
		InTangent = inTangent;
		OutTangent = outTangent;
	}

	public ControlPoint(Vector3 pos)
	{
		Position = pos;
		InTangent = pos;
		OutTangent = pos;
	}
}
