using System;
using Snui.Profile;

namespace Snui
{
	[Serializable]
	public class EventRaceOpponent : PlayerInfo
	{
		public enum BehaviorMode
		{
			Normal = 0,
			Slow = 1,
			Fast = 2,
			Stop = 3
		}

		public bool IsVip;
		public bool IsSelf;
		public BehaviorMode Behavior;
		public int Speed;
		public int Rank;
		public int Score;
		public int LastScore;
		public bool IsCompleted;
		public int WaitCount;
		public int WaitingTime;

		public override string AvatarFrame
		{
			get
			{
				if (string.IsNullOrEmpty(base.AvatarFrame) && IsVip)
				{
					return "pass";
				}
				if (base.AvatarFrame == "pass" && !IsVip)
				{
					return null;
				}
				return base.AvatarFrame;
			}
			set => base.AvatarFrame = value;
		}

		public override string NameEffects
		{
			get
			{
				if (string.IsNullOrEmpty(base.NameEffects) && IsVip)
				{
					return "pass";
				}
				if (base.NameEffects == "pass" && !IsVip)
				{
					return null;
				}
				return base.NameEffects;
			}
			set => base.NameEffects = value;
		}

		public EventRaceOpponent()
		{
		}

		public EventRaceOpponent(string name, int speed)
		{
			Name = name;
			Speed = speed;
		}

		public void UpdateLastRecord()
		{
			LastScore = Score;
		}

		public static EventRaceOpponent GetSelf()
		{
			var self = new EventRaceOpponent();
			self.IsSelf = true;
			self.Name = "You";
			return self;
		}
	}
}