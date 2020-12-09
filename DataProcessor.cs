using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessesMonitoring
{
    class DataProcessor
    {
        private int intervalMS { get; set; }
        private int suspiciousThreshold { get; set; }
        private int suspiciousPacketCounter = 0;
        private int updateCounter = 0;
        private int updateThreshold = 59;
        private int processID { get; set; }
        private bool stopNotifying = false;
        private SessionData lastData;
        AnalisysWindow analysis_window;
        private int m_sequentialBadBehaviorFrameSize;
        private bool m_behaviourIsOk = true;
        private int m_behaviour_counter = 0;
        private bool stopNotifyingStatistics = false;

        public DataProcessor(int intervalMS, int threshold, int processID, int updateThreshold)
        {
            this.intervalMS = intervalMS;
            this.suspiciousThreshold = threshold;
            this.processID = processID;
            this.updateThreshold = updateThreshold;
            this.analysis_window = new AnalisysWindow(int.Parse(ConfigurationManager.AppSettings["AnalysisWindowSize"]));
            this.m_sequentialBadBehaviorFrameSize = int.Parse(ConfigurationManager.AppSettings["SequentialBadBehaviourFrameSize"]);
            this.lastData = new SessionData();
            //Console.WriteLine($"analysis class ({this.processID}): m_sequentialBadBehaviorFrameSize = {m_sequentialBadBehaviorFrameSize}");
        }

        private bool statisticsAreBad(double val) {
            bool behaviorIsOk = analysis_window.AppendAndCheck(val);

            if (behaviorIsOk && !m_behaviourIsOk)
            {
                // new is ok last one was bad - reset
                m_behaviour_counter = 0;
            }
            else if (!behaviorIsOk && !m_behaviourIsOk)
            {
                //previous not good, new one not good
                m_behaviour_counter++;
            }
            else if( !behaviorIsOk && m_behaviourIsOk)
            {
                //new one is not ok, but previous was ok
                m_behaviour_counter++;
            }
            else
            {
                //both are ok
                m_behaviour_counter = 0;
            }

            //if(m_behaviour_counter > 0 && analysis_window.isReady())
            //{
            //    Console.WriteLine($"{this.processID}: behave counter = {m_behaviour_counter}");
            //}
            
            // if more then one
            return m_behaviour_counter >= m_sequentialBadBehaviorFrameSize && analysis_window.isReady(); 
        }

        public void Append(SessionData data_)
        {
            if (stopNotifying)
                return;

            updateCounter++;

            if (data_.disconnected)
            {
                Notifier.Notify($"PID {this.processID} disconnected. ( {ConfigurationManager.AppSettings["SlackMention"]} )");
                stopNotifying = true;
            }


            if (updateCounter > updateThreshold)
            {
                updateCounter = 0;
                string redflag = data_.timestamp.Equals(lastData.timestamp) ? ":red_flag:" : "";
                Notifier.AppendForNextNotify($"PID {processID}: S({data_.sent}) R({data_.received}) T({data_.timestamp}{redflag})");
            }

            if (statisticsAreBad(data_.received) && !stopNotifyingStatistics)
            {
                Console.WriteLine($"Notifying about process {this.processID}, bad statistics.");
                suspiciousPacketCounter = 0;
                // notify
                stopNotifyingStatistics = true;
                Notifier.Notify($"PID {this.processID} might have a problem (suspicious behavior). ( {ConfigurationManager.AppSettings["SlackMention"]} )");
            }

            double milliseconds_passed_since_data = DateTime.Now.Subtract(data_.timestamp).TotalMilliseconds;
            if (milliseconds_passed_since_data > intervalMS)
            {
                Console.WriteLine($"Notifying: suspiciosCounter={suspiciousPacketCounter} millisPassed={milliseconds_passed_since_data}");
                Console.WriteLine($"Data:R({data_.received}) S({data_.sent}) T({data_.timestamp})");
                suspiciousPacketCounter++;
            }

            

            if (suspiciousPacketCounter > suspiciousThreshold)
            {
                Console.WriteLine($"Notifying about process {this.processID}");
                suspiciousPacketCounter = 0;
                // notify
                stopNotifying = true;
                Notifier.Notify($"PID {this.processID} might have a problem. ( {ConfigurationManager.AppSettings["SlackMention"]} )");
            }

            lastData.hardCopy(data_);
        }
    }

    class AnalisysWindow
    {
        private int windowSize;
        private Queue<double> container_;
        public AnalisysWindow(int windowSize)
        {
            this.windowSize = windowSize;
            container_ = new Queue<double>(windowSize);
        }

        public void Append(double newValue)
        {
            if (container_.Count >= this.windowSize)
            {
                container_.Dequeue();
            }
            container_.Enqueue(newValue);
        }

        private float variance(double[] a)
        {
            int n = a.Length;
            // Compute mean (average 
            // of elements) 
            double sum = 0;

            for (int i = 0; i < n; i++)
                sum += a[i];

            double mean = (double)sum /
                          (double)n;

            // Compute sum squared  
            // differences with mean. 
            double sqDiff = 0;

            for (int i = 0; i < n; i++)
                sqDiff += (a[i] - mean) *
                          (a[i] - mean);

            return (float)sqDiff / n;
        }
        public int numOfSubsets(int bins)
        {

            List<List<double>> subsets = new List<List<double>>(bins);

            if (container_.Count < bins)
                return 0;

            double max_v = container_.Max();
            double min_v = container_.Min();

            double delta = ((max_v - min_v) / (bins));
            if (delta == 0)
            {
                // if max == min -> 1 subset and it is the set itself
                return 1;
            }

            for (int j = 0; j < bins; j++)
            {
                subsets.Add(new List<double>());
            }

            List<double> container = container_.ToList();

            for (int i = 0; i < container.Count; i++)
            {

                int bin_index = (int)(((container[i] - min_v)) / delta);
                subsets[bin_index < bins ? bin_index : bins - 1].Add(container[i]);
            }

            int subsets_count = 0;
            foreach (var subset in subsets)
            {
                if (subset.Count >= 1)
                {
                    subsets_count++;
                }

            }

            return subsets_count;
        }

        private float standardDeviation(double[] arr)
        {
            int n = arr.Length;
            return (float)Math.Sqrt(variance(arr));
        }

        public bool Check()
        {
            var subsets_count = numOfSubsets(10);

            float var = variance(container_.ToArray());
            //Console.WriteLine("Variance: " + var);

            float devi = standardDeviation(container_.ToArray());
            //Console.WriteLine("Standard Deviation: " + devi);

            bool isValid = ((subsets_count > 3)) && container_.Count == windowSize;
            
            
            return isValid;
        }

        public bool isReady()
        {
            return this.container_.Count == this.windowSize;
        }

        public bool AppendAndCheck(double newValue)
        {
            Append(newValue);

            return Check();
        }

        public string getValues()
        {
            string values = "[";
            var arr = container_.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (i == arr.Length - 1)
                {
                    values += arr[i];
                }
                else
                {
                    values += arr[i] + ",";
                }

            }
            values += "]";

            return values;

        }
    }
}
