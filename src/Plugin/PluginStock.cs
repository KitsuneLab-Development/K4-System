
namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	using System.Reflection;

	public sealed partial class Plugin : BasePlugin
	{
		public string ApplyPrefixColors(string msg)
		{
			if (!msg.Contains('{'))
			{
				return string.IsNullOrEmpty(msg) ? $"{ChatColors.LightRed}[K4-System]" : msg;
			}

			string modifiedValue = msg;
			Type chatColorsType = typeof(ChatColors);

			foreach (FieldInfo field in chatColorsType.GetFields())
			{
				string pattern = $"{{{field.Name}}}";
				if (modifiedValue.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			return modifiedValue;
		}
	}
}