using System;
using System.Collections.Generic;
using System.Text;

namespace VkNetExtend.WallWatcher.Models
{
    public class BorderModel
    {
        public DateTime MonitoringTimeBorder { get; set; }

        public long MonitoringNumberBorder { get; set; }

        public BorderModel()
        {

        }
        public BorderModel(DateTime monitoringTimeBorder, long monitoringNumberBorder)
        {
            MonitoringTimeBorder   = monitoringTimeBorder;
            MonitoringNumberBorder = monitoringNumberBorder;
        }

    }
}
