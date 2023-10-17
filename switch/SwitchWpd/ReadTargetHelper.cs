using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SwitchWpd
{
    public static class ReadTargetHelper
    {
        private static string PathRoot { get; } = Path.GetFullPath(TilesManager.Root).ToLower();
        public static bool IsMultiDirLine(string p)
        {
            var count = 0;
            var offset = 0;
            var root = Path.GetFullPath(TilesManager.Root).ToLower();

            do
            {
                offset = p.IndexOf(PathRoot, offset) + 1;
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

        public static string[] ReadTargetFromStream(StreamReader reader)
        {
            HashSet<string> vals = new HashSet<string>();
            while (!reader.EndOfStream)
            {
                var p = reader.ReadLine();
                if (!string.IsNullOrEmpty(p))
                {
                    if (IsMultiDirLine(p))
                    {
                        ReadMultiDir(vals, p);
                    }
                    else if (Directory.Exists(p))
                    {
                        FromDir(vals, p);
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
                            if (TilesManager.Instance.TryGetTitleIdFilesByName(p, out string[] ids))
                            {
                                foreach (var id in ids)
                                {
                                    foreach (var item in TilesManager.Instance.EnumAppTileIdFilesID(id))
                                    {
                                        vals.Add(item);
                                    }
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

        private static void FromDir(HashSet<string> vals, string? p)
        {
            if (!Directory.Exists(p))
            {
                throw new DirectoryNotFoundException(p);
            }
            Console.WriteLine($"Read Target from {p}");
            var ids = TilesManager.EnumerateFiles(p).Select(TilesManager.GetTileId);
            foreach (var item in ids)
            {
                vals.Add(item);
            }
        }

        public static void ReadMultiDir(HashSet<string> vals, string p)
        {
            char split = '\\';
            // 特殊模式 复制很多个目录
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                split = '/';
            }
            var paths = p.Split(PathRoot, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                var dir = Path.Join(PathRoot, path);
                FromDir(vals, dir.Trim());
            }
        }
    }
}
