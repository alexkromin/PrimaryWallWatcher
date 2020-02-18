using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using PostsProvider;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.Attachments;

namespace PrimaryWallWatcher.Models
{
    public class PostsCollection
    {
        private readonly Dictionary<PostType, List<Post>> _collection = new Dictionary<PostType, List<Post>>();

        public PostsCollection()
        {
            _collection.Add(PostType.Copy, new List<Post>());
            _collection.Add(PostType.Post, new List<Post>());
            _collection.Add(PostType.Postpone, new List<Post>());
            _collection.Add(PostType.Reply, new List<Post>());
            _collection.Add(PostType.Suggest, new List<Post>());
        }

        public ICollection<Post> this[PostType type] => _collection[type];

        public IEnumerable<Post> this[IEnumerable<PostType> types]
        {
            get
            {
                return _collection.Where(e => types.Contains(e.Key))
                    .SelectMany(e => e.Value)
                    .OrderByDescending(e => e.Id)
                    .ToList();
            }
        }

        public void AddPost([NotNull] Post post)
        {
            _collection[post.PostType].Add(post);
        }

        public void AddPosts([NotNull] IEnumerable<Post> posts)
        {
            foreach (var post in posts)
                AddPost(post);
        }
    }
}
