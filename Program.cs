using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProcessesMonitoring
{
    class Program
    {
        public static bool UserCancelled()
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo k = Console.ReadKey(true);
                if ((k.Key >= ConsoleKey.Backspace && k.Key <= ConsoleKey.OemClear))
                { // the user pressed any key
                    return true; // out of the loop
                }
            }
            return false;
        }
        public class P_Process
        {
            public Process process { get; set; }
            public DataProcessor data_processor { get; set; }
        }

        static void Main(string[] args)
        {
            string postUrl = "";
            string processToMonitor = "";
            int intervalMS = 10000;
            int rotationalNotifyTime = 600000; //600000; // in ms

            intervalMS = int.Parse(ConfigurationManager.AppSettings["pollingIntervalMS"]);
            rotationalNotifyTime = int.Parse(ConfigurationManager.AppSettings["rotationNotifyTimeMS"]);
            postUrl = ConfigurationManager.AppSettings["SlackURL"];
            processToMonitor = ConfigurationManager.AppSettings["ProcessName"];


            
            CultureInfo culture = new CultureInfo("en-GB");
            Notifier.setDestinationUrl(postUrl);
            Notifier.start(rotationalNotifyTime); //10 min
            Process[] localAll = Process.GetProcessesByName(processToMonitor);
            List<Process> sortedProcesses = new List<Process>(localAll);
            sortedProcesses.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));
            List<P_Process> monitored_processes = new List<P_Process>();

            string StartMessage = "Starting Monitoring:\n";
            foreach (var process in sortedProcesses)
            {
                StartMessage += $"{process.Id} which started at {process.StartTime}\n";
                Console.WriteLine($"Adding ({process.ProcessName})[{process.StartTime}] ({process.Id})  to monitor");
                ETWwrapper.addProcess(process.Id);
                P_Process monitoredProcess = new P_Process();
                monitoredProcess.process = process;
                monitoredProcess.data_processor = new DataProcessor(intervalMS, 6, process.Id, rotationalNotifyTime/10000);
                monitored_processes.Add(monitoredProcess);
            }
            StartMessage += ":good-luck: :good-luck: :good-luck: :good-luck: :good-luck: :good-luck: :good-luck: :good-luck: :good-luck:";
            Notifier.Notify(StartMessage);
            string logName = $"{processToMonitor}_{DateTime.Now.Day}_{DateTime.Now.Month}__{DateTime.Now.Hour}_{DateTime.Now.Minute}.csv";
            Console.WriteLine($"Writting to {logName}\nConfigurations:");
            var appSettings = ConfigurationManager.AppSettings;
            foreach (var key in appSettings.AllKeys)
            {
                Console.WriteLine("{0}={1}", key, appSettings[key]);
            }


            Log.Start(logName);

            ETWwrapper.start();

            string header = "Time";
            foreach (var process in monitored_processes)
            {
                header += $",S({process.process.Id}),R({process.process.Id})";
            }
            Log.Write(header);


            while (!UserCancelled())
            {
                string outputData = "";
                outputData += $"{DateTime.Now.ToString(culture)}";
                foreach (var process in monitored_processes)
                {
                    
                    SessionData somedata = ETWwrapper.getData(process.process.Id);
                    process.data_processor.Append(somedata);
                    double milliseconds_passed_since_data = DateTime.Now.Subtract(somedata.timestamp).TotalMilliseconds;
                    if (milliseconds_passed_since_data > intervalMS)
                    {
                        outputData += $",{0},{0}";
                    }
                    else
                    {
                        outputData += $",{somedata.sent},{somedata.received}";
                    }
                    

                }
                Log.Write(outputData);
                Thread.Sleep(intervalMS);
            }

        }
    }
}
