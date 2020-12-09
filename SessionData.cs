using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessesMonitoring
{
    public class SessionData
    {
        public SessionData()
        {
            sent = 0;
            received = 0;
            cpuUsage = 0;
            timestamp = DateTime.Now;
            disconnected = false;
        }
        public int sent { get; set; }
        public int received { get; set; }
        public DateTime timestamp { get; set; }

        public double cpuUsage { get; set; }
        public bool disconnected { get; set; }

        public void hardCopy(SessionData other)
        {
            this.sent = other.sent;
            this.received = other.received;
            this.cpuUsage = other.cpuUsage;
            this.timestamp = other.timestamp;
        }

    }
}
