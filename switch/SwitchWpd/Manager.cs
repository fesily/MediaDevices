using MediaDevices;
using System.Collections.Concurrent;

namespace SwitchWpd
{
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