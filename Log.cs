using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessesMonitoring
{

    public static class Log
    {
        private static string logPath;
        private static CultureInfo culture;

        public static void Start(string filename_)
        {
            logPath = Path.Combine(Directory.GetCurrentDirectory(), filename_);
            culture = new CultureInfo("en-GB");
        }

        public static async Task Write(string message)
        {
           
            string text = String.Format("{0}", message);

            using (FileStream sourceStream = new FileStream(logPath,
                FileMode.Append, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                using (StreamWriter sw = new StreamWriter(sourceStream))
                {
                    await sw.WriteLineAsync(text);
                }
            };

        }

    }


}
