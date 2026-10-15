using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PuzzleMessagePackDecoder
{
	public static Puzzle Decode(byte[] bytes)
	{
		if (bytes == null || bytes.Length == 0) throw new ArgumentException("Puzzle data is empty.", nameof(bytes));
		var reader = new Reader(bytes);
		return ReadPuzzle(Map(reader.ReadValue()));
	}

	private static Puzzle ReadPuzzle(Dictionary<string, object> map)
	{
		var puzzle = new Puzzle
		{
			// Version = Int(map, "v", 7),
			HardLevel = Int(map, "h"),
			Size = Float(map, "s", 10f),
			PathLength = Int(map, "l"),
			PathSectionDistance = Float(map, "ps", 0.2f)
		};

		foreach (object value in List(map, "b")) puzzle.Boxes.Add(ReadBox(Map(value)));
		foreach (object value in List(map, "p")) puzzle.Paths.Add(ReadPath(Map(value)));
		foreach (object value in List(map, "m")) puzzle.MapData.Add(ReadMapComponent(Map(value)));
		foreach (object value in List(map, "g")) puzzle.Groups.Add(ReadGroup(Map(value)));
		foreach (object value in List(map, "c")) puzzle.Covers.Add(ReadCover(Map(value)));
		foreach (object value in List(map, "sd")) puzzle.SurpriseDatas.Add(ReadSurprise(Map(value)));
		return puzzle;
	}

	private static BoxData ReadBox(Dictionary<string, object> map)
	{
		var box = new BoxData
		{
			BoxType = (BoxType)Int(map, "t"),
			PathIndex = Int(map, "i"),
			PathIndexV2 = Int(map, "iv2"),
			Position = Vector(map, "p"),
			Rotation = Vector(map, "r"),
			LockColor = Int(map, "l"),
			IsEnterOnly = Int(map, "e"),
			NumLock = Int(map, "n"),
			LimitedColor = Int(map, "lc"),
			IsAdjacentLocked = Int(map, "al")
		};
		foreach (object value in List(map, "c")) box.Cubes.Add(ReadCube(Map(value)));
		foreach (object value in List(map, "lci")) box.LinkedCubes.Add(IntList(Values(value)));
		box.MultiColorLock = IntList(List(map, "mc"));
		return box;
	}

	private static CubeData ReadCube(Dictionary<string, object> map) => new CubeData
	{
		Type = Int(map, "t"),
		IsSecret = Int(map, "s"),
		GroupKeyId = Int(map, "k"),
		NumLock = Int(map, "l"),
		IsFixed = Int(map, "f")
	};

	private static SurpriseData ReadSurprise(Dictionary<string, object> map)
	{
		var data = new SurpriseData { SurpriseType = Int(map, "s"), NormalCubes = new List<CubeData>() };
		foreach (object value in List(map, "c")) data.NormalCubes.Add(ReadCube(Map(value)));
		return data;
	}

	private static GroupData ReadGroup(Dictionary<string, object> map) =>
		new GroupData(Int(map, "i"), IntList(List(map, "b")), Int(map, "c"));

	private static RollingCoverData ReadCover(Dictionary<string, object> map) =>
		new RollingCoverData(IntList(List(map, "b")));

	private static GamePath ReadPath(Dictionary<string, object> map)
	{
		var path = new GamePath { IsCircle = Int(map, "c") };
		foreach (object value in List(map, "p"))
		{
			Dictionary<string, object> point = Map(value);
			path.ControlPoints.Add(new ControlPoint(Vector(point, "p"), Vector(point, "i"), Vector(point, "o")));
		}
		return path;
	}

	private static MapComponent ReadMapComponent(Dictionary<string, object> map) =>
		new MapComponent(String(map, "n"), Vector(map, "p"), Vector(map, "r"), Vector(map, "s"));

	private static Dictionary<string, object> Map(object value) =>
		value as Dictionary<string, object> ?? new Dictionary<string, object>();
	private static List<object> Values(object value) => value as List<object> ?? new List<object>();
	private static List<object> List(Dictionary<string, object> map, string key)
	{
		object value;
		return map != null && map.TryGetValue(key, out value) ? Values(value) : new List<object>();
	}
	private static int Int(Dictionary<string, object> map, string key, int fallback = 0)
	{
		object value;
		return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToInt32(value) : fallback;
	}
	private static float Float(Dictionary<string, object> map, string key, float fallback = 0f)
	{
		object value;
		return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToSingle(value) : fallback;
	}
	private static string String(Dictionary<string, object> map, string key)
	{
		object value;
		return map != null && map.TryGetValue(key, out value) ? value as string ?? string.Empty : string.Empty;
	}
	private static Vector3 Vector(Dictionary<string, object> map, string key)
	{
		List<object> values = List(map, key);
		return values.Count < 3 ? Vector3.zero : new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
	}
	private static List<int> IntList(List<object> values)
	{
		var result = new List<int>(values.Count);
		for (int i = 0; i < values.Count; i++) result.Add(Convert.ToInt32(values[i]));
		return result;
	}

	private sealed class Reader
	{
		private readonly byte[] _data;
		private int _offset;
		public Reader(byte[] data) { _data = data; }

		public object ReadValue()
		{
			byte code = ReadByte();
			if (code <= 0x7f) return (int)code;
			if (code >= 0xe0) return (int)(sbyte)code;
			if ((code & 0xf0) == 0x80) return ReadMap(code & 0x0f);
			if ((code & 0xf0) == 0x90) return ReadArray(code & 0x0f);
			if ((code & 0xe0) == 0xa0) return ReadString(code & 0x1f);

			switch (code)
			{
				case 0xc0: return null;
				case 0xc2: return false;
				case 0xc3: return true;
				case 0xc4: return ReadBytes(ReadByte());
				case 0xc5: return ReadBytes(ReadUInt16());
				case 0xc6: return ReadBytes(checked((int)ReadUInt32()));
				case 0xca: return ReadFloat32();
				case 0xcb: return ReadFloat64();
				case 0xcc: return (int)ReadByte();
				case 0xcd: return (int)ReadUInt16();
				case 0xce: return (long)ReadUInt32();
				case 0xcf: return checked((long)ReadUInt64());
				case 0xd0: return (int)(sbyte)ReadByte();
				case 0xd1: return (int)(short)ReadUInt16();
				case 0xd2: return (int)ReadUInt32();
				case 0xd3: return (long)ReadUInt64();
				case 0xd9: return ReadString(ReadByte());
				case 0xda: return ReadString(ReadUInt16());
				case 0xdb: return ReadString(checked((int)ReadUInt32()));
				case 0xdc: return ReadArray(ReadUInt16());
				case 0xdd: return ReadArray(checked((int)ReadUInt32()));
				case 0xde: return ReadMap(ReadUInt16());
				case 0xdf: return ReadMap(checked((int)ReadUInt32()));
				default: throw new FormatException("Unsupported MessagePack code 0x" + code.ToString("X2") + " at " + (_offset - 1));
			}
		}

		private Dictionary<string, object> ReadMap(int count)
		{
			var map = new Dictionary<string, object>(count);
			for (int i = 0; i < count; i++)
			{
				object key = ReadValue();
				map[key == null ? string.Empty : Convert.ToString(key)] = ReadValue();
			}
			return map;
		}
		private List<object> ReadArray(int count)
		{
			var values = new List<object>(count);
			for (int i = 0; i < count; i++) values.Add(ReadValue());
			return values;
		}
		private string ReadString(int length) => Encoding.UTF8.GetString(ReadBytes(length));
		private byte[] ReadBytes(int length)
		{
			Ensure(length);
			var bytes = new byte[length];
			Buffer.BlockCopy(_data, _offset, bytes, 0, length);
			_offset += length;
			return bytes;
		}
		private byte ReadByte() { Ensure(1); return _data[_offset++]; }
		private ushort ReadUInt16() => (ushort)((ReadByte() << 8) | ReadByte());
		private uint ReadUInt32() => ((uint)ReadByte() << 24) | ((uint)ReadByte() << 16) | ((uint)ReadByte() << 8) | ReadByte();
		private ulong ReadUInt64() => ((ulong)ReadUInt32() << 32) | ReadUInt32();
		private float ReadFloat32()
		{
			byte[] bytes = ReadBytes(4);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return BitConverter.ToSingle(bytes, 0);
		}
		private double ReadFloat64()
		{
			byte[] bytes = ReadBytes(8);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return BitConverter.ToDouble(bytes, 0);
		}
		private void Ensure(int count)
		{
			if (count < 0 || _offset + count > _data.Length) throw new FormatException("Unexpected end of MessagePack data.");
		}
	}
}
