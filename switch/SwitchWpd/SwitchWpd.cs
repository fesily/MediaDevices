using MediaDevices;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SwitchWpd
{
    public struct GameInfo
    {
        public string tileId;
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
    public class Switch : IDisposable
    {
        ILogger Logger;
        public MediaDevice _device;
        public GameInfo[] target;
        public GameInfo[] installed;
        public GameInfo[]? left;
        public string ID { get; private set; }
        public Switch(MediaDevice device) { 
            _device = device;
            ID = _device.DeviceId;
        }
        const string DiskName1 = @"\1. SD Card";
        const string DiskName5 = @"\5. installed games";
        const string DiskName6 = @"\5. installed games";
        const string hash_name = "installer_hash";
        const string installer_hash_path = DiskName1 + @"\installer_hash";
        const string installer_json_path = DiskName1 + @"\installer.json";
        public void Connect()
        {
            _device.Connect();
            using (var ss = new MemoryStream())
            {
                _device.DownloadFile(installer_json_path, ss);
                target = JsonSerializer.Deserialize<string[]>(ss)?.Select(id => new GameInfo { tileId = id }).ToArray();
            }
            if (!string.IsNullOrEmpty(_device.GetFiles(DiskName1).FirstOrDefault(x => x == hash_name)))
            {
                using(var ss = new MemoryStream())
                {
                    _device.DownloadFile(installer_hash_path, ss);
                    var hash = new StreamReader(ss).ReadToEnd();
                    if (hash == hash_name)
                    {
                        return;
                    }
                }
            }
            using (var ss = new MemoryStream()){
                installed = _device.EnumerateDirectories(DiskName5).Select(name=> new GameInfo { tileId = TilesManager.Instance.en2TitleId[name] }).ToArray();
            }

            Func<GameInfo, string> selector = x => x.tileId;
            var a = installed.Select(selector);
            left = target?.IntersectBy(a, selector).ExceptBy(a, selector).ToArray();
        }
        public void Upload()
        {
            if (left != null)
            {
                foreach (var info in left)
                {
                    string path;
                    if (!TilesManager.Instance.tileId2Path.TryGetValue(info.tileId,out path))
                    {
                        //TODO:缺少文件
                        continue;
                    }
                    if (!Directory.Exists(path))
                    {
                        //TODO:缺少文件
                        continue;
                    }
                    _device.UploadFolder(path, DiskName6);
                }
            }
        }
        private byte[]? TargetHash
        {
            get {
                var md5 = MD5.Create();
                foreach (var info in target)
                {
                    md5.ComputeHash(Encoding.UTF8.GetBytes(info.tileId));
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
        public Manager() { }
        public static Manager Instance { get; set; }
        public List<Switch> RefreshList()
        {
            var devices = MediaDevice.GetDevices();
            return devices.TakeWhile(d => d.FriendlyName == "Switch").
                ExceptBy(
                    switches.Select(s => s.Key), x => x.DeviceId)
                .Select(x=>new Switch(x)).ToList();
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