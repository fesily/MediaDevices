// See https://aka.ms/new-console-template for more information
using MediaDevices;
using SwitchWpd;
using System.Diagnostics;

if (Config.Roots == null || Config.Roots.Length == 0)
{
    throw new Exception("Need SWITCH_ROOT");
}

Console.WriteLine($"Game Root : \t {Config.Roots}");


DiskTarget diskTarget = Config.DISK_TARGET;

Console.WriteLine($"DiskTarget : \t {diskTarget}");

bool NeedRandomInstaller = Config.RANDOM;
Console.WriteLine($"Need Random Installer : {NeedRandomInstaller}");

TilesManager.EnumRoot();

//DBInfo.ReadGameDBInfo();

//序列号
MediaDevice? device;
var SerialNumber = Config.SWITCH_ID;
if (SerialNumber == null)
{
    var devices = MediaDevice.GetDevices().Where(x => x.FriendlyName == "Switch").Where(x =>
     {
         x.Connect();
         return !Mutex.TryOpenExisting(x.SerialNumber, out Mutex? _);
     }).ToList();
    if (devices.Count > 1)
    {
        // fork new process

        foreach (var d in devices)
        {
            var info = new ProcessStartInfo();
            info.Environment.Add("SWITCH_ID", d.SerialNumber);
            info.WorkingDirectory = Directory.GetCurrentDirectory();
            info.UseShellExecute = true;
            info.FileName = Process.GetCurrentProcess().MainModule.FileName;
            Process.Start(info);
        }
        return;
    }

    if (devices.Count == 0)
    {
        Console.WriteLine("can't find any switch");
        return;
    }

    device = devices[0];
    SerialNumber = device.SerialNumber;
    if (SerialNumber == null)
    {
        Console.WriteLine("can't find any switch");
        return;
    }
}
else
{
    device = MediaDevice.GetDevices().Where(x => x.FriendlyName == "Switch").First(x =>
    {
        x.Connect();
        return x.SerialNumber == SerialNumber;
    });
}
Console.WriteLine($"SWITCH ID : \t {SerialNumber}");

if (device == null)
{
    Console.WriteLine($"[ERROR] can't find switch :{SerialNumber}");
    return;
}

StartOne(device, SerialNumber);

bool StartOne(MediaDevice device, string SerialNumber)
{
    var mtx = new Mutex(false, SerialNumber);

    if (!mtx.WaitOne(1000))
    {
        Console.WriteLine($"[WARN]{SerialNumber} Can't get mutex");
        return false;
    }
    try
    {
        var failedList = new Dictionary<string, int>();

        for (int i = 0; i < 64; i++)
        {
            device.Connect();
            Console.WriteLine($"FirmwareVersion: {device.FirmwareVersion}");
            try
            {
                var @switch = new SwitchWpd.Switch(device);

                var installed = @switch.ReadInstalledGames().Select(x => x.TileId).ToList();
                if (Config.INSTALLED_FILE_PATH?.Length > 0 && File.Exists(Config.INSTALLED_FILE_PATH))
                {
                    Console.WriteLine($"Read installed file from: {Config.INSTALLED_FILE_PATH}");
                    var customInstalled = @switch.ReadInstalledGames(Config.INSTALLED_FILE_PATH).Select(x=>x.TileId).ToList();
                    if (customInstalled != null)
                    {
                        installed = installed != null ? installed.Union(customInstalled).ToList() : customInstalled;
                    }
                }

                string[]? targetIDs = null;
                try
                {
                    targetIDs = @switch.ReadTarget(Environment.GetEnvironmentVariable("TARGET_FILE"));
                }
                catch (FileNotFoundException)
                {
                    if (!NeedRandomInstaller)
                    {
                        throw;
                    }
                }

                if (NeedRandomInstaller)
                {
                    var ran = @switch.CreateRandomGames(@switch.AppIds?.ToArray());
                    try
                    {
                        @switch.WriteTargetFile(ran, true);
                        targetIDs = @switch.ReadTarget(Environment.GetEnvironmentVariable("TARGET_FILE"));
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine($"[ERROR] create random failed: {e.Message}");
                    }
                }
                Debug.Assert(targetIDs != null);
                var target = targetIDs.Where(x =>
                {
                    if (installed != null && installed.Contains(x))
                    {
                        Console.WriteLine($"[WARN] SKIP GAME HAD INSTALLED!{DBInfo.GetName(x)}");
                        return false;
                    }
                    return true;
                }).ToHashSet().ToList();

                long target_mb = target.Select(x => TilesManager.Instance.tileId2Path[x]).Sum(p => new FileInfo(p).Length) / 1024 / 1024;
                long installed_mb = 0;
                TimeSpan spentTime = new TimeSpan();
                var count = 0;
                foreach (var id in target)
                {
                    if (failedList.ContainsKey(id) && failedList[id] >= 2)
                    {
                        Console.WriteLine($"[WARN] SKIP FAILED GAME: {DBInfo.GetName(id)}");
                        continue;
                    }
                    count++;
                    var filename = TilesManager.Instance.tileId2Path[id];
                    try
                    {
                        var start = DateTime.Now;
                        Console.WriteLine($"[{start}][{count}/{target.Count}][{installed_mb}MB/{target_mb}MB][upload]\t{filename}\t[{(target_mb - installed_mb) / (installed_mb / spentTime.TotalSeconds) / 60}M]");
                        device.UploadFile(filename, DiskPath.Join(diskTarget == DiskTarget.Nand ? DiskPath.Type.NAND_install : DiskPath.Type.SD_Card_install, Path.GetFileName(filename)));
                        spentTime += DateTime.Now - start;
                        installed_mb += new FileInfo(filename).Length / 1024 / 1024;
                        installed.Add(id);
                    }
                    catch (IOException e)
                    {
                        if (e.Message.EndsWith("already exists"))
                        {
                            Console.WriteLine($"[WARN] SKIP GAME HAD INSTALLED!{DBInfo.GetName(id)}");
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
                                Console.WriteLine("$ 准备切换到 NAND");

                                diskTarget = DiskTarget.Nand;
                            }
                            else
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (failedList.ContainsKey(id))
                                failedList[id]++;
                            else
                                failedList.Add(id, 1);
                            throw;
                        }
                    }
                }
                Console.WriteLine("Complete!");
                if (@switch.FindAllTarget)
                {
                    @switch.CompleteTarget();
                }
                if (NeedRandomInstaller) return true;

                if (Environment.GetEnvironmentVariable("NO_RANDOM_FULL") == null)
                    return true;
                NeedRandomInstaller = true;
                mtx.ReleaseMutex();
                mtx = null;
                Console.WriteLine("[INFO] Random full emmc");
                StartOne(device, SerialNumber);
                return true;
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
                SwitchWpd.Switch.Reset(device.PnPDeviceID);
                float ms = 5000 + (int)(i * Random.Shared.NextSingle());
                Console.WriteLine($"[ERROR] 失败{e.Message},等待{ms / 1000}S");
                Thread.Sleep((int)ms);
            }
            finally
            {
                device.Disconnect();
            }
        }
        return false;
    }
    finally
    {
        if (mtx != null)
            mtx.ReleaseMutex();
    }
}