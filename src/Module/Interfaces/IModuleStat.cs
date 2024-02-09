namespace K4System;

public interface IModuleStat
{
	public void Initialize(bool hotReload);

	public void Release(bool hotReload);

	public void LoadStatData(int slot, Dictionary<string, int> statData);

	public void BeforeRoundEnd(int winnerTeam);
}
