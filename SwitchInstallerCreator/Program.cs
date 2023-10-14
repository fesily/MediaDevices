using SwitchWpd;
using System.Globalization;
using System.Numerics;

DBInfo.ReadGameDBInfo();
TilesManager.Instance = new TilesManager()
{

    zh2TitleId = DBInfo.infos.Where((x, i) => DBInfo.infos.FindIndex(z => z.CH_NAME == x.CH_NAME) == i).ToDictionary(i => i.CH_NAME, i => i.TitleID),
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
        if (TilesManager.Instance.TryGetTitleIdFilesByName(p, out string id))
        {
            targetids.Add(id);
            continue;
        }
    }

    Console.WriteLine($"unkown item : {p}");
    targetids.Add(p);
}
File.WriteAllLines(file, targetids);
