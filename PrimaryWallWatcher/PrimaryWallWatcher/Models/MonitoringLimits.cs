using System;
using System.Collections.Generic;
using System.Text;

namespace VkNetExtend.WallWatcher.Models
{
    public class MonitoringLimits
    {
        public TimeSpan? MonitoringPeriod { get; set; }

        public int? MonitoringLimit { get; set; }

        public MonitoringLimits(TimeSpan? monitoringPeriod, int? monitoringLimit)
        {
            if (monitoringPeriod == null && monitoringLimit == null)
                throw new Exception("Один из параметров должен быть не NULL");
            MonitoringPeriod = monitoringPeriod;
            MonitoringLimit  = monitoringLimit;
        }
    }
}
