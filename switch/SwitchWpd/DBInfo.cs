using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using SwitchWpd;
using System.Globalization;

public class DBInfo
{
    [Name("Title ID")]
    public string TitleID { get; set; }
    [Name("游戏名称")]
    public string Name { get; set; }
    [Name("游戏中文名称")]
    public string CH_NAME { get; set; }
    [Name("发布日期")]
    public string Publish { get; set; }
    [Name("支持中文")]
    public string SupportCh { get; set; }
    [Name("nswdb ID")]
    public string NswDb { get; set; }
    [Name("最新版本")]
    public ulong Version
    {
        get; set;
    }
    [Name("DLC数量")]
    public string DLC_Num { get; set; }

    public static List<DBInfo> infos { get; private set; }
    public static void ReadGameDBInfo()
    {
        using (var ss = new FileStream(Config.GetConfig("SWITCH_DB") ?? Path.Join(Directory.GetCurrentDirectory(), "db.csv"), FileMode.Open))
        {
            using (TextReader reader = new StreamReader(ss))
            {
                var csv = new CsvHelper.CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
                infos = csv.GetRecords<DBInfo>().ToList();
            }
        }
    }
    public static string GetName(string id)
    {
        var info = infos?.Find(x => x.TitleID == id);
        return info?.CH_NAME ?? info?.Name ?? id;
    }
}