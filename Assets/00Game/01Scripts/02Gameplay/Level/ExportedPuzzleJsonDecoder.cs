using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>Reads JSON level packs containing puzzle metadata and a gameplay layout.</summary>
public static class ExportedPuzzleJsonDecoder
{
	public static Puzzle Decode(string json)
	{
		return DecodeLevel(json).Puzzle;
	}

	/// <summary>
	/// Decodes both the exported level envelope and its Puzzle layout.  The
	/// envelope values are needed because GameData.Id is the puzzle id while
	/// Puzzle.MainLevel/InnerIndex describe the displayed level.
	/// </summary>
	public static ExportedPuzzleLevel DecodeLevel(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			throw new ArgumentException("Puzzle JSON is empty.", nameof(json));
		var reader = new JsonReader(json);
		Dictionary<string, object> root = Map(reader.ReadValue());
		Dictionary<string, object> layout = root.ContainsKey("layout")
			? Map(root["layout"])
			: root;
		var result = new ExportedPuzzleLevel
		{
			PuzzleId = Int(root, "puzzle_id", -1),
			LevelNumber = Int(root, "level_number", -1),
			Puzzle = ReadPuzzle(layout)
		};
		return result;
	}

	private static Puzzle ReadPuzzle(Dictionary<string, object> map)
	{
		int version = Int(map, "Version", Puzzle.LegacyVersion);
		var puzzle = new Puzzle
		{
			Version = version > 0 ? version : Puzzle.LegacyVersion,
			HardLevel = Int(map, "HardLevel"),
			Size = Float(map, "Size", 10f),
			PathLength = Int(map, "PathLength"),
			PathSectionDistance = Float(map, "PathSectionDistance", 0.2f)
		};
		if (puzzle.PathSectionDistance <= 0f) puzzle.PathSectionDistance = 0.2f;
		foreach (object value in List(map, "Boxes")) puzzle.Boxes.Add(ReadBox(Map(value)));
		foreach (object value in List(map, "Paths")) puzzle.Paths.Add(ReadPath(Map(value)));
		foreach (object value in List(map, "MapData")) puzzle.MapData.Add(ReadMapComponent(Map(value)));
		foreach (object value in List(map, "Groups")) puzzle.Groups.Add(ReadGroup(Map(value)));
		foreach (object value in List(map, "Covers")) puzzle.Covers.Add(ReadCover(Map(value)));
		foreach (object value in List(map, "SurpriseDatas")) puzzle.SurpriseDatas.Add(ReadSurprise(Map(value)));
		return puzzle;
	}

	private static BoxData ReadBox(Dictionary<string, object> map)
	{
		var box = new BoxData
		{
			BoxType = (BoxType)Int(map, "BoxType"),
			PathIndex = Int(map, "PathIndex"),
			PathIndexV2 = Int(map, "PathIndexV2"),
			Position = Vector(map, "Position"),
			Rotation = Vector(map, "Rotation"),
			LockColor = Int(map, "LockColor"),
			IsEnterOnly = Int(map, "IsEnterOnly"),
			NumLock = Int(map, "NumLock"),
			LimitedColor = Int(map, "LimitedColor"),
			IsAdjacentLocked = Int(map, "IsAdjacentLocked")
		};
		foreach (object value in List(map, "Cubes")) box.Cubes.Add(ReadCube(Map(value)));
		foreach (object value in List(map, "LinkedCubes")) box.LinkedCubes.Add(IntList(Values(value)));
		box.MultiColorLock = IntList(List(map, "MultiColorLock"));
		return box;
	}

	private static CubeData ReadCube(Dictionary<string, object> map) => new CubeData
	{
		Type = Int(map, "Type"), IsSecret = Int(map, "IsSecret"),
		GroupKeyId = Int(map, "GroupKeyId"), NumLock = Int(map, "NumLock"),
		IsFixed = Int(map, "IsFixed")
	};

	private static GamePath ReadPath(Dictionary<string, object> map)
	{
		var path = new GamePath { IsCircle = Int(map, "IsCircle") };
		foreach (object value in List(map, "ControlPoints"))
		{
			Dictionary<string, object> point = Map(value);
			path.ControlPoints.Add(new ControlPoint(Vector(point, "Position"),
				Vector(point, "InTangent"), Vector(point, "OutTangent")));
		}
		return path;
	}

	private static MapComponent ReadMapComponent(Dictionary<string, object> map) =>
		new MapComponent(String(map, "Name"), Vector(map, "Position"),
			Vector(map, "Rotation"), Vector(map, "Scale"));

	private static GroupData ReadGroup(Dictionary<string, object> map) =>
		new GroupData(Int(map, "Id"), IntList(List(map, "BoxIndexes")), Int(map, "LockedByColor"));

	private static RollingCoverData ReadCover(Dictionary<string, object> map) =>
		new RollingCoverData(IntList(List(map, "BoxIndexes")));

	private static SurpriseData ReadSurprise(Dictionary<string, object> map)
	{
		var data = new SurpriseData
		{
			SurpriseType = Int(map, "SurpriseType"), NormalCubes = new List<CubeData>()
		};
		foreach (object value in List(map, "NormalCubes")) data.NormalCubes.Add(ReadCube(Map(value)));
		return data;
	}

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
		return map != null && map.TryGetValue(key, out value) && value != null
			? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;
	}
	private static float Float(Dictionary<string, object> map, string key, float fallback = 0f)
	{
		object value;
		return map != null && map.TryGetValue(key, out value) && value != null
			? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
	}
	private static string String(Dictionary<string, object> map, string key)
	{
		object value;
		return map != null && map.TryGetValue(key, out value) ? value as string ?? string.Empty : string.Empty;
	}
	private static Vector3 Vector(Dictionary<string, object> map, string key)
	{
		List<object> values = List(map, key);
		return values.Count < 3 ? Vector3.zero : new Vector3(
			Convert.ToSingle(values[0], CultureInfo.InvariantCulture),
			Convert.ToSingle(values[1], CultureInfo.InvariantCulture),
			Convert.ToSingle(values[2], CultureInfo.InvariantCulture));
	}
	private static List<int> IntList(List<object> values)
	{
		var result = new List<int>(values.Count);
		foreach (object value in values)
			result.Add(Convert.ToInt32(value, CultureInfo.InvariantCulture));
		return result;
	}

	private sealed class JsonReader
	{
		private readonly string _json;
		private int _offset;
		public JsonReader(string json) { _json = json; }

		public object ReadValue()
		{
			SkipWhitespace();
			if (_offset >= _json.Length) throw Error("Unexpected end of JSON");
			char token = _json[_offset];
			if (token == '{') return ReadObject();
			if (token == '[') return ReadArray();
			if (token == '"') return ReadString();
			if (token == '-' || char.IsDigit(token)) return ReadNumber();
			if (ReadLiteral("true")) return true;
			if (ReadLiteral("false")) return false;
			if (ReadLiteral("null")) return null;
			throw Error("Unexpected token '" + token + "'");
		}

		private Dictionary<string, object> ReadObject()
		{
			var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			_offset++;
			SkipWhitespace();
			if (Take('}')) return result;
			while (true)
			{
				SkipWhitespace();
				if (_offset >= _json.Length || _json[_offset] != '"') throw Error("Expected property name");
				string key = ReadString();
				SkipWhitespace();
				Expect(':');
				result[key] = ReadValue();
				SkipWhitespace();
				if (Take('}')) return result;
				Expect(',');
			}
		}

		private List<object> ReadArray()
		{
			var result = new List<object>();
			_offset++;
			SkipWhitespace();
			if (Take(']')) return result;
			while (true)
			{
				result.Add(ReadValue());
				SkipWhitespace();
				if (Take(']')) return result;
				Expect(',');
			}
		}

		private string ReadString()
		{
			Expect('"');
			var value = new System.Text.StringBuilder();
			while (_offset < _json.Length)
			{
				char character = _json[_offset++];
				if (character == '"') return value.ToString();
				if (character != '\\') { value.Append(character); continue; }
				if (_offset >= _json.Length) throw Error("Incomplete escape sequence");
				char escaped = _json[_offset++];
				switch (escaped)
				{
					case '"': case '\\': case '/': value.Append(escaped); break;
					case 'b': value.Append('\b'); break;
					case 'f': value.Append('\f'); break;
					case 'n': value.Append('\n'); break;
					case 'r': value.Append('\r'); break;
					case 't': value.Append('\t'); break;
					case 'u': value.Append(ReadUnicode()); break;
					default: throw Error("Invalid escape sequence");
				}
			}
			throw Error("Unterminated string");
		}

		private char ReadUnicode()
		{
			if (_offset + 4 > _json.Length) throw Error("Incomplete unicode escape");
			int code;
			if (!int.TryParse(_json.Substring(_offset, 4), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture, out code)) throw Error("Invalid unicode escape");
			_offset += 4;
			return (char)code;
		}

		private object ReadNumber()
		{
			int start = _offset;
			if (_json[_offset] == '-') _offset++;
			while (_offset < _json.Length && char.IsDigit(_json[_offset])) _offset++;
			bool floatingPoint = false;
			if (_offset < _json.Length && _json[_offset] == '.')
			{
				floatingPoint = true; _offset++;
				while (_offset < _json.Length && char.IsDigit(_json[_offset])) _offset++;
			}
			if (_offset < _json.Length && (_json[_offset] == 'e' || _json[_offset] == 'E'))
			{
				floatingPoint = true; _offset++;
				if (_offset < _json.Length && (_json[_offset] == '+' || _json[_offset] == '-')) _offset++;
				while (_offset < _json.Length && char.IsDigit(_json[_offset])) _offset++;
			}
			string text = _json.Substring(start, _offset - start);
			if (floatingPoint) return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
			return long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
		}

		private bool ReadLiteral(string literal)
		{
			if (_offset + literal.Length > _json.Length ||
				string.Compare(_json, _offset, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
				return false;
			_offset += literal.Length;
			return true;
		}
		private bool Take(char character)
		{
			if (_offset >= _json.Length || _json[_offset] != character) return false;
			_offset++;
			return true;
		}
		private void Expect(char character)
		{
			SkipWhitespace();
			if (!Take(character)) throw Error("Expected '" + character + "'");
		}
		private void SkipWhitespace()
		{
			while (_offset < _json.Length && char.IsWhiteSpace(_json[_offset])) _offset++;
		}
		private FormatException Error(string message) =>
			new FormatException(message + " at character " + _offset + ".");
	}
}

public sealed class ExportedPuzzleLevel
{
	public int PuzzleId;
	public int LevelNumber;
	public Puzzle Puzzle;
}
