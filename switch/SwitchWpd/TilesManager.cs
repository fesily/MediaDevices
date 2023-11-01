namespace SwitchWpd
{
    public class TilesManager
    {
        public static string? GetTileId(string filename) => Path.GetFileName(filename).Split('[', ']').Where((x) =>
        {
            return (x.Length == 16 || x.Length == 18) && x.All(char.IsAsciiHexDigit);

        }).FirstOrDefault();
        public static TilesManager Instance { get; set; }
        public Dictionary<string, string> en2TitleId { get; private set; }
        public Dictionary<string, string[]> zh2TitleId { get; set; }
        public Dictionary<string, string> tileId2Path { get; private set; }
        public Dictionary<string, string> AppTileId2Path { get; private set; }
        public List<RootGameInfo> AllGames { get; private set; }

        public static string[] ValidExtensions = { ".nsz", ".xci", ".nsp", ".xcz" };
        public static IEnumerable<string> EnumerateFiles(string directory)
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(x => ValidExtensions.Contains(Path.GetExtension(x).ToLower()));
        }
        public static void EnumRoot()
        {
            foreach (var root in Config.Roots)
            {
                EnumRoot(root);
            }
        }
        public static void EnumRoot(string root)
        {
            List<RootGameInfo> games = new List<RootGameInfo>();
            Dictionary<string, string> TileId2Path = new Dictionary<string, string>();
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var filename = Path.GetFileName(dir);
                if (!filename.StartsWith("["))
                    continue;
                var sp = filename.Split('[', ']');
                var ch_name = sp[1];
                var en_name = sp[3];
                var tileid = sp[5];

                var allTitleIds = new List<string>();
                var length = EnumerateFiles(dir).Sum(path =>
                {
                    var id = GetTileId(path);
                    if (id == null)
                        return 0;
                    allTitleIds.Add(id);
                    TileId2Path[id] = path;
                    return new FileInfo(path).Length;
                });

                games.Add(new RootGameInfo
                {
                    ch_name = ch_name,
                    en_name = en_name,
                    tileid = tileid,
                    length = length,
                    dir_path = dir,
                    allTitleIds = allTitleIds.ToArray()
                });

            }

            Dictionary<string, string> en2TitleId = games.Where(g => !string.IsNullOrEmpty(g.en_name)).ToDictionary(g => g.en_name, g => g.tileid);
            Dictionary<string, string[]> zh2TitleId = games.Where(g => !string.IsNullOrEmpty(g.ch_name)).GroupBy(g => g.ch_name).ToDictionary(g => g.Key, g => g.Select(g1 => g1.tileid).ToArray()); ;
            Dictionary<string, string> AppTileId2Path = games.ToDictionary(g => g.tileid, g => g.dir_path);

            Instance = new TilesManager()
            {
                en2TitleId = en2TitleId,
                zh2TitleId = zh2TitleId,
                tileId2Path = TileId2Path,
                AppTileId2Path = AppTileId2Path,
                AllGames = games,
            };
        }
        public IEnumerable<string> EnumAppTileIdFilesID(string tileId)
        {
            if (AppTileId2Path.TryGetValue(tileId, out var dir))
            {
                return EnumerateFiles(dir).Select(GetTileId);
            }
            Console.WriteLine($"[ERROR] NO GAME!{DBInfo.GetName(tileId)}:{tileId}");
            return Array.Empty<string>();
        }
        public bool TryGetTitleIdFilesByName(string name, out string[] ids)
        {
            Func<string, int> match_score = x => x.Intersect(name).Count(c => !char.IsSeparator(c));
            var infos = Instance.zh2TitleId.OrderByDescending(x => match_score(x.Key)).Take(5).OrderBy(x =>
            {
                var i = x.Key.IndexOf(name[0]);
                return i == -1 ? 99999 : i;
            }).ToArray();

            foreach (var info in infos)
            {
                if (match_score(info.Key) == name.Intersect(name).Where(c => !char.IsSeparator(c)).Count())
                {
                    Console.WriteLine($"{name} matched {info.Key}, try use {Path.Join(info.Value)}");
                    ids = info.Value;
                    return true;
                }
            }
            ids = Array.Empty<string>();
            return false;
        }
        public class RootGameInfo
        {
            public string ch_name;
            public string en_name;
            public string tileid;
            public Int64 length;
            public string dir_path;
            public string[] allTitleIds;
        }
    }
}