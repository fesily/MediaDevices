// See https://aka.ms/new-console-template for more information
using MediaDevices;
using SwitchWpd;
//序列号
var SerialNumber = Config.SWITCH_ID;
if (SerialNumber == null)
{
    throw new ArgumentNullException("Need SWITCH_ID");
}
Console.WriteLine($"SWITCH ID : \t {SerialNumber}");

if (TilesManager.Root == null)
{
    throw new Exception("Need SWITCH_ROOT");
}

Console.WriteLine($"Game Root : \t {TilesManager.Root}");


DiskTarget diskTarget = Config.DISK_TARGET;

Console.WriteLine($"DiskTarget : \t {diskTarget}");

bool NeedRandomInstaller = Config.RANDOM;
Console.WriteLine($"Need Random Installer : {NeedRandomInstaller}");

TilesManager.EnumRoot();

DBInfo.ReadGameDBInfo();

for (int i = 0; i < 64; i++)
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
        Console.WriteLine($"FirmwareVersion: {driver.FirmwareVersion}");
        try
        {
            var @switch = new Switch(driver);

            var installed = @switch.ReadInstalledGames().Select(x => x.TileId).ToList();

            if (NeedRandomInstaller)
            {
                var ran = @switch.CreateRandomGames();
                @switch.WriteTargetFile(ran);
            }
            var targettileids = @switch.ReadTarget(Environment.GetEnvironmentVariable("TARGET_FILE"));

            var target = targettileids.Where(x =>
            {
                if (installed != null && installed.Contains(x))
                {
                    Console.WriteLine($"[WARN] SKIP GAME HAD INSTALLED!{DBInfo.GetChName(x)}");
                    return false;
                }
                return true;
            }).ToHashSet();

            long target_mb = target.Select(x => TilesManager.Instance.tileId2Path[x]).Sum(p => new FileInfo(p).Length) / 1024 / 1024;
            long installed_mb = 0;
            TimeSpan spentTime = new TimeSpan();
            var count = 0;
            foreach (var id in target)
            {
                count++;
                var filename = TilesManager.Instance.tileId2Path[id];
                try
                {
                    var start = DateTime.Now;
                    Console.WriteLine($"[{start}][{count}/{target.Count}][{installed_mb}MB/{target_mb}MB][upload]\t{filename}\t[{(target_mb - installed_mb) / (installed_mb / spentTime.TotalSeconds) / 60}M]");
                    driver.UploadFile(filename, DiskPath.Join(diskTarget == DiskTarget.SD ? DiskPath.Type.SD_Card_install : DiskPath.Type.NAND_install, Path.GetFileName(filename)));
                    spentTime += DateTime.Now - start;
                    installed_mb += new FileInfo(filename).Length / 1024 / 1024;
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
            if (@switch.FindAllTarget)
            {
                @switch.CompleteTarget();
            }
            return;
        }
        catch (System.Runtime.InteropServices.COMException e)
        {
            Switch.Reset(driver.PnPDeviceID);
            float ms = 5000 + (int)(i * Random.Shared.NextSingle());
            Console.WriteLine($"[ERROR] 失败{e.Message},等待{ms / 1000}S");
            Thread.Sleep((int)ms);
        }
        finally
        {
            driver.Disconnect();
        }
    }
}
