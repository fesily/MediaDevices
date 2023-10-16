// See https://aka.ms/new-console-template for more information
using MediaDevices;
using SwitchWpd;

//序列号
var SerialNumber = Environment.GetEnvironmentVariable("SWITCH_ID");
if (SerialNumber == null)
{
    throw new ArgumentNullException("Need SWITCH_ID");
}
Console.WriteLine($"SWITCH ID : \t {SerialNumber}");

TilesManager.Root = "G:\\switch";
Console.WriteLine($"Game Root : \t {TilesManager.Root}");


var env_disk_target = Environment.GetEnvironmentVariable("DISK_TARGET");
DiskTarget diskTarget = env_disk_target != null ? Enum.Parse<DiskTarget>(env_disk_target) : DiskTarget.SD;

Console.WriteLine($"DiskTarget : \t {diskTarget}");

TilesManager.EnumRoot();

DBInfo.ReadGameDBInfo();

bool last_failed = false;
List<string>? installed = null;
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

            if (installed == null)
            {
                installed = @switch.ReadInstalledGames().SelectMany(x =>
                {
                    if (x.Version != "" && int.Parse(x.Version) > 0)
                    {
                        // 创建另外一个update的信息
                        // TODO version更新需要处理
                        var prefix = x.TileId.Substring(0, x.TileId.Length - 3);
                        var info = TilesManager.Instance.AllGames.Find(g => g.tileid == x.TileId);
                        if (info != null)
                        {
                            var upd_id = info.allTitleIds.FirstOrDefault(id =>
                          id.StartsWith(prefix) && id != x.TileId);
                            if (upd_id != null)
                            {
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
                    }

                    return new InstalledGameInfo[1] { x };
                }).Select(x => x.TileId).ToList();

            }

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
            long left_target_mb = target.Select(x => TilesManager.Instance.tileId2Path[x]).Sum(p => new FileInfo(p).Length) / 1024 / 1024;
            long installed_mb = 0;
            TimeSpan spentTime = new TimeSpan();
            var count = 0;
            foreach (var id in target)
            {
                count++;
                var filename = TilesManager.Instance.tileId2Path[id];
                try
                {
                    installed_mb += new FileInfo(filename).Length / 1024 / 1024;
                    var start = DateTime.Now;
                    Console.WriteLine($"[{start}][{count}/{target.Count}][{installed_mb}MB/{left_target_mb}MB][{(left_target_mb / installed_mb) * spentTime.Minutes}M][upload]\t{filename}");
                    driver.UploadFile(filename, DiskPath.Join(diskTarget == DiskTarget.SD ? DiskPath.Type.SD_Card_install : DiskPath.Type.NAND_install, Path.GetFileName(filename)));
                    spentTime += DateTime.Now - start;
                    installed.Add(id);
                }
                catch (System.IO.IOException e)
                {
                    if (e.Message.EndsWith("already exists"))
                    {
                        Console.WriteLine($"[WARN] SKIP GAME HAD INSTALLED!{DBInfo.GetChName(id)}");
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (System.Runtime.InteropServices.COMException e)
                {
                    if ((uint)e.HResult == 0x80070070)
                    {
                        Console.WriteLine($"switch {diskTarget} 空间不足");
                        if (diskTarget == DiskTarget.All)
                        {
                            Console.WriteLine("$ 准备切换到 nand");

                            diskTarget = DiskTarget.Nand;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            Console.WriteLine("Complete!");
            return;
        }
        catch (System.Runtime.InteropServices.COMException e)
        {
            driver.ResetDevice();
            if ((uint)e.HResult == 0x8007001)
            {
                Console.WriteLine("switch 断开链接, 等待重试");
                Thread.Sleep(1000);
                last_failed = true;
            }

            else
            {
                Console.WriteLine($"[ERROR] 失败{e.Message},等待5S");
                Thread.Sleep(5000);
                last_failed = false;
            }
        }
        finally
        {
            driver.Disconnect();
        }
    }
}
public enum DiskTarget
{
    All,
    Nand,
    SD,
};