using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snui
{
    public enum RewardLayoutType { RewardDialog = 0, RewardDetailToast = 1, ClaimRewardsToast = 2, Card = 3 }
    public static class RewardLayoutHelper
    {
        private static readonly float[][] Scale = { new[] {.95f,.8f,.75f,.65f}, new[] {.35f,.35f,.35f,.35f},
            new[] {.5f,.4f,.4f,.4f}, new[] {.55f,.55f,.55f,.55f} };
        private static readonly float[][] SpaceX = {new[] {200f,200f,180f},new[] {90f,90f,90f},new[] {130f,95f,90f},new[] {242f,242f,242f}};
        private static readonly float[][] SpaceY = {new[] {230f,180f},new[] {90f,90f},new[] {90f,90f},new[] {330f,330f}};
        public static (float, List<Vector2>) GetRewardsLayoutPos(int count, RewardLayoutType rewardLayoutType = RewardLayoutType.RewardDialog)
        {
            if (count < 0 || count > 12) throw new ArgumentOutOfRangeException(nameof(count));
            int type = (int)rewardLayoutType; if (type < 0 || type > 3) type = 0;
            var positions = new List<Vector2>();
            if (count == 0) return (Scale[type][0], positions);
            if (count == 4)
            {
                float x = type == 0 ? 100 : type == 3 ? 121 : 45;
                float top = type == 0 ? 100 : type == 3 ? 165 : 45;
                float bottom = type == 0 ? -130 : -top;
                positions.AddRange(new[] { new Vector2(-x,top),new Vector2(x,top),new Vector2(-x,bottom),new Vector2(x,bottom) });
                return (Scale[type][1], positions);
            }
            int rows = (count + 3) / 4, columns = (count + rows - 1) / rows;
            float dx = columns > 1 ? SpaceX[type][columns-2] : 0;
            float dy = rows > 1 ? SpaceY[type][rows-2] : 0;
            float y = dy * (rows-1) * .5f;
            int remaining = count;
            while (remaining > 0)
            {
                int row = remaining % columns; if (row == 0) row = columns;
                float x = dx * (1-row) * .5f;
                for (int i=0;i<row;i++) positions.Add(new Vector2(x+i*dx,y));
                remaining -= row; y -= dy;
            }
            return (Scale[type][columns-1], positions);
        }
        public static Vector2 GetLayoutSize(int count, RewardLayoutType type = RewardLayoutType.RewardDialog)
        {
            var layout = GetRewardsLayoutPos(count,type);
            if (layout.Item2.Count == 0) return Vector2.zero;
            Vector2 min = layout.Item2[0], max = min;
            foreach (var p in layout.Item2) { min = Vector2.Min(min,p); max = Vector2.Max(max,p); }
            return max - min + Vector2.one * (256 * layout.Item1);
        }
    }
}
