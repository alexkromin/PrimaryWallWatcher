using System;
using System.Collections.Generic;
using System.Text;
using VkNet.Model.Attachments;

namespace VkNetExtend.WallWatcher.Models
{
    public class PrimaryWallEvent
    {
        public PrimaryWallTypeEvents EventType { get; set; }

        public long? DeletedPostId { get; set; }

        public Post Post { get; set; }

        public PrimaryWallEvent(PrimaryWallTypeEvents eventType, long? deletedPostId, Post post)
        {
            (EventType, DeletedPostId, Post) = (eventType, deletedPostId, post);
        }
    }
}
