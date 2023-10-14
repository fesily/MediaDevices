// See https://aka.ms/new-console-template for more information
using MediaDevices;
using SwitchWpd;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

var Root = Environment.GetEnvironmentVariable("ROOT") ?? Path.Join("G:\\", "switch");

//序列号
var SerialNumber = Environment.GetEnvironmentVariable("SWITCH_ID") ?? "XKC10008452541"?? Random.Shared.NextInt64().ToString();


string GetTileId(string filename)
{
    return Path.GetFileName(filename).Split('[', ']')[5];
}
List<GameInfo> games = new List<GameInfo>();
Dictionary<string, string> TileIdtoPath = new Dictionary<string, string>();
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
    var length = new DirectoryInfo(dir).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories=true}).Sum(i =>
    {
        var id = GetTileId(i.Name);
        allTitleIds.Add(id);
        TileIdtoPath[id] = i.FullName;
        return i.Length;
    });

    games.Add(new GameInfo
    {
        ch_name = ch_name,
        en_name = en_name,
        tileid = tileid,
        length = length,
        dir_path = dir,
        allTitleIds = allTitleIds.ToArray()
    });
}


using (var driver = MediaDevices.MediaDevice.GetDevices().First(x => {
    x.Connect();
    return x.SerialNumber == SerialNumber;
}))
{
    var @switch = new SwitchWpd.Switch(driver);
    try
    {
        var installed = @switch.ReadInstalledGames().Select(x => x.TileId.Substring(2)).ToHashSet();


        var target_file = Environment.GetEnvironmentVariable("TARGET") ?? @"C:\\Users\\fesil\\Downloads\\Untitled-1.ini";
        if (target_file != null)
        {
            using (var ss = new StreamReader(new FileStream(target_file, FileMode.Open)))
            {
                var paths = new HashSet<string>(); 
                while (!ss.EndOfStream) {
                    var p = ss.ReadLine();
                    if (p!=null)
                        paths.Add(p);
                }
                var targettileids = paths.Where(p => Directory.Exists(p)).SelectMany(x => Directory.EnumerateFiles(x, "*", new EnumerationOptions { RecurseSubdirectories=true})).Select(GetTileId).ToHashSet();
               
                var target = games.SelectMany(x => x.allTitleIds).Where(
                    x => targettileids.Contains(x)
                    )
                    .Where(x => !(installed != null && installed.Contains(x))).ToHashSet();
                
                foreach (var id in target)
                {
                    var filename = TileIdtoPath[id];
                    Console.WriteLine($"[{DateTime.Now}]  upload {filename}");
                    //TODO  System.Runtime.InteropServices.COMException 0x8007001F
                    //TODO retry
                    driver.UploadFile(filename,DiskPath.Join(DiskPath.Type.SD_Card_install,Path.GetFileName(filename)));
                    Console.WriteLine($"[{DateTime.Now}]  upload {filename} complete!");
                    installed.Add(id);
                }
            }
        }
        else
        {
            var left_memory = (long)@switch.FreeMem;
            static List<T> ListRandom<T>(List<T> sources)
            {
                var random = new Random();
                var resultList = new List<T>();
                foreach (var item in sources)
                {
                    resultList.Insert(random.Next(resultList.Count), item);
                }
                return resultList;
            }

            var games_Arr = games.ToArray();
            Random.Shared.Shuffle(games_Arr);
            List<GameInfo> target = games_Arr.Where(x => (installed != null && installed.Contains(x.tileid))).Where(x =>
            {
                left_memory -= x.length;
                return left_memory > 0;
            }).ToList();

            foreach (var item in target)
            {
                Console.WriteLine($"[{DateTime.Now}]  upload {item.en_name}");
                driver.UploadFolder(item.dir_path, @"\5: SD Card install");
                Console.WriteLine($"[{DateTime.Now}]  upload {item.en_name} complete!");
                installed.Add(item.tileid);
            }
        }

    }
    finally
    {
        @switch.Disconnect();
    }

}

public class GameInfo
{
    public string ch_name;
    public string en_name;
    public string tileid;
    public Int64 length;
    public string dir_path;
    public string[] allTitleIds;
}