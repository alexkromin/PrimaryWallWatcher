using VkNet.Model.Attachments;
using VkNetExtend.WallWatcher.Models;

namespace PrimaryWallWatcher.Models
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
