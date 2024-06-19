using CsvHelper.Configuration;
using MediaDevices;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text;

namespace SwitchWpd
{
    public class Switch : IDisposable
    {
        public ulong FreeMem
        {
            get
            {
                var Storages = _device.FunctionalObjects(FunctionalCategory.Storage)?.ToList();
                var SelectedStorage = Storages?.FirstOrDefault();
                if (SelectedStorage != null)
                {
                    return _device.GetStorageInfo(SelectedStorage).FreeSpaceInBytes;
                }
                return 0;
            }
        }
        public string SerialNumber
        {
            get
            {
                return _device.SerialNumber;
            }
        }
        ILogger Logger;
        public MediaDevice _device;
        public GameInfo[] target;
        public InstalledGameInfo[] installed;
        public GameInfo[]? left;
        public string ID { get; private set; }
        public Switch(MediaDevice device)
        {
            _device = device;
            ID = _device.DeviceId;
        }
        public const string hash_name = "installer_hash";
        public string installer_path { get; private set; } = DiskPath.Join(DiskPath.Type.SD_Card, "installer");
        private InstalledGameInfo[] ReadInstalledGameFile()
        {
            try
            {
                var p = DiskPath.Join(DiskPath.Type.Installed_Games, "InstalledApplications.csv");
                using (var ss = new MemoryStream())
                {
                    _device.DownloadFile(p, ss);
                    ss.Seek(0, SeekOrigin.Begin);
                    using (TextReader reader = new StreamReader(ss))
                    {
                        var csvReader = new CsvHelper.CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                        {
                            HasHeaderRecord = false,
                        });

                        installed = csvReader.GetRecords<InstalledGameInfo>().Select((x) =>
                        {
                            x.TileId = x.TileId.Substring(2);
                            return x;
                        }).ToHashSet().ToArray();
                        return installed;
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"[ERROR] Not find Installed Games Information, entry any key to exit");
                Console.ReadKey();
                Environment.Exit(1);
                throw;
            }
        }
        private InstalledGameInfo[] ReadInstalledGameFileFromPath(string p)
        {
            try
            {
                using (var ss = File.OpenRead(p))
                {
                    using (TextReader reader = new StreamReader(ss))
                    {
                        var csvReader = new CsvHelper.CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                        {
                            HasHeaderRecord = false,
                        });

                        installed = csvReader.GetRecords<InstalledGameInfo>().Select((x) =>
                        {
                            x.TileId = x.TileId.Substring(2);
                            return x;
                        }).ToHashSet().ToArray();
                        return installed;
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                throw;
            }
        }
        public IEnumerable<InstalledGameInfo> ReadInstalledGames(string p = "")
        {
            var files = p.Length == 0? ReadInstalledGameFile(): ReadInstalledGameFileFromPath(p);
            return files.SelectMany(x =>
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
            });
        }
        public bool FindAllTarget { get; private set; } = false;
        public HashSet<string> AppIds { get; private set; }
        public string[] ReadTarget(string? target_file)
        {
            using (var ss = new MemoryStream())
            {
                if (target_file != null)
                    installer_path = target_file;
                _device.DownloadFile(installer_path, ss);
                ss.Seek(0, SeekOrigin.Begin);
                var reader = new StreamReader(ss);
                var helper = new ReadTargetHelper();
                var rets = helper.ReadTargetFromStream(reader);
                foreach (var id in helper.appIds)
                {
                    Console.WriteLine($"Use App :{DBInfo.GetName(id)}");
                }
                if (helper.NeedUpdate)
                {
                    WriteTargetFile(helper.appIds.ToArray(), true);
                }
                FindAllTarget = helper.MissCount == 0;
                AppIds = helper.appIds;
                return rets;
            }
        }

        public void CompleteTarget()
        {
            _device.DeleteFile(installer_path);
        }
        public static void KnuthDurstenfeldShuffle<T>(List<T> list)
        {
            //随机交换
            int currentIndex;
            T tempValue;
            for (int i = 0; i < list.Count; i++)
            {
                currentIndex = Random.Shared.Next(list.Count - i);
                tempValue = list[currentIndex];
                list[currentIndex] = list[list.Count - 1 - i];
                list[list.Count - 1 - i] = tempValue;
            }
        }
        public string[] CreateRandomGames(string[]? targetIDs)
        {
            var left_memory = (long)FreeMem;

            if (targetIDs != null)
            {
                left_memory -= targetIDs.SelectMany(TilesManager.Instance.EnumAppTileIdFilesID).Select(x => TilesManager.Instance.tileId2Path[x]).Select(x => new FileInfo(x).Length).Sum();
                if (left_memory <= 0)
                {
                    return targetIDs;
                }
            }
            Console.WriteLine($"[INFO] Create Random Game List : Left Memory {left_memory / 1024 / 1024} MB");

            var games = TilesManager.Instance.AllGames.ToList();
            KnuthDurstenfeldShuffle(games);

            var installedIds = installed.Select(x => x.TileId).ToHashSet();

            var arr = games.Where(x => !installedIds.Contains(x.tileid)).Where(x =>
            {
                left_memory -= x.length;
                if (left_memory > 0)
                {
                    var name = string.IsNullOrEmpty(x.ch_name) ? x.en_name : x.ch_name;
                    Console.WriteLine($"[INFO][RANDOM] select {name} : {x.tileid}");
                    return true;
                }
                return false;
            }).Select(g => g.tileid).ToArray();
            if (targetIDs != null)
            {
                return targetIDs.Concat(arr).ToArray();
            }
            return arr;
        }

        public void WriteTargetFile(string[] ids, bool delete_old)
        {
            var builder = new StringBuilder();
            foreach (var item in ids)
            {
                builder.AppendLine(item);
            }
            using (var ss = new MemoryStream(Encoding.Default.GetBytes(builder.ToString())))
            {
                if (delete_old)
                {
                    try
                    {
                        _device.DeleteFile(installer_path);
                    }
                    catch (FileNotFoundException)
                    {

                    }
                }
                _device.UploadFile(ss, installer_path);
            }
        }
        public void Connect()
        {
            if (!_device.IsConnected)
                _device.Connect();

            Func<GameInfo, string> selector = x => x.TileId;
            var a = installed.Select(x => x.TileId);
            left = target?.IntersectBy(a, selector).ExceptBy(a, selector).ToArray();
        }
        public void Upload()
        {
            if (left != null)
            {
                foreach (var info in left)
                {
                    string path;
                    if (!TilesManager.Instance.tileId2Path.TryGetValue(info.TileId, out path))
                    {
                        Console.WriteLine($"[ERROR] tileId2Path NO FILE! {path}");
                        continue;
                    }
                    if (!Directory.Exists(path))
                    {
                        Console.WriteLine($"[ERROR] NO FILE! {path}");
                        continue;
                    }
                    _device.UploadFolder(path, DiskPath.DiskSDCardInstall);
                }
            }
        }
        public void Disconnect()
        {
            _device.Disconnect();
        }
        public void Dispose()
        {
            _device.Dispose();
        }

        public static void Reset(string PnPDeviceID)
        {
            var device = GetDevice(PnPDeviceID);
            if (device != null)
            {
                device.InvokeMethod("Disable", new object[] { false });
                device.InvokeMethod("Enable", new object[] { false });
            }
        }
        static ManagementObject? GetDevice(string pnpId)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity");
            ManagementObjectCollection collection = searcher.Get();

            foreach (ManagementObject device in collection)
            {
                var v = device.Properties["DeviceID"].Value;
                if (v != null)
                {
                    var deviceID = v.ToString().ToLower().Replace('\\', '#');
                    if (pnpId.IndexOf(deviceID, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        return device;
                    }
                }
            }
            return null;
        }
    }
}