using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SwitchWpd
{
    public struct GameInfo
    {
        public const string RandFullDiskName = "00000000";
        public string TileId;
        public UInt64 version;
        public string Name;

    }
}