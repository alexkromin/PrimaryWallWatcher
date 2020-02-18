using System;
using VkNet.Model.Attachments;
using VkNetExtend.WallWatcher.Models;

namespace PrimaryWallWatcher.Models
{
    public class PrimaryWallEvent
    {
        public PrimaryWallTypeEvents EventType { get; private set; }

        public long PostId { get; private set; }

        public Post Post { get; private set; }

        public PrimaryWallEvent(PrimaryWallTypeEvents eventType, Post post)
        {
            EventType = eventType;
            PostId = post?.Id ?? throw new ArgumentNullException(nameof(post));
            Post = post;
        }

        public PrimaryWallEvent(PrimaryWallTypeEvents eventType, long postId)
        {
            EventType = eventType;
            PostId = postId;
        }
    }
}
