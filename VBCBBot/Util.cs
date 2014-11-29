using System;
using System.IO;
using System.Reflection;

namespace VBCBBot
{
    public static class Util
    {
        public static string ProgramDirectory
        {
            get
            {
                var localPath = (new Uri(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;
                return Path.GetDirectoryName(localPath);
            }
        }
    }
}

