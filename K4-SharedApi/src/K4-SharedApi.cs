using CounterStrikeSharp.API.Core;

namespace K4SharedApi
{
	public interface IPlayerAPI
	{
		bool IsLoaded { get; }
		bool IsValid { get; }
		bool IsPlayer { get; }
		CCSPlayerController Controller { get; }
		int Points { get; set; }
		int RankID { get; }
		string RankName { get; }
		string RankClanTag { get; }
	}
}
