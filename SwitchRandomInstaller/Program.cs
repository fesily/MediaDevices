// See https://aka.ms/new-console-template for more information
using MediaDevices;
using SwitchWpd;

//序列号
var SerialNumber = Environment.GetEnvironmentVariable("SWITCH_ID") ?? "XKC10008452541" ?? Random.Shared.NextInt64().ToString();

TilesManager.Root = "E:\\switch";
TilesManager.EnumRoot();

DBInfo.ReadGameDBInfo();

while (true)
{
    using (var driver = MediaDevice.GetDevices().First(x =>
    {
        x.Connect();
        return x.SerialNumber == SerialNumber;
    }))
    {
        if (driver == null)
        {
            throw new NotConnectedException($"无法找到{SerialNumber}");
        }
        try
        {
            var @switch = new SwitchWpd.Switch(driver);

            var installed = @switch.ReadInstalledGames().SelectMany(x =>
            {
                if (x.Version != "" && int.Parse(x.Version) > 0)
                {
                    // 创建另外一个update的信息
                    // TODO version更新需要处理
                    var prefix = x.TileId.Substring(0, x.TileId.Length - 4);
                    var info = TilesManager.Instance.AllGames.Find(g => g.tileid == x.TileId);
                    if (info != null)
                    {
                        var upd_id = info.allTitleIds.First(id =>
                      id.StartsWith(prefix));
                        x.Version = "0";
                        return new InstalledGameInfo[2]
                        {
                        x,
                        new InstalledGameInfo
                        {
                            TileId = upd_id,
                            Name=x.Name,
                            Version = x.Version,
                        }
                        };
                    }
                }

                return new InstalledGameInfo[1] { x };
            }).Select(x => x.TileId).ToList();

            string[]? targettileids = null;
            try
            {
                targettileids = @switch.ReadTarget(Environment.GetEnvironmentVariable("TARGET_FILE"));
            }
            catch (FileNotFoundException)
            {
                var ran = @switch.CreateRandomGames();
                @switch.WriteTargetFile(ran);
                targettileids = ran;
            }
            var target = targettileids.Where(x =>
            {
                if (installed != null && installed.Contains(x))
                {
                    Console.WriteLine($"[WARN] SKIP GAME HAD INSTALLED!{DBInfo.GetChName(x)}");
                    return false;
                }
                return true;
            }).ToHashSet();

            foreach (var id in target)
            {
                var filename = TilesManager.Instance.tileId2Path[id];
                var startTime = DateTime.Now;
                Console.WriteLine($"[{startTime}][upload]\t{filename}");
                driver.UploadFile(filename, DiskPath.Join(DiskPath.Type.SD_Card_install, Path.GetFileName(filename)));
                Console.WriteLine($"[{DateTime.Now}][complete]\t {DateTime.Now - startTime}");
                installed.Add(id);
            }
            Console.WriteLine("Complete!");
            return;
        }
        catch (System.Runtime.InteropServices.COMException e)
        {
            driver.ResetDevice();
            if (e.HResult == 0x8007001)
            {
                Console.WriteLine("switch 断开链接, 等待重试");
                Thread.Sleep(1000);
            }
        }
        finally
        {
            driver.Disconnect();
        }
    }
}