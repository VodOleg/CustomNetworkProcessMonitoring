using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProcessesMonitoring
{
    public static class Notifier
    {
        private static HttpClient m_pClient = new HttpClient();
        private static string m_sUrl;
        private static string rotationMessage = "";
        private static bool keepRotating = true;
        
        public static void setDestinationUrl(string url)
        {
            m_sUrl = url;
        }

        public static void start(int rotationTimeMS)
        {
            Task oAsyncTaskRotation = Task.Factory.StartNew(() => startRotationThread(rotationTimeMS));

        }

        public static void startRotationThread(int rotationTimeMS)
        {

            while (keepRotating)
            {
                notifyRotational();
                Thread.Sleep(rotationTimeMS);
            }
        }

        public static void Notify(string message)
        {
            if (ConfigurationManager.AppSettings["SlackEnabled"] == "1")
            {
                string content = $"{{\"text\":\"{message}\"}}";
                string res = sendPost(content).Result;
            }   
        }

        public static void AppendForNextNotify(string message) {
            lock (rotationMessage)
            {
                rotationMessage += message + Environment.NewLine;
            }
        }

        private static void notifyRotational()
        {
            
            string message = "";
            lock (rotationMessage)
            {
                message = rotationMessage;
                rotationMessage = "";
            }
            if (!string.IsNullOrEmpty(message)) {
                Notify(message);
            }
        }

        private static async Task<string> sendPost(string jsonedContent)
        {
            if (string.IsNullOrEmpty(m_sUrl))
            {
                return "Url was not set.";
            }
            var httpContent = new StringContent(jsonedContent, Encoding.UTF8, "application/json");
            var result = await m_pClient.PostAsync(m_sUrl, httpContent);
            var received_json = result.Content.ReadAsStringAsync().Result;
            return received_json;
        }
    }
}
