using IniParser;
using IniParser.Model;
namespace SwitchWpd
{
    public static class Config
    {
        static IniData IniData;
        static KeyDataCollection InitDataDefault => IniData["Default"];
        static Config()
        {
            var parser = new FileIniDataParser();
            IniData = parser.ReadFile(Environment.GetEnvironmentVariable("CONFIG_PATH")?? "config.ini");
        }
        public static string GetConfig(string key) => Environment.GetEnvironmentVariable(key) ?? InitDataDefault[key];
        public static string Root => GetConfig("SWITCH_ROOT");
        public static string SWITCH_ID => GetConfig("SWITCH_ID");
        public static DiskTarget DISK_TARGET
        {
            get
            {
                var env = GetConfig("DISK_TARGET");
                return env != null ? Enum.Parse<DiskTarget>(env) : DiskTarget.SD;
            }
        }
        public static bool RANDOM => GetConfig("RANDOM") != null;

    }
    public enum DiskTarget
    {
        All,
        Nand,
        SD,
    };
}
