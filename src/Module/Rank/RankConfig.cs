namespace K4System
{
	using System.Text.RegularExpressions;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;
	using System.Collections.Generic;

	public partial class ModuleRank : IModuleRank
	{
		public void Initialize_Config(Plugin plugin)
		{
			string ranksFilePath = Path.Join(ModuleDirectory, "ranks.jsonc");

			string defaultRanksContent = @"{
	""None"": { // Whatever you set here, be unique. Not read by plugin
		""Name"": ""None"", // This name is set in MySQL and also if not Tag is preset, this is the Tag
		""Point"": -1, // Whatever you set to -1 is the default rank
		""Color"": ""default""
	},
	""Silver"": {
		""Name"": ""Silver"",
		""Tag"": ""[S]"", // Clan tag (scoreboard) of the rank. If not set, it uses the key instead, which is currently ""Silver""
		""Point"": 250, // From this amount of experience, the player is Silver
		""Color"": ""lightblue"" // Color code for the rank. Find color names here: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Utils/ChatColors.cs
	},
	""Gold"": {
		""Name"": ""Gold"",
		""Tag"": ""[G]"",
		""Point"": 1000,
		""Color"": ""red"",
		""Permissions"": [ // You can add permissions to the rank. If you don't want to add any, remove this line
			{
				""DisplayName"": ""Super Permission"", // This is the name of the permission. Will be displayed in the menu of ranks to let people know the benefits of a rank
				""PermissionName"": ""permission1"" // This is the permission name. You can assign 3rd party permissions here
			},
			{
				""DisplayName"": ""Legendary Permission"",
				""PermissionName"": ""permission2""
			}
			// You can add as many as you want
		]
	}
	// You can add as many as you want
}";

			if (!File.Exists(ranksFilePath))
			{
				File.WriteAllText(ranksFilePath, defaultRanksContent);
				Logger.LogInformation("Default ranks file created.");
			}

			try
			{
				var jsonContent = Regex.Replace(File.ReadAllText(ranksFilePath), @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
				rankDictionary = JsonConvert.DeserializeObject<Dictionary<string, Rank>>(jsonContent)!;

				rankDictionary = rankDictionary.OrderBy(kv => kv.Value.Point).ToDictionary(kv => kv.Key, kv => kv.Value);

				int id = rankDictionary.Values.First().Point == -1 ? -1 : 0;
				foreach (Rank rank in rankDictionary.Values)
				{
					rank.Id = id++;
					rank.Color = plugin.ApplyPrefixColors(rank.Color);
				}

				Rank? temp = rankDictionary.Values.FirstOrDefault(rank => rank.Point == -1);
				if (temp == null)
				{
					Logger.LogWarning("Default rank is not set. You can set it by creating a rank with -1 point.");

					noneRank = new Rank
					{
						Id = -1,
						Name = "None",
						Point = -1,
						Color = "Default"
					};
				}
				else
					noneRank = temp;
			}
			catch (Exception ex)
			{
				Logger.LogError("An error occurred: " + ex.Message);
			}
		}
	}
}