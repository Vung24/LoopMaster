using System.Collections.Generic;
using UnityEngine;

namespace Snui.Profile
{
	public class BaseAvatarManager : BaseManager
	{
		public virtual bool IsAvatarFrameUnlock(string name)
		{
			return string.IsNullOrEmpty(name) || name == "default";
		}

		public virtual List<string> GetAllAvatarFrames()
		{
			return new List<string> { "default" };
		}

		public virtual string GetDefaultAvatarFrameName()
		{
			return "default";
		}

		public virtual List<string> GetBuiltinAvatarUrls()
		{
			return new List<string>();
		}
	}
}