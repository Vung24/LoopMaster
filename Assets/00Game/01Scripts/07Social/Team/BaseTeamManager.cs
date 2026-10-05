using UnityEngine;

namespace Snui.Team
{
	public class BaseTeamManager : BaseManager
	{
		public virtual long RequestRemainTime => 0;
		public virtual long HelpRemainTime => 0;
	}
}