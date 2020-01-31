using System;
using System.Collections.Generic;
using System.Text;

namespace VkNetExtend.WallWatcher.Models
{
    public enum EventWallType
    {
        Post,
        Deleted,
        Edited,
        PublishedFromSuggested,
        PublishedFromPostponed,
        PostponedFromSuggested
    }
}
