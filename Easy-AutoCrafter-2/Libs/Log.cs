using System;
using System.Collections.Generic;
using System.Linq;
using VRage;

namespace IngameScript
{
    public static class MyLog
    {
        static readonly List<MyTuple<LogLevel, DateTime, string>> Logs = new List<MyTuple<LogLevel, DateTime, string>>();
        static readonly List<MyTuple<LogLevel, DateTime, string>> NewLogs = new List<MyTuple<LogLevel, DateTime, string>>();

        public static bool HasNewLogs => NewLogs.Count > 0;
        
        public static void Log(LogLevel level, string message)
        {
            NewLogs.Add(new MyTuple<LogLevel, DateTime, string>(level, DateTime.UtcNow, message));
        }

        public static MyTuple<LogLevel, DateTime, string>[] Flush()
        {
            var array = NewLogs.ToArray();
            Logs.AddRange(NewLogs);
            NewLogs.Clear();
            return array;
        }
        
        public static IEnumerable<string> GetLogs(LogLevel minimumLevel = LogLevel.Info) =>
            Logs.Where(k => k.Item1 >= minimumLevel).Select(AsLog);
        
        public static IEnumerable<string> GetLogsOfLevel(LogLevel level) =>
            Logs.Where(k => k.Item1 == level).Select(AsLog);

        public static string AsLog(this MyTuple<LogLevel, DateTime, string> log) => $"{log.Item1}: {log.Item2} - {log.Item3}";
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}