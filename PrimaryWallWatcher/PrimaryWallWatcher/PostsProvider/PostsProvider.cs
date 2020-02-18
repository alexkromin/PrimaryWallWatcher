using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using PostsProvider;
using VkNet.Enums.SafetyEnums;

namespace PrimaryWallWatcher.PostsProvider
{
    public class PostsProvider : IPostsProvider
    {
        // wallId - Dict<postId, post>
        private readonly ConcurrentDictionary<long, Dictionary<long, PostModel>> _posts = new ConcurrentDictionary<long, Dictionary<long, PostModel>>();
        // wallId - Dict<PostType, posts>
        private readonly ConcurrentDictionary<long, Dictionary<PostType, List<PostModel>>> _postsByType =  new ConcurrentDictionary<long, Dictionary<PostType, List<PostModel>>>();
        
        public void AddPost(long wallId, PostModel post) 
        {
            if (!_posts.ContainsKey(wallId))
            {
                _posts[wallId] = new Dictionary<long, PostModel> { [post.PostId] = post };
                _postsByType[wallId] = new Dictionary<PostType, List<PostModel>> { [post.Post.PostType] = new List<PostModel>{ post } };
                return;
            }
            _posts[wallId][post.PostId] = post;
            if (!_postsByType[wallId].ContainsKey(post.Post.PostType))
            {
                _postsByType[wallId][post.Post.PostType] = new List<PostModel> { post };
                return;
            }
            _postsByType[wallId][post.Post.PostType].Insert(0, post);                   

        }

        public void UpdatePost(long wallId, PostModel post)
        {
            if (_posts[wallId][post.PostId] != null)
            {
                _posts[wallId][post.PostId] = post;
            }
            else
                AddPost(wallId, post);
        }

        public bool DeletePost(long wallId, long postId)
        {
            if (_posts[wallId][postId] != null && _posts[wallId][postId].PostStatus == 1)
            {
                _posts[wallId][postId].PostStatus = 0; // marked as deleted
                return true;
            }
            return false;
        }

        public PostModel GetPostById(long wallId, long postId)
        {
            if (_posts.ContainsKey(wallId))
                if (_posts[wallId].ContainsKey(postId))
                    return _posts[wallId][postId];
            throw new Exception($"PostsProvider doesn't contain any elements with id:{postId}");
        }

        public List<PostModel> GetPreList(long wallId, IEnumerable<PostType> postTypes, long idBorderInPreList)
        {
            // TODO: что будем делать с сортировкой
            if (_posts.ContainsKey(wallId))
            {
                return _posts[wallId]
                    .Where(e => e.Key >= idBorderInPreList && postTypes.Contains(e.Value.Post.PostType) && e.Value.PostStatus == 1)
                    .Select(e => e.Value)
                    .OrderByDescending(e => e.PostId)
                    .ToList();
            }

            return new List<PostModel>();
        }

        public int GetPostsCount(long wallId, IEnumerable<PostType> postTypes)
        {
            int res = 0;
            if (_postsByType.ContainsKey(wallId))
            {
                foreach (var postType in postTypes)
                {
                    if (_postsByType[wallId][postType] != null)
                        res += _postsByType[wallId][postType].Count(e => e.PostStatus == 1);
                }
            }
            return res;
        }

        public long GetMinIdForPeriod(long wallId, PostType[] postTypes, TimeSpan period)
        {
           DateTime border = DateTime.Now - period;
           List<long> r = new List<long>(postTypes.Count());
           if (_posts.ContainsKey(wallId))
           {
               foreach (var postType in postTypes)
               {
                   if (_postsByType[wallId].ContainsKey(postType))
                       r.Add(_postsByType[wallId][postType].Where(e => e.Post.Date < border).Take(1).FirstOrDefault()?.Post?.Id ?? 0);
               }

               if (r.Any())
                   return r.Max();
           }
           return 0;
        }

        public long GetIdNumPostFromTop(long wallId, PostType[] postypes, int num)
        {
            IEnumerable<long> list = Array.Empty<long>();
            if (_postsByType.ContainsKey(wallId))
            {
                foreach (var postType in postypes)
                {
                    if (_postsByType[wallId].ContainsKey(postType)) 
                        list = list.Concat(_postsByType[wallId][postType].Where(e => e.PostStatus == 1).Take(num).Select((e => e.Post.Id.Value)));
                }

                if (list.Any())
                    return list.OrderByDescending(e => e).Skip(1000).Take(1).FirstOrDefault(); // может ли вернуть нулл?
            }

            return 0;
        }

        public bool AnyExistingPosts(long wallId, IEnumerable<PostType> postTypes, long idBorderInPreList)
        {
            if (_postsByType.ContainsKey(wallId))
            {
                foreach (var postType in postTypes)
                {
                    if (_postsByType[wallId].ContainsKey(postType))
                        if (_postsByType[wallId][postType].Any(x => x.Post.Id > idBorderInPreList))
                            return true;
                }
            }
            return false;
        }
    }
}