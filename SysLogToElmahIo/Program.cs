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
using Elmah.Io.Client;
using Newtonsoft.Json;
using NLog;
using Logger = NLog.Logger;

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
                logger.Log(LogLevel.Debug, "No EventID provided");
                return;
            }
            var eventRecordId = args[0];

            // query the event record id from the event log reader
            EventRecord @event = null;
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
                        @event = eventRecord;
                        var eventLogData = eventRecord.ToXml();
                        logger.Log(LogLevel.Debug, eventLogData);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Error, e);
                throw;
            }

            if (@event == null)
            {
                logger.Log(LogLevel.Debug, "Event was null");
                return;
            }

            var message = EventToMessage(@event);
            var elmahClient = Elmah.Io.Client.Logger.Create(new Guid(ConfigurationManager.AppSettings["ElmahIoLogId"]));
            elmahClient.OnMessage += (sender, eventArgs) => logger.Log(LogLevel.Info, "Successfully imported event to elmah.io");
            elmahClient.OnMessageFail += (sender, eventArgs) => logger.Log(LogLevel.Error, eventArgs.Error);
            elmahClient.Log(message);
        }

        private static Message EventToMessage(EventRecord @event)
        {
            if (@event.UserId != null)
            {
                var message = new Message(@event.FormatDescription())
                {
                    Severity = Level(@event.LevelDisplayName),
                    DateTime = @event.TimeCreated ?? DateTime.UtcNow,
                    Source = @event.LogName,
                    Hostname = @event.MachineName,
                    User = @event.UserId.Value,
                    Data = Data(@event.Properties),
                    Detail = System.Security.SecurityElement.Escape(@event.ToXml()),
                };
                return message;
            }
            else
            {
                var message = new Message(@event.FormatDescription())
                {
                    Severity = Level(@event.LevelDisplayName),
                    DateTime = @event.TimeCreated ?? DateTime.UtcNow,
                    Source = @event.LogName,
                    Hostname = @event.MachineName,
                    Data = Data(@event.Properties),
                    Detail = System.Security.SecurityElement.Escape(@event.ToXml()),
                };
                return message;
            }
        }

        private static List<Item> Data(IList<EventProperty> properties)
        {
            return properties
                .Select((t, i) => new Item
                {
                    Key = "Property" + (1 + i),
                    Value = t.Value.ToString()
                })
                .ToList();
        }

        private static Severity? Level(string level)
        {
            if (string.IsNullOrWhiteSpace(level)) return null;
            switch (level)
            {
                case "Critical":
                    return Severity.Fatal;
                case "Error":
                    return Severity.Error;
                case "Informational":
                    return Severity.Information;
                case "Verbose":
                    return Severity.Verbose;
                case "Warning":
                    return Severity.Warning;
            }

            return null;
        }
    }
}