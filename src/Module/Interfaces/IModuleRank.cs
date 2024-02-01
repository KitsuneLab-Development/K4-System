using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static K4System.ModuleRank;

namespace K4System
{
	public interface IModuleRank
	{
		public void Initialize(bool hotReload);

		public void Release(bool hotReload);

		public Rank GetNoneRank();

		public void LoadRankData(int slot, int points);

		public void BeforeRoundEnd(int winnerTeam);

		public void OnCommandRank(CCSPlayerController? player, CommandInfo info);

		public void OnCommandRanks(CCSPlayerController? player, CommandInfo info);

		public void OnCommandResetMyRank(CCSPlayerController? player, CommandInfo info);

		public void OnCommandTop(CCSPlayerController? player, CommandInfo info);

		public void OnCommandResetRank(CCSPlayerController? player, CommandInfo info);

		public void OnCommandSetPoints(CCSPlayerController? player, CommandInfo info);

		public void OnCommandGivePoints(CCSPlayerController? player, CommandInfo info);

		public void OnCommandRemovePoints(CCSPlayerController? player, CommandInfo info);
	}
}