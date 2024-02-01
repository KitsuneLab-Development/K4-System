namespace K4System
{
	using System.Text;

	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleTime : IModuleTime
	{
		public void LoadTimeData(int slot, Dictionary<string, int> timeData)
		{
			DateTime now = DateTime.UtcNow;

			TimeData playerData = new TimeData
			{
				TimeFields = timeData,
				Times = new Dictionary<string, DateTime>
				{
					{ "Connect", now },
					{ "Team", now },
					{ "Death", now }
				}
			};

			timeCache[slot] = playerData;
		}

		public void BeforeDisconnect(CCSPlayerController player)
		{
			DateTime now = DateTime.UtcNow;

			TimeData playerData = timeCache[player];

			playerData.TimeFields["all"] += (int)Math.Round((now - playerData.Times["Connect"]).TotalSeconds);
			playerData.TimeFields[GetFieldForTeam((CsTeam)player.TeamNum)] += (int)Math.Round((now - playerData.Times["Team"]).TotalSeconds);

			if ((CsTeam)player.TeamNum > CsTeam.Spectator)
				playerData.TimeFields[player.PawnIsAlive ? "alive" : "dead"] += (int)Math.Round((now - playerData.Times["Death"]).TotalSeconds);
		}

		public string GetFieldForTeam(CsTeam team)
		{
			switch (team)
			{
				case CsTeam.Terrorist:
					return "t";
				case CsTeam.CounterTerrorist:
					return "ct";
				default:
					return "spec";
			}
		}

		public string FormatPlaytime(int totalSeconds)
		{
			string[] units = { "k4.phrases.shortyear", "k4.phrases.shortmonth", "k4.phrases.shortday", "k4.phrases.shorthour", "k4.phrases.shortminute", "k4.phrases.shortsecond" };
			int[] values = { totalSeconds / 31536000, totalSeconds % 31536000 / 2592000, totalSeconds % 2592000 / 86400, totalSeconds % 86400 / 3600, totalSeconds % 3600 / 60, totalSeconds % 60 };

			StringBuilder formattedTime = new StringBuilder();

			bool addedValue = false;

			Plugin plugin = (this.PluginContext.Plugin as Plugin)!;

			for (int i = 0; i < units.Length; i++)
			{
				if (values[i] > 0)
				{
					formattedTime.Append($"{values[i]}{plugin.Localizer[units[i]]}, ");
					addedValue = true;
				}
			}

			if (!addedValue)
			{
				formattedTime.Append("0s");
			}

			return formattedTime.ToString().TrimEnd(' ', ',');
		}
	}
}
