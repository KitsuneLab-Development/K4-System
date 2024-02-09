using static K4System.ModuleRank;

namespace K4System;

public interface IModuleRank
{
	public void Initialize(bool hotReload);

	public void Release(bool hotReload);

	public Rank GetNoneRank();

	public void LoadRankData(int slot, int points);

	public void BeforeRoundEnd(int winnerTeam);
}
