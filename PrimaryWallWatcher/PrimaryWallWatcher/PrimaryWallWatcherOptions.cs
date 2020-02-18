using System;
using System.Collections.Generic;
using System.Text;

namespace PrimaryWallWatcher
{
    public class PrimaryWallWatcherOptions
    {
        /// <summary>
        /// Time for attemption for reconnecting
        /// </summary>
        public TimeSpan TimeForReconnecting { get; } = new TimeSpan(0, 10, 0);

        /// <summary>
        /// Maximum posts count to load per request
        /// </summary>
        public int MaxLoadPostsCount { get; } = 100;

        /// <summary>
        /// Increment for break time when 'wall posts events' activity is low
        /// </summary>
        public TimeSpan IncrementSleepTimeForLongCheck { get; } = TimeSpan.FromSeconds(5);

        public TimeSpan IncrementSleepTimeForShortCheck { get; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Maximum break time when 'wall posts events' activity is low
        /// </summary>
        public TimeSpan MaxSleepTime { get; } = TimeSpan.FromSeconds(60);

        public TimeSpan MaxSleepTimeForShortCheck { get; internal set; } = TimeSpan.FromSeconds(60);




        /// <summary>
        /// Initial posts count to load per request (amount of last posts to load on probe request)
        /// </summary>
        public int ProbeLoadPostsCount { get; set; } = 10;
        

        /// <summary>
        /// Indicates whether to inform about new posts on loading after retrieve or after gathering all new posts
        /// </summary>
        public bool InformImmediately { get; set; } = true;

        /// <summary>
        /// No nw posts in a row max sleep steps multiplier
        /// </summary>
        public byte MaxSleepSteps { get; set; } = 3;

        /// <summary>
        /// Watcher wait time after step in miliseconds if there was no new posts
        /// </summary>
        public int StepSleepTimeMsec { get; set; } = 5000;
        
    }
}
