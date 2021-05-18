﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Constants.WireGuard
{
    public static class WireGuardConstants
    {
        public static string CONFIG_FILE_LOCATION = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static string NAME_WIN_SERVICE = "WireGuard_SYNTROPY";
        public static string DESCRIPTION_WIN_SERVICE = "";
    }
}
