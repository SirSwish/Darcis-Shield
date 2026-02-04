using System;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace UrbanChaosMapEditor.Models
{
    public enum PrimButtonCategory
    {
        CityAssets,
        Vehicles,
        Structures,
        Furniture,
        Foliage,
        Signs,
        Lights,
        Switches,
        Weapons,
        Collectibles,
        Utilities,
        Misc
    }
}

