using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SwitchWpd
{
    public class ReadTargetHelper
    {
        private static string PathRoot(string root) => Path.GetFullPath(root).ToLower();
        public static bool IsMultiDirLine(string p, string root)
        {
            var count = 0;
            var offset = 0;

            do
            {
                offset = p.IndexOf(root, offset) + 1;
                if (offset == 0)
                    break;
                count++;
            } while (true);
            if (count > 1)
            {
                Console.WriteLine($"Detected {count} Root Dir");
            }
            return count > 1;
        }
        public static bool IsMultiDirLine(string p)
        {
            foreach (var root in Config.Roots)
            {
                if (IsMultiDirLine(p, root))
                    return true;
            }
            return false;
        }
        public HashSet<string> appIds = new HashSet<string>();
        HashSet<string> vals = new HashSet<string>();
        public int MissCount { get; private set; } = 0;
        public bool NeedUpdate { get; private set; } = false;
        public string[] ReadTargetFromStream(StreamReader reader)
        {
            while (!reader.EndOfStream)
            {
                var p = reader.ReadLine();
                if (!string.IsNullOrEmpty(p))
                {
                    if (IsMultiDirLine(p))
                    {
                        NeedUpdate = true;
                        ReadMultiDir(vals, p);
                    }
                    else if (Directory.Exists(p))
                    {
                        NeedUpdate = true;
                        appIds.Add(p);
                        FromDir(vals, p);
                    }
                    else if (Path.Exists(p))
                    {
                        appIds.Add(p);
                        vals.Add(TilesManager.GetTileId(p));
                    }
                    else
                    {
                        try
                        {
                            BigInteger i = BigInteger.Parse(p, NumberStyles.HexNumber);
                            if (i > 0)
                            {
                                appIds.Add(p);
                                foreach (var item in TilesManager.Instance.EnumAppTileIdFilesID(p))
                                {
                                    vals.Add(item);
                                }
                                continue;
                            }
                        }
                        catch (FormatException)
                        {
                            if (TilesManager.Instance.TryGetTitleIdFilesByName(p, out string[] ids))
                            {
                                NeedUpdate = true;
                                foreach (var id in ids)
                                {
                                    appIds.Add(id);
                                    foreach (var item in TilesManager.Instance.EnumAppTileIdFilesID(id))
                                    {
                                        vals.Add(item);
                                    }
                                }
                                continue;
                            }
                        }

                        Console.WriteLine($"unkown item : {p}");
                        appIds.Add(p);
                        MissCount++;
                    }
                }
            }
            return vals.ToArray();

        }

        private void FromDir(HashSet<string> vals, string? p)
        {
            if (!Directory.Exists(p))
            {
                throw new DirectoryNotFoundException(p);
            }
            appIds.Add(p);
            Console.WriteLine($"Read Target from {p}");
            var ids = TilesManager.EnumerateFiles(p).Select(TilesManager.GetTileId);
            foreach (var item in ids)
            {
                vals.Add(item);
            }
        }

        public void ReadMultiDir(HashSet<string> vals, string p)
        {
            // 特殊模式 复制很多个目录
            char split = '\\';
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                split = '/';
            }
            var paths = p.Split(Config.Roots.Select(PathRoot).ToArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                var dir = Config.Roots.Select(x => Path.Join(x, path)).First(Directory.Exists);
                FromDir(vals, dir.Trim());
            }
        }
    }
}
