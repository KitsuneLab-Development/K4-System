namespace K4System
{
	using System.Runtime.CompilerServices;
	using System.Text;

	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleTime : IModuleTime
	{
		public void BeforeDisconnect(K4Player k4player)
		{
			DateTime now = DateTime.UtcNow;
			TimeData? playerData = k4player.timeData;

			if (playerData is null)
				return;

			playerData.TimeFields["all"] += (int)(now - playerData.Times["Connect"]).TotalSeconds;
			playerData.TimeFields[GetFieldForTeam(k4player.Controller.Team)] += (int)(now - playerData.Times["Team"]).TotalSeconds;

			if (k4player.Controller.Team > CsTeam.Spectator)
				playerData.TimeFields[k4player.Controller.PawnIsAlive ? "alive" : "dead"] += (int)(now - playerData.Times["Death"]).TotalSeconds;

			// This is for the mapchange cases
			playerData.Times = new Dictionary<string, DateTime>
			{
				{ "Connect", now },
				{ "Team", now },
				{ "Death", now }
			};
		}

		public string GetFieldForTeam(CsTeam team)
		{
			return team switch
			{
				CsTeam.Terrorist => "t",
				CsTeam.CounterTerrorist => "ct",
				_ => "spec"
			};
		}

		public string FormatPlaytime(int totalSeconds)
		{
			string[] units = { "year", "month", "day", "hour", "minute", "second" };
			int[] timeDivisors = { 31536000, 2592000, 86400, 3600, 60, 1 };

			StringBuilder formattedTime = new StringBuilder();
			bool addedValue = false;

			for (int i = 0; i < units.Length; i++)
			{
				int timeValue = totalSeconds / timeDivisors[i];
				totalSeconds %= timeDivisors[i];
				if (timeValue > 0)
				{
					if (formattedTime.Length > 0)
					{
						formattedTime.Append(", ");
					}
					formattedTime.Append($"{timeValue}{GetShortUnit(units[i])}");
					addedValue = true;
				}
			}

			if (!addedValue)
			{
				return "0" + GetShortUnit("second");
			}

			return formattedTime.ToString();

			string GetShortUnit(string unit)
			{
				return plugin.Localizer[$"k4.phrases.short{unit}"];
			}
		}
	}
}
