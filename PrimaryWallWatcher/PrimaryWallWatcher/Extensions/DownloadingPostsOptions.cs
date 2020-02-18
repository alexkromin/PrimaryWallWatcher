using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using VkNet.Enums.SafetyEnums;

namespace PrimaryWallWatcher.Extensions
{
    public class DownloadingPostsOptions
    {
        public DownloadingPostsOptions(long wallId, int countInFirstLoad, WallFilter wallFilter, TimeSpan timeForReconnection)
        {
            WallId = wallId;
            CountInFirstLoad = countInFirstLoad;
            WallFilter = wallFilter;
            TimeForReconnection = timeForReconnection;
        }

        public long WallId { get; set; }

        public int CountInFirstLoad { get; set; }

        [NotNull]
        public WallFilter WallFilter { get; set; }

        public TimeSpan TimeForReconnection { get; set; }
    }
}
