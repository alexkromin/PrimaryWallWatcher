using System;
using System.Collections.Generic;
using System.Text;

namespace VkNetExtend.WallWatcher.Models
{
    public class StartWallWatchModel
    {
        /// <summary>
        /// Id группы/пользователя за стеной, которого ведётся наблюдение
        /// </summary>
        public long WallId { get; }


        //////////////|| MONITORING LIMITS ||///////////////

        /// <summary>
        /// Период до настоящего момента, за который нужно отслеживать не удалён ли пост + Кол-во постов до настоящего момента, за которыми надо вести наблюдение
        /// </summary>
        public MonitoringLimits MonitoringLimits { get; set; }

        /// <summary>
        /// Лимит для короткой проверки. Указывает сколько опубликованных постов загружать в короткой проверке сверх новых
        /// +
        /// Период для короткой проверки. Указывает за какой период надо загружать посты в короткой проверке сверх новых
        /// </summary>
        public MonitoringLimits ShortMonitoringLimits { get; set; }

        /// <summary>
        /// Нужно ли отслеживать не отредактирован ли пост
        /// </summary>
        public bool WatchEditing { get; set; } = true;
        //////////////////////////////




        /// <summary>
        /// Период между отслеживаниями за появлением новых постов, редактированием, удалением и переходами всех постов за большой период
        /// </summary>
        public TimeSpan PeriodBetweenLongChecks { get; set; }

        /// <summary>
        /// Период между отслеживаниями за появлениям новых постов, редактированием, удалением и переходами постов за короткий период
        /// </summary>
        public TimeSpan PeriodBetweenShortChecks { get; set; }

        //public TimeSpan MaxSleepTime { get; set; }

        //public TimeSpan IncrementSleepTime { get; set; }

        



                        

        public StartWallWatchModel(long wallId,
                                     MonitoringLimits monitoringLimits,
                                     MonitoringLimits shortMonitoringLimits,
                                     bool watchEditing,
                                     TimeSpan periodBetweenLongChecks,
                                     TimeSpan periodBetweenShortChecks)
        { 
            (WallId, MonitoringLimits , ShortMonitoringLimits, WatchEditing, PeriodBetweenLongChecks , PeriodBetweenShortChecks)
             = 
            (wallId, monitoringLimits, shortMonitoringLimits, watchEditing, periodBetweenLongChecks, periodBetweenShortChecks);
        }
    }
}
