using System.Globalization;
using System.Numerics;

namespace SwitchWpd
{
    public class TilesManager
    {
        public static string Root { get; set; }
        public static string? GetTileId(string filename) => Path.GetFileName(filename).Split('[', ']').Where((x) =>
        {
            try
            {
                BigInteger o = BigInteger.Parse(x, NumberStyles.HexNumber);
                return o > 0;
            }
            catch (FormatException)
            {

                return false;
            }

        }).FirstOrDefault();
        public static TilesManager Instance { get; set; }
        public Dictionary<string, string> en2TitleId { get; private set; }
        public Dictionary<string, string> zh2TitleId { get; set; }
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
            if (Root == null)
            {
                Root = Environment.GetEnvironmentVariable("SWITCH_ROOT");
                if (Root == null)
                {
                    throw new Exception("Need SWITCH_ROOT");
                }
            }
            List<RootGameInfo> games = new List<RootGameInfo>();
            Dictionary<string, string> TileId2Path = new Dictionary<string, string>();
            foreach (var dir in Directory.EnumerateDirectories(Root))
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
            Dictionary<string, string> zh2TitleId = games.Where(g => !string.IsNullOrEmpty(g.ch_name)).ToDictionary(g => g.ch_name, g => g.tileid); ;
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
            Console.WriteLine($"[ERROR] NO GAME!{DBInfo.GetChName(tileId)}");
            return Array.Empty<string>();
        }
        public bool TryGetTitleIdFilesByName(string name, out string titleId)
        {
            Func<string, int> match_score = x => x.Intersect(name).Count(c => !char.IsSeparator(c));
            var info = TilesManager.Instance.zh2TitleId.OrderByDescending(x => match_score(x.Key)).First();
            if (match_score(info.Key) > 2)
            {
                Console.WriteLine($"{name} matched {info.Key}, try use {info.Value}");
                titleId = info.Value;
                return true;
            }
            titleId = "";
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