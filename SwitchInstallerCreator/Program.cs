using SwitchWpd;
using System.Globalization;
using System.Numerics;

DBInfo.ReadGameDBInfo();
TilesManager.Instance = new TilesManager()
{

    zh2TitleId = DBInfo.infos.GroupBy(g => g.CH_NAME).ToDictionary(i => i.Key, i => i.Select(j => j.TitleID).ToArray()),
};

var file = args[0];
var targetids = new List<string>();
foreach (var p in File.ReadAllLines(file))
{
    try
    {
        BigInteger i = BigInteger.Parse(p, NumberStyles.HexNumber);
        if (i > 0)
        {
            targetids.Add(p);
            continue;
        }
    }
    catch (FormatException)
    {
        if (TilesManager.Instance.TryGetTitleIdFilesByName(p, out string[] ids))
        {
            foreach (var id in ids)
            {
                targetids.Add(id);
            }
            continue;
        }
    }

    Console.WriteLine($"unkown item : {p}");
    targetids.Add(p);
}
var target_path = Path.Join(Path.GetDirectoryName(file), "installer");
Console.WriteLine($"Write Installer :{target_path}");
File.WriteAllLines(target_path, targetids);
Console.ReadKey();
