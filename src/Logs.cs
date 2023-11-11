using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4ryuuSystem
{
	public partial class K4System
	{
		public enum LogLevel
		{
			Debug = -1,
			Info,
			Warning,
			Error
		}

		public void Log(string message, LogLevel level = LogLevel.Info)
		{
			if ((int)level < config.LogLevel)
			{
				return; // Skip logging if the log level is lower than the configured level
			}

			string logFile = Path.Join(_ModuleDirectory, $"logs-{DateTime.Now:yyyy-MM-dd}.txt");

			string logLevelString = LogLevelToString(level);
			string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevelString}] > {message}";

			using (StreamWriter writer = File.AppendText(logFile))
			{
				writer.WriteLine(logMessage);
			}

			if (level == LogLevel.Debug)
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();

				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot)
						continue;

					if (!AdminManager.PlayerHasPermissions(player, "@css/config"))
						continue;

					Server.PrintToChatAll($" {ChatColors.Gold}[{logLevelString}] {ChatColors.Yellow}{message}");
				}
			}

			Console.ForegroundColor = GetConsoleColor(level);
			Console.WriteLine(logMessage);
			Console.ResetColor();
		}

		private string LogLevelToString(LogLevel level)
		{
			switch (level)
			{
				case LogLevel.Debug:
					return "DEBUG";
				case LogLevel.Info:
					return "INFO";
				case LogLevel.Warning:
					return "WARNING";
				case LogLevel.Error:
					return "ERROR";
				default:
					return "UNKNOWN";
			}
		}

		private static ConsoleColor GetConsoleColor(LogLevel level)
		{
			switch (level)
			{
				case LogLevel.Debug:
					return ConsoleColor.Gray;
				case LogLevel.Info:
					return ConsoleColor.White;
				case LogLevel.Warning:
					return ConsoleColor.Yellow;
				case LogLevel.Error:
					return ConsoleColor.Red;
				default:
					return ConsoleColor.Gray;
			}
		}
	}
}
