using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Snui
{
    [Serializable]
    public class BoardData
    {
        public int Rows;
        public int Cols;
        public List<BlockData> Blocks = new List<BlockData>();
        public BoardData DeepCopy() => JsonConvert.DeserializeObject<BoardData>(JsonConvert.SerializeObject(this));
    }

    [Serializable, JsonObject]
    public class BlockData
    {
        public string Shape;
        public int PosX;
        public int PosY;
        public int Color;
        public bool[] Screws;
        public bool IsPrize;
        [JsonIgnore] public int BlockCount => Shape.Count(c => c == '1');
        [JsonIgnore] public int Rows => Shape.Count(c => c == '-') + 1;
        [JsonIgnore] public int Cols => Shape.IndexOf('-') < 0 ? Shape.Length : Shape.IndexOf('-');

        public void InitScrews()
        {
            string cells = Shape.Replace("-", string.Empty);
            if (cells.Length != Rows * Cols)
                throw new ArgumentException("Key Hunter shape must be a rectangular grid.", nameof(Shape));
            Screws = cells.Select(c => c == '1').ToArray();
        }
    }
}
