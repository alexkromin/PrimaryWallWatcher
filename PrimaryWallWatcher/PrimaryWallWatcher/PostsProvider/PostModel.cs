using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using VkNet.Model.Attachments;

namespace PostsProvider
{
    public class PostModel
    {

        public Post Post;
        public string ContentHash { get; set; }
        public int PostStatus { get; set; } = 0;  // 0 - deleted, 1 - exists
        public DateTime EditingDateTime { get; set; }



        public PostModel(Post post, int postStatus)
        {
            Post = post;

            ContentHash = GetContentHash(post);

            PostStatus = postStatus;

            EditingDateTime = DateTime.UtcNow;
        }

        public static string GetContentHash(Post post)
        {
            return GetMd5Hash(post.Text + string.Join(" ", post.Attachments));
        }


        //////////////////////////////////////
        private static string GetMd5Hash(string input)
        {
            MD5 md5Hasher = MD5.Create();

            byte[] data = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(input));

            StringBuilder sBuilder = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }
    }
}
