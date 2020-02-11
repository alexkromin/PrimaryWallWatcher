using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PrimaryWallWatcher.Models;

namespace PrimaryWallWatcher
{
    public delegate void WallPostsChangesDelegate(IEnumerable<PrimaryWallEvent> events);
    public interface IPrimaryWallWatchService
    {
        event WallPostsChangesDelegate PrimaryWallPostsEvents;

        Task StartWatchAsync(StartWallWatchModel model);
    }
}
