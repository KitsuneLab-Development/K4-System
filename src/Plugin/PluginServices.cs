namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using Microsoft.Extensions.DependencyInjection;

	public class PluginServices : IPluginServiceCollection<Plugin>
	{
		public void ConfigureServices(IServiceCollection serviceCollection)
		{
			serviceCollection.AddSingleton<ModuleRank>();
			serviceCollection.AddSingleton<ModuleStat>();
			serviceCollection.AddSingleton<ModuleTime>();
		}
	}
}
