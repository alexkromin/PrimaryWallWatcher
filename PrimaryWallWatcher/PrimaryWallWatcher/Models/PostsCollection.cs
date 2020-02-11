using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.Attachments;

namespace VkNetExtend.WallWatcher.Models
{
    public class PostsCollection
    {
        private Dictionary<PostType, List<Post>> _collection = new Dictionary<PostType, List<Post>>();

        public PostsCollection()
        {
            /*foreach (var postType in Enum.GetValues(typeof(PostType)))
            {
                _collection.Add(postType, new List<Post>());
            }*/
            _collection.Add(PostType.Copy, new List<Post>());
            _collection.Add(PostType.Post, new List<Post>());
            _collection.Add(PostType.Postpone, new List<Post>());
            _collection.Add(PostType.Reply, new List<Post>());
            _collection.Add(PostType.Suggest, new List<Post>());
        }

        public ICollection<Post> this[PostType type]
        {
            get
            { 
                // TODO: Нужно ли отслеживать этот момент, если в коллекции уже есть всегда листы!?
                if (!_collection.ContainsKey(type))
                    _collection.Add(type, new List<Post>());
                return _collection[type];
            }
        }

        public IEnumerable<Post> this[IEnumerable<PostType> types]
        {
            get
            {
                List<Post> res = new List<Post>();

                foreach (var postType in types)
                {
                    if (_collection.ContainsKey(postType))
                        res.AddRange(_collection[postType]);
                }

                return res;
            }
            /*set
            {
                foreach(var post in value)
                {
                    _collection[post.PostType].Add(post);
                }
            }*/
        }

        public void Add(Post post)
        {
            // TODO: Нужно ли это проверять,  если всегда все списки добавляются в конструкторе
            if (!_collection.ContainsKey(post.PostType))
                _collection.Add(post.PostType, new List<Post> { post });
            else
                _collection[post.PostType].Add(post);
        }

        public void Add(IEnumerable<Post> posts)
        {
            foreach (var post in posts)
                Add(post);
        }
    }
}
