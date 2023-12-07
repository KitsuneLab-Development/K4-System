namespace K4System
{
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;

    using Microsoft.Extensions.Logging;
    using MySqlConnector;
    using Nexd.MySQL;

    [MinimumApiVersion(112)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public required MySqlDb Database { get; set; }
        public required string _ModuleDirectory { get; set; }

        //** ? MODULES */
        private readonly IModuleRank ModuleRank;
        private readonly IModuleStat ModuleStat;
        private readonly IModuleTime ModuleTime;

        public Plugin(ModuleRank moduleRank, ModuleStat moduleStat, ModuleTime moduleTime)
        {
            this.ModuleRank = moduleRank;
            this.ModuleStat = moduleStat;
            this.ModuleTime = moduleTime;
        }

        public void OnConfigParsed(PluginConfig config)
        {
            if (config.Version < Config.Version)
            {
                base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);
            }

            config.GeneralSettings.Prefix = ApplyPrefixColors(config.GeneralSettings.Prefix);

            DatabaseSettings dbSettings = config.DatabaseSettings;

            string connectionString = @$"Server={dbSettings.Host};Database={dbSettings.Database};Port={dbSettings.Port};Uid={dbSettings.Username};Password={dbSettings.Password};SslMode=none;";

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                }
            }
            catch (MySqlException ex)
            {
                base.Logger.LogError($"Connection to the MySQL database failed: {ex.Message}");
            }

            //** ? Database Connection */

            Database = new MySqlDb(dbSettings.Host, dbSettings.Username, dbSettings.Password, dbSettings.Database, dbSettings.Port);

            //** ? Save Config */

            this.Config = config;
        }

        public override void Load(bool hotReload)
        {
            _ModuleDirectory = ModuleDirectory;

            //** ? Core */

            Initialize_Events();
            Initialize_Commands();

            //** ? Initialize Modules */

            if (Config.GeneralSettings.ModuleRanks)
                this.ModuleRank.Initialize(hotReload);

            if (Config.GeneralSettings.ModuleStats)
                this.ModuleStat.Initialize(hotReload);

            if (Config.GeneralSettings.ModuleTimes)
                this.ModuleTime.Initialize(hotReload);
        }

        public override void Unload(bool hotReload)
        {
            //** ? Release Modules */

            if (Config.GeneralSettings.ModuleRanks)
                this.ModuleRank.Release(hotReload);

            if (Config.GeneralSettings.ModuleStats)
                this.ModuleStat.Release(hotReload);

            if (Config.GeneralSettings.ModuleTimes)
                this.ModuleTime.Release(hotReload);
        }
    }
}