using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VkNet.Abstractions;

using PrimaryWallWatcher.Models;

namespace PrimaryWallWatcher
{
    public delegate void WallPostsChangesDelegate(IEnumerable<PrimaryWallEvent> events);

    public interface IPrimaryWallWatchService
    {
        event WallPostsChangesDelegate PrimaryWallPostsEvents;

        Task StartWatchAsync(IVkApi api, WallWatchModel model);
    }
}
