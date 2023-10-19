using MediaDevices;
using System.Collections.Concurrent;

namespace SwitchWpd
{
    public class Manager
    {
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
                            if (!switches.TryAdd(sw.ID, sw))
                                return;
                            try
                            {

                            }
                            finally
                            {
                                switches.TryRemove(sw.ID, out Switch __);
                            }
                        });
                    }

                }
            }
        }
    }
}