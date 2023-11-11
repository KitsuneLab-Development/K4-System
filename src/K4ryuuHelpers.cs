using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
internal static class CCSPlayerControllerEx
{
	internal static bool IsValidPlayer(this CCSPlayerController? controller)
	{
		return controller != null && controller.IsValid && !controller.IsBot;
	}
}

internal static class K4ryuu
{
	internal static CCSGameRules GameRules()
	{
		return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
	}

	internal static CCSPlayerResource PlayerResource()
	{
		return Utilities.FindAllEntitiesByDesignerName<CCSPlayerResource>("cs_player_manager").First();
	}

	internal static IEnumerable<CCSTeam> TeamManagers()
	{
		return Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");
	}

	internal static CCSTeam? TeamManager(CsTeam team)
	{
		IEnumerable<CCSTeam> teamManagers = TeamManagers();

		foreach (var teamManager in teamManagers)
		{
			if ((int)team == teamManager.TeamNum)
			{
				return teamManager;
			}
		}

		return null;
	}
}