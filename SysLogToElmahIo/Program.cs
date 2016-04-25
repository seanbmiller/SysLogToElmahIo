using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using NLog;

namespace SysLogToElmahIo
{
    class Program
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            //get the event record id
            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    logger.Log(LogLevel.Debug, "args[" + i + "]:" + args[i]);
                }
            }
            else
            { 
                logger.Log(LogLevel.Debug,"No EventID provided");
                return;
            }
            var eventRecordId = args[0];

            // query the event record id from the event log reader
            string eventLogData = null;
            try
            {
                EventLogQuery query = new EventLogQuery("Application", PathType.LogName, ConfigurationManager.AppSettings["EventLogXmlPath"].Expand(eventRecordId));
                query.ReverseDirection = true;
                EventLogReader reader = new EventLogReader(query);
                EventRecord eventRecord;
                while ((eventRecord = reader.ReadEvent()) != null)
                {
                    logger.Log(LogLevel.Debug, "EventRecordID:{0}", eventRecord.RecordId.ToString());
                    if (eventRecord.RecordId.Value == Int32.Parse(eventRecordId))
                    {
                        eventLogData = eventRecord.ToXml();
                        logger.Log(LogLevel.Debug, eventLogData);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Error, e);
                throw;
            }
            
            if (eventLogData == null)
            {
                logger.Log(LogLevel.Debug, "Event Log Data was null");
                return;
            }


            //build an api call to elmah.io
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(eventLogData);
            string jsonText = JsonConvert.SerializeXmlNode(doc);

            var createError = new
            {
                title = jsonText
            };
            
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(ConfigurationManager.AppSettings["ElmahIoLogUrl"] + ConfigurationManager.AppSettings["ElmahIoLogId"]);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                var createErrorString = JsonConvert.SerializeObject(createError);
                var bytes = Encoding.UTF8.GetBytes(createErrorString);
                request.ContentLength = bytes.Length;
                request.ContentType = "application/json";
                var outputStream = request.GetRequestStream();
                outputStream.Write(bytes, 0, bytes.Length);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                logger.Log(LogLevel.Debug, "Status: " + response.StatusDescription);
                logger.Log(LogLevel.Debug, "ResponseUri: " + response.ResponseUri);
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Error, e);
                throw;
            }
        }
    }
}
