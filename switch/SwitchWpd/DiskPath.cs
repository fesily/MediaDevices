namespace SwitchWpd
{
    public static class DiskPath
    {
        public enum Type
        {
            Unknown,
            SD_Card,
            Nand_User,
            Nand_System,
            Installed_Games,
            SD_Card_install,
            NAND_install,
            Saves,
            Album,
            Homebrew,
            Screenshots,
        }
        public const string DiskSDCard = "1: SD Card";
        public const string DiskNandUser = "2: Nand USER";
        public const string DiskNandSystem = "3: Nand SYSTEM";
        public const string DiskInstalledGames = "4: Installed games";
        public const string DiskSDCardInstall = "5: SD Card install";
        public const string DiskNandInstall = "6: NAND install";
        public const string DiskSaves = "7: Saves";
        public const string DiskAlbum = "8: Album";
        public const string DiskHomebrew = "Homebrew";
        public const string DiskScreenshots = "Screenshots";

        public static string ToDisk(Type type)
        {
            switch (type)
            {
                case Type.SD_Card: return DiskSDCard;
                case Type.Nand_User: return DiskNandUser;
                case Type.Nand_System: return DiskNandSystem;
                case Type.Installed_Games: return DiskInstalledGames;
                case Type.SD_Card_install: return DiskSDCardInstall;
                case Type.NAND_install: return DiskNandInstall;
                case Type.Saves: return DiskSaves;
                case Type.Album: return DiskAlbum;
                case Type.Homebrew: return DiskHomebrew;
                case Type.Screenshots: return DiskScreenshots;
                case Type.Unknown:
                default:
                    return "unkown";
            }
        }

        public static string Join(Type tt, params string[] path)
        {
            var newPaths = new string[path.Length + 2];
            newPaths[0] = "\\";
            newPaths[1] = ToDisk(tt);
            for (int i = 0; i < path.Length; i++)
            {
                newPaths[i + 2] = path[i];
            }
            return Path.Join(newPaths);
        }
    }
}