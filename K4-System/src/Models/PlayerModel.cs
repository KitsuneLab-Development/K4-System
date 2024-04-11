
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static K4System.ModuleRank;
using static K4System.ModuleStat;
using static K4System.ModuleTime;

namespace K4System.Models;

public class K4Player
{
	//** ? Main */
	private readonly Plugin Plugin;

	//** ? Player */
	public readonly CCSPlayerController Controller;
	public readonly ulong SteamID;
	public readonly string PlayerName;

	//** ? Data */
	public RankData? rankData { get; set; }
	public StatData? statData { get; set; }
	public TimeData? timeData { get; set; }
	public (int killStreak, DateTime lastKillTime) KillStreak = (0, DateTime.MinValue);

	public K4Player(Plugin plugin, CCSPlayerController playerController)
	{
		Plugin = plugin;

		Controller = playerController;
		SteamID = playerController.SteamID;
		PlayerName = playerController.PlayerName;
	}

	public bool IsValid
	{
		get
		{
			return Controller?.IsValid == true && Controller.PlayerPawn?.IsValid == true && Controller.Connected == PlayerConnectedState.PlayerConnected;
		}
	}

	public bool IsPlayer
	{
		get
		{
			return !Controller.IsBot && !Controller.IsHLTV;
		}
	}

	public string ClanTag
	{
		get { return Controller.Clan; }
		set
		{
			Controller.Clan = value;
			Utilities.SetStateChanged(Controller, "CCSPlayerController", "m_szClan");
		}
	}
}