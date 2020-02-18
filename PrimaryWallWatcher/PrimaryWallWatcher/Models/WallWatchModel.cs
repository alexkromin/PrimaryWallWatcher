using System;
using VkNetExtend.WallWatcher.Models;

namespace PrimaryWallWatcher.Models
{
    public class WallWatchModel
    {
        /// <summary>
        /// Id стены, за которой ведётся наблюдение
        /// </summary>
        public long WallId { get; }

        //////////////|| MONITORING LIMITS ||///////////////

        /// <summary>
        /// Лимит проверка для длинной проверки.
        /// </summary>
        public MonitoringLimits MonitoringLimits { get; set; }

        /// <summary>
        /// Лимит проверки для короткой проверки.
        /// </summary>
        public MonitoringLimits ShortMonitoringLimits { get; set; }

        /// <summary>
        /// Отслеживать посты на редактирование
        /// </summary>
        public bool WatchEditing { get; set; } = false;

        /// <summary>
        /// Период между отслеживаниями за появлением новых постов, редактированием, удалением и переходами всех постов за большой период
        /// </summary>
        public TimeSpan MonitoringPeriod { get; set; }

        /// <summary>
        /// Период между отслеживаниями за появлениям новых постов, редактированием, удалением и переходами постов за короткий период
        /// </summary>
        public TimeSpan ShortMonitoringPeriod { get; set; }

        //public TimeSpan MaxSleepTime { get; set; }

        //public TimeSpan IncrementSleepTime { get; set; }

        public WallWatchModel(long wallId,
            MonitoringLimits monitoringLimits,
            MonitoringLimits shortMonitoringLimits,
            TimeSpan monitoringPeriod,
            TimeSpan shortMonitoringPeriod)
        {
            WallId = wallId;
            MonitoringLimits = monitoringLimits ?? throw new ArgumentNullException(nameof(monitoringLimits));
            ShortMonitoringLimits = shortMonitoringLimits ?? throw new ArgumentNullException(nameof(shortMonitoringLimits));
            MonitoringPeriod = monitoringPeriod;
            ShortMonitoringPeriod = shortMonitoringPeriod;
        }
    }
}
