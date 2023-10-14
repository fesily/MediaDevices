using MediaDevices;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CsvHelper.Configuration;

namespace SwitchWpd
{
    public struct GameInfo
    {
        public const string RandFullDiskName = "00000000";
        public string TileId;
        public UInt64 version;
        public string Name;
    }
    public class InstalledGameInfo
    {
        public string TileId { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
    }
    public class TilesManager
    {
        public static TilesManager Instance { get; set; }
        public Dictionary<string, string> en2TitleId;
        public Dictionary<string, string> zh2TitleId;
        public Dictionary<string, string> tileId2Path;
        TilesManager(Dictionary<string, string> en2TitleId, Dictionary<string, string> zh2TitleId, Dictionary<string, string> tileId2Path)
        {
            this.en2TitleId = en2TitleId;
            this.zh2TitleId = zh2TitleId;
            this.tileId2Path = tileId2Path;
        }
    }
    public static class DiskPath
    {
        public enum Type
        {
            Unknown,
            SD_Card,
            Nand_User,
            Nand_System,
            Installed_Games,
            SD_Card_install,
            NAND_install,
            Saves,
            Album,
            Homebrew,
            Screenshots,
        }
        public const string DiskSDCard = "1: SD Card";
        public const string DiskNandUser = "2: Nand USER";
        public const string DiskNandSystem = "3: Nand SYSTEM";
        public const string DiskInstalledGames = "4: Installed games";
        public const string DiskSDCardInstall = "5: SD Card install";
        public const string DiskNandInstall = "6: NAND install";
        public const string DiskSaves = "7: Saves";
        public const string DiskAlbum = "8: Album";
        public const string DiskHomebrew = "Homebrew";
        public const string DiskScreenshots = "Screenshots";

        public static string ToDisk(Type type)
        {
            switch (type)
            {
                case Type.SD_Card: return DiskSDCard;
                case Type.Nand_User: return DiskNandUser;
                case Type.Nand_System: return DiskNandSystem;
                case Type.Installed_Games: return DiskInstalledGames;
                case Type.SD_Card_install: return DiskSDCardInstall;
                case Type.NAND_install: return DiskNandInstall;
                case Type.Saves: return DiskSaves;
                case Type.Album: return DiskAlbum;
                case Type.Homebrew: return DiskHomebrew;
                case Type.Screenshots: return DiskScreenshots;
                case Type.Unknown:
                default:
                    return "unkown";
            }
        }

        public static string Join(Type tt, params string[] path)
        {
            var newPaths = new string[path.Length + 2];
            newPaths[0] = "\\";
            newPaths[1] = ToDisk(tt);
            for (int i = 0; i < path.Length; i++)
            {
                newPaths[i + 2] = path[i];
            }
            return Path.Join(newPaths);
        }
    }
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
        public string SerialNumber {
            get{
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
        const string hash_name = "installer_hash";
        string installer_hash_path = DiskPath.Join(DiskPath.Type.SD_Card, hash_name);
        string installer_json_path = DiskPath.Join(DiskPath.Type.SD_Card, "installer.json");
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

                    return csvReader.GetRecords<InstalledGameInfo>().ToArray();
                }
            }
        }
        public void Connect()
        {
            _device.Connect();
            using (var ss = new MemoryStream())
            {
                _device.DownloadFile(installer_json_path, ss);
                target = JsonSerializer.Deserialize<string[]>(ss)?.Select(id => new GameInfo { TileId = id }).ToArray();
            }
            if (!string.IsNullOrEmpty(_device.GetFiles(DiskPath.DiskSDCard).FirstOrDefault(x => x == hash_name)))
            {
                using (var ss = new MemoryStream())
                {
                    _device.DownloadFile(installer_hash_path, ss);
                    var hash = new StreamReader(ss).ReadToEnd();
                    if (hash == hash_name)
                    {
                        return;
                    }
                }
            }

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
                        //TODO:缺少文件
                        continue;
                    }
                    if (!Directory.Exists(path))
                    {
                        //TODO:缺少文件
                        continue;
                    }
                    _device.UploadFolder(path, DiskPath.DiskSDCardInstall);
                }
            }
        }
        private byte[]? TargetHash
        {
            get
            {
                var md5 = MD5.Create();
                foreach (var info in target)
                {
                    md5.ComputeHash(Encoding.UTF8.GetBytes(info.TileId));
                }
                return md5.Hash;
            }
        }
        public void Disconnect()
        {
            var hash = TargetHash;
            if (hash != null)
            {
                using (var ms = new MemoryStream(hash, false))
                {
                    _device.UploadFile(ms, installer_hash_path);
                }
            }
            _device.Disconnect();
        }
        public void Dispose()
        {
            _device.Dispose();
        }
    }
    public class Manager
    {
        // 情况：1.完成，2.异常断开，3.新加入
        /*
         * 1. 完成：hash结果
         * 2. 异常断开： 不处理，等待重连
         * 3. 新加入： 判断是不是加载过的
         */
        ConcurrentDictionary<string, Switch> switches = new ConcurrentDictionary<string, Switch>();
        public Manager()
        {
        }
        public static Manager Instance { get; set; }
        public List<Switch> RefreshList()
        {
            var devices = MediaDevice.GetDevices();
            return devices.TakeWhile(d => d.FriendlyName == "Switch").
                ExceptBy(
                    switches.Select(s => s.Key), x => x.DeviceId)
                .Select(x => new Switch(x)).ToList();
        }
        public bool Stop { get; set; }
        public void Upload()
        {
            while (!Stop)
            {
                var joined_switches = RefreshList();
                if (joined_switches.Count > 0)
                {
                    foreach (var sw in joined_switches)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                sw.Connect();
                                if (sw.left != null)
                                {
                                    switches.TryAdd(sw.ID, sw);
                                    sw.Upload();
                                }
                            }
                            finally
                            {
                                Switch @switch;
                                switches.TryRemove(sw.ID, out @switch);
                                sw.Disconnect();
                            }
                        });
                    }

                }
            }
        }
    }
}