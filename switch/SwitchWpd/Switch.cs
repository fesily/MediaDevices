using CsvHelper.Configuration;
using MediaDevices;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Numerics;
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
        public string installer_path { get; } = DiskPath.Join(DiskPath.Type.SD_Card, "installer");
        public InstalledGameInfo[] ReadInstalledGames()
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

                    return csvReader.GetRecords<InstalledGameInfo>().Select((x) =>
                    {
                        x.TileId = x.TileId.Substring(2);
                        return x;
                    }).ToHashSet().ToArray();
                }
            }
        }
        public string[] ReadTarget(string? target_file)
        {
            using (var ss = new MemoryStream())
            {
                _device.DownloadFile(target_file ?? installer_path, ss);
                ss.Seek(0, SeekOrigin.Begin);
                var reader = new StreamReader(ss);
                HashSet<string> vals = new HashSet<string>();
                while (!reader.EndOfStream)
                {
                    var p = reader.ReadLine();
                    if (p != null)
                    {
                        if (Directory.Exists(p))
                        {
                            var ids = TilesManager.EnumerateFiles(p)
                                .Select(TilesManager.GetTileId);
                            foreach (var item in ids)
                            {
                                vals.Add(item);
                            }
                        }
                        else if (Path.Exists(p))
                        {
                            vals.Add(TilesManager.GetTileId(p));
                        }
                        else
                        {
                            try
                            {
                                BigInteger i = BigInteger.Parse(p, NumberStyles.HexNumber);
                                if (i > 0)
                                {
                                    foreach (var item in TilesManager.Instance.EnumAppTileIdFilesID(p))
                                    {
                                        vals.Add(item);
                                    }
                                    continue;
                                }
                            }
                            catch (FormatException)
                            {
                                if (TilesManager.Instance.TryGetTitleIdFilesByName(p, out string id))
                                {
                                    foreach (var item in TilesManager.Instance.EnumAppTileIdFilesID(id))
                                    {
                                        vals.Add(item);
                                    }
                                    continue;
                                }
                            }

                            Console.WriteLine($"unkown item : {p}");
                        }
                    }
                }
                return vals.ToArray();
            }
        }

        public string[] CreateRandomGames()
        {
            var left_memory = (long)FreeMem;

            void KnuthDurstenfeldShuffle<T>(List<T> list)
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
            var games = TilesManager.Instance.AllGames.ToList();
            KnuthDurstenfeldShuffle(games);

            var installedIds = installed.Select(x => x.TileId).ToHashSet();

            return games.Where(x => installedIds.Contains(x.tileid)).Where(x =>
            {
                left_memory -= x.length;
                return left_memory > 0;
            }).Select(g => g.tileid).ToArray();
        }

        public void WriteTargetFile(string[] ids)
        {
            var builder = new StringBuilder();
            foreach (var item in ids)
            {
                builder.AppendLine(item);
            }
            using (var ss = new MemoryStream(Encoding.Default.GetBytes(builder.ToString())))
            {
                _device.UploadFile(ss, installer_path);
            }
        }
        public void Connect()
        {
            if (!_device.IsConnected)
                _device.Connect();

            installed = ReadInstalledGames();

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
    }
}