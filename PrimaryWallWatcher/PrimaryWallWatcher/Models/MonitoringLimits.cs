using System;
using System.Diagnostics;

namespace VkNetExtend.WallWatcher.Models
{
    [DebuggerDisplay("{MonitoringPeriod} - {MonitoringLimit}")]
    public class MonitoringLimits
    {
        /// <summary>
        /// Time limit
        /// </summary>
        public TimeSpan? MonitoringPeriod { get; private set; } = null;

        /// <summary>
        /// Count limit
        /// </summary>
        public int? MonitoringLimit { get; private set; } = null;

        private string DEBUGGER_INFO
        {
            get
            {
                if (MonitoringPeriod.HasValue && MonitoringLimit.HasValue)
                {
                    return $"Last {MonitoringLimit} for {MonitoringPeriod}.";
                }
                else if (MonitoringPeriod.HasValue)
                {
                    return $"For last {MonitoringPeriod}.";
                }
                else
                {
                    return $"Last {MonitoringLimit}.";
                }
            }
        }

        public MonitoringLimits(TimeSpan monitoringPeriod)
        {
            MonitoringPeriod = monitoringPeriod;
        }

        public MonitoringLimits(int monitoringLimit)
        {
            MonitoringLimit = monitoringLimit;
        }

        public MonitoringLimits(TimeSpan monitoringPeriod, int monitoringLimit)
        {
            MonitoringPeriod = monitoringPeriod;
            MonitoringLimit = monitoringLimit;
        }
    }
}
