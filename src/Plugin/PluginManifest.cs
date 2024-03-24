namespace K4System
{
    using CounterStrikeSharp.API.Core;

    public sealed partial class Plugin : BasePlugin
    {
        public override string ModuleName => "K4-System";

        public override string ModuleDescription => "Enhances the server with playtime tracking, statistics, commands, and player ranks.";

        public override string ModuleAuthor => "K4ryuu";

        public override string ModuleVersion => "4.0.0 " +
#if RELEASE
            "(release)";
#else
            "(debug)";
#endif
    }
}