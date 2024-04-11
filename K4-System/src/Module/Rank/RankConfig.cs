namespace K4System
{
	using System.Text.RegularExpressions;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;
	using System.Collections.Generic;

	public partial class ModuleRank : IModuleRank
	{
		public void Initialize_Config()
		{
			string ranksFilePath = Path.Join(plugin.ModuleDirectory, "ranks.jsonc");

			string defaultRanksContent = @"{
	""None"": { // Whatever you set here, be unique. Not read by plugin
 		""Name"": ""None"", // This name is set in MySQL and also if not Tag is preset, this is the Tag
 		""Point"": -1, // Whatever you set to -1 is the default rank
 		""Color"": ""default""
 	},
	""Silver I"": {
		""Name"": ""Silver I"",
		""Tag"": ""[SI]"", // Clan tag (scoreboard) of the rank. If not set, it uses the key instead, which is currently
		""Point"": 100, // From this amount of experience, the player is Silver
		""Color"": ""silver"", // Color code for the rank. Find color names here: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Utils/ChatColors.cs
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
	},
	""Silver II"": {
		""Name"": ""Silver II"",
		""Tag"": ""[SII]"",
		""Point"": 500,
		""Color"": ""silver""
	},
	""Silver III"": {
		""Name"": ""Silver III"",
		""Tag"": ""[SIII]"",
		""Point"": 900,
		""Color"": ""silver""
	},
	""Silver IV"": {
		""Name"": ""Silver IV"",
		""Tag"": ""[SIV]"",
		""Point"": 1300,
		""Color"": ""silver""
	},
	""Silver Elite"": {
		""Name"": ""Silver Elite"",
		""Tag"": ""[SE]"",
		""Point"": 1700,
		""Color"": ""silver""
	},
	""Silver Elite Master"": {
		""Name"": ""Silver Elite Master"",
		""Tag"": ""[SEM]"",
		""Point"": 2100,
		""Color"": ""silver""
	},
	""Gold Nova I"": {
		""Name"": ""Gold Nova I"",
		""Tag"": ""[GNI]"",
		""Point"": 2600,
		""Color"": ""gold""
	},
	""Gold Nova II"": {
		""Name"": ""Gold Nova II"",
		""Tag"": ""[GNII]"",
		""Point"": 3100,
		""Color"": ""gold""
	},
	""Gold Nova III"": {
		""Name"": ""Gold Nova III"",
		""Tag"": ""[GNIII]"",
		""Point"": 3600,
		""Color"": ""gold""
	},
	""Gold Nova Master"": {
		""Name"": ""Gold Nova Master"",
		""Tag"": ""[GNM]"",
		""Point"": 4100,
		""Color"": ""gold""
	},
	""Master Guardian I"": {
		""Name"": ""Master Guardian I"",
		""Tag"": ""[MGI]"",
		""Point"": 4700,
		""Color"": ""green""
	},
	""Master Guardian II"": {
		""Name"": ""Master Guardian II"",
		""Tag"": ""[MGII]"",
		""Point"": 5300,
		""Color"": ""green""
	},
	""Master Guardian Elite"": {
		""Name"": ""Master Guardian Elite"",
		""Tag"": ""[MGE]"",
		""Point"": 5900,
		""Color"": ""green""
	},
	""Distinguished Master Guardian"": {
		""Name"": ""Distinguished Master Guardian"",
		""Tag"": ""[DMG]"",
		""Point"": 6500,
		""Color"": ""green""
	},
	""Legendary Eagle"": {
		""Name"": ""Legendary Eagle"",
		""Tag"": ""[LE]"",
		""Point"": 7200,
		""Color"": ""blue""
	},
	""Legendary Eagle Master"": {
		""Name"": ""Legendary Eagle Master"",
		""Tag"": ""[LEM]"",
		""Point"": 7900,
		""Color"": ""blue""
	},
	""Supreme Master First Class"": {
		""Name"": ""Supreme Master First Class"",
		""Tag"": ""[SMFC]"",
		""Point"": 8600,
		""Color"": ""purple""
	},
	""Global Elite"": {
		""Name"": ""Global Elite"",
		""Tag"": ""[GE]"",
		""Point"": 9300,
		""Color"": ""lightred""
	}
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
