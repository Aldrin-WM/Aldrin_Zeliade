using System;
using System.Collections.Generic;
using System.Linq;


namespace AldrinAnalytics
{

    public static class LogPath
    {
        public const string Value = "D:\\dev"; // TODO
        public static readonly string StartTime; // TODO

        static LogPath()
        {
            StartTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }


}
