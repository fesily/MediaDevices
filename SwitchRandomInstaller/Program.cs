// See https://aka.ms/new-console-template for more information
using MediaDevices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

var Root = Environment.GetEnvironmentVariable("ROOT") ?? Path.Join("G:\\", "switch");

//序列号
var SwitchId = Environment.GetEnvironmentVariable("SWITCH_ID") ?? Random.Shared.NextInt64().ToString();
var DBDir = Path.Join(Root, "..", "switch_install_db");

List<GameInfo> games = new List<GameInfo>();
foreach (var dir in Directory.EnumerateDirectories(Root))
{
    if (!dir.StartsWith("["))
        continue;
    var sp = dir.Split('[', ']');
    var ch_name = sp[0];
    var en_name = sp[1];
    var tileid = sp[2];

    var length = new DirectoryInfo(dir).EnumerateFiles().Sum(i => i.Length);

    games.Add(new GameInfo
    {
        ch_name = ch_name,
        en_name = en_name,
        tileid = tileid,
        length = length,
        dir_path = dir
    });
}
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

ListRandom(games);

using (var driver = MediaDevices.MediaDevice.GetDevices().First(x => x.DeviceId == SwitchId))
{
    driver.Connect();

    var db = Path.Join("1: SD Card", "installed.json");
    List<string> installed;
    using (var ss = new MemoryStream())
    {
        driver.DownloadFile(db, ss);
        installed = JsonSerializer.Deserialize<List<string>>(ss) ?? new List<string>();
    }
    //TODO 
    long left_memory = 0;

    List<GameInfo> target = games.SkipWhile(x=> installed.Contains(x.tileid)). TakeWhile(x =>
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
        using( var ss = new MemoryStream()) 
        {
            JsonSerializer.Serialize(ss, installed);
            using ( var ws = new MemoryStream(ss.GetBuffer()))
            {
                driver.UploadFile(ws, db);
            }
        }
    }
}

public class GameInfo
{
    public string ch_name;
    public string en_name;
    public string tileid;
    public Int64 length;
    public string dir_path;
}