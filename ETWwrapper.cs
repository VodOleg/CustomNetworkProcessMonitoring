using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProcessesMonitoring
{
    
    public static class ETWwrapper
    {
        private static Task m_workingThread;
        private static CancellationTokenSource _tokenSource = null;
        private static Dictionary<int, SessionData> m_ProcessIDs = new Dictionary<int, SessionData>();
        private static TraceEventSession m_EtwSession;

        public static void start()
        {
            _tokenSource = new CancellationTokenSource();
            var cancelToken = _tokenSource.Token;
            m_workingThread = Task.Run(() => StartEtwSession(cancelToken));
        }

        public static void addProcess(int processID)
        {
            lock (m_ProcessIDs)
            {
                m_ProcessIDs[processID] = new SessionData();
            }
        }

        private static void StartEtwSession(CancellationToken cancelToken)
        {
            try
            {

                using (m_EtwSession = new TraceEventSession("MyKernelAndClrEventsSession"))
                {
                    m_EtwSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

                    m_EtwSession.Source.Kernel.TcpIpRecv += data =>
                    {
                        //Console.WriteLine($"process reading: {data.ProcessID}");
                        if (cancelToken.IsCancellationRequested)
                        {
                            // stop the monitor
                            Console.WriteLine("canceled rec");
                            return;
                        }
                        if (m_ProcessIDs.ContainsKey(data.ProcessID))
                        {
                            //Console.WriteLine($"rec: {data.ProcessID} ({data.ProcessName}) ->{data.size}");
                            lock (m_ProcessIDs[data.ProcessID])
                            {
                                m_ProcessIDs[data.ProcessID].received = data.size;
                                m_ProcessIDs[data.ProcessID].timestamp = data.TimeStamp;
                            }

                        }
                        else
                        {

                        }
                    };

                    m_EtwSession.Source.Kernel.TcpIpDisconnect += data =>
                    {
                        if (m_ProcessIDs.ContainsKey(data.ProcessID))
                        {
                            Console.WriteLine($"detected {data.ProcessID} tcp disconnect");
                        }
                    };

                    m_EtwSession.Source.Kernel.TcpIpSend += data =>
                    {
                        if (cancelToken.IsCancellationRequested)
                        {
                            // stop the monitor
                            return;
                        }
                        if (m_ProcessIDs.ContainsKey(data.ProcessID))
                        {
                            lock (m_ProcessIDs[data.ProcessID])
                            {
                                m_ProcessIDs[data.ProcessID].sent = data.size;
                            }
                        }
                    };

                    m_EtwSession.Source.Process();
                }
            }
            catch
            {
                //ResetCounters(); // Stop reporting figures
                // Probably should log the exception
                Console.WriteLine("Exception caught in session ETW");
            }
        }

        public static SessionData getData(int ProcessID)
        {
            SessionData dataToReturn = new SessionData();
            

            if (m_ProcessIDs.ContainsKey(ProcessID))
            {
                lock (m_ProcessIDs[ProcessID])
                {
                    dataToReturn.hardCopy(m_ProcessIDs[ProcessID]);
                }
            }
            return dataToReturn;
        }
    }
}
