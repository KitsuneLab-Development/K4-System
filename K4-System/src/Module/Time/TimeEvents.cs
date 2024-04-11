
namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleTime : IModuleTime
	{
		public void Initialize_Events()
		{
			plugin.RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				TimeData? playerData = k4player.timeData;

				if (playerData is null)
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;

				playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
				playerData.Times["Connect"] = now;

				if ((CsTeam)@event.Oldteam != CsTeam.None)
				{
					playerData.TimeFields[GetFieldForTeam((CsTeam)@event.Oldteam)] += (int)(now - playerData.Times["Team"]).TotalSeconds;
				}

				playerData.Times["Team"] = now;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				TimeData? playerData = k4player.timeData;

				if (playerData is null)
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;
				playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
				playerData.Times["Connect"] = now;

				playerData.TimeFields["dead"] += (int)(now - playerData.Times["Death"]).TotalSeconds;
				playerData.Times["Death"] = DateTime.UtcNow;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);

				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				TimeData? playerData = k4player.timeData;

				if (playerData is null)
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;
				playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
				playerData.Times["Connect"] = now;

				playerData.TimeFields["alive"] += (int)(now - playerData.Times["Death"]).TotalSeconds;
				playerData.Times["Death"] = now;

				return HookResult.Continue;
			});
		}
	}
}