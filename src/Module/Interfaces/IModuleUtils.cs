using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace K4System
{
	public interface IModuleUtils
	{
		public void Initialize(bool hotReload);

		public void Release(bool hotReload);

		public void OnCommandAdmins(CCSPlayerController? player, CommandInfo info);
	}
}