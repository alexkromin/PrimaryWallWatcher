using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VkNet.Abstractions;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNetExtend.WallWatcher.Models;

namespace VkNet
{
    public static class WallCategoryExtension // TODO: Add logger
    {
        private const int MAX_LOAD_COUNT = 100;
        public static List<Post> GetPosts(this IWallCategory wall, long wallId, int countInFirstLoad, WallFilter wallFilter)
        {
            List<Post> res = new List<Post>();
            ulong offset = 0;
            int loadCount = countInFirstLoad;
            while (true)
            {
                WallGetObject wallPosts = LoadPosts(wall, wallId, wallFilter, loadCount, offset);
                foreach (var post in wallPosts.WallPosts)
                    res.Add(post);

                if (wallPosts.WallPosts.Count < loadCount)
                    break;
                offset += (ulong)loadCount;
                loadCount = MAX_LOAD_COUNT;
            }
            return res;
        }

        public static List<Post> GetPosts(this IWallCategory wall, long wallId, int countInFirstLoad, WallFilter wallFilter, long border)
        {
            List<Post> res = new List<Post>();
            bool flag = true;
            ulong offset = 0;
            int loadCount = countInFirstLoad;
            while (flag)
            {
                WallGetObject wallPosts = LoadPosts(wall, wallId, wallFilter, loadCount, offset);
                res.AddRange(SelectionLoadedPosts(wallPosts, ref flag, border));

                if (wallPosts.WallPosts.Count < countInFirstLoad)
                    flag = false;
                offset += (ulong)countInFirstLoad;
                loadCount = MAX_LOAD_COUNT;
            }
            return res;
        }

        /// <summary>
        /// Загрузка постов с определенного fromId по лимитам установленным в модели (Все посты за период ИЛИ заданное кол-во постов)
        /// </summary>
        /// <param name="wall"></param>
        /// <param name="wallId"></param>
        /// <param name="countInFirstLoad"></param>
        /// <param name="wallFilter"></param>
        /// <param name="monitoringLimits"></param>
        /// <param name="fromId"></param>
        /// <returns></returns>
        public static List<Post> GetPosts(this IWallCategory wall, long wallId, int countInFirstLoad, WallFilter wallFilter, MonitoringLimits monitoringLimits, long fromId)
        {
            List<Post> res = new List<Post>();
            int loadCount = countInFirstLoad;
            bool allPostsRecieved = true;
            ulong offset = 0;
            long? monitoringNumBorder = null;
            DateTime? monitoringTimeBorder = null;

            if (monitoringLimits.MonitoringLimit != null)
                monitoringNumBorder = 0;
            if (monitoringLimits.MonitoringPeriod != null)
                monitoringTimeBorder = DateTime.UtcNow - monitoringLimits.MonitoringPeriod;

            while (allPostsRecieved)
            {
                WallGetObject wallPosts = LoadPosts(wall, wallId, wallFilter, loadCount, offset);
                if (wallPosts.WallPosts.Count != 0)
                {
                    if (monitoringNumBorder == null)
                        res.AddRange(SelectionLoadedPosts(wallPosts, ref allPostsRecieved, monitoringTimeBorder.Value));
                    else if (monitoringTimeBorder == null)
                        res.AddRange(SelectionLoadedPosts(wallPosts, ref allPostsRecieved, ref monitoringNumBorder, monitoringLimits.MonitoringLimit.Value));
                    else
                        res.AddRange(SelectionLoadedPosts(wallPosts, ref allPostsRecieved, monitoringTimeBorder.Value, ref monitoringNumBorder, monitoringLimits.MonitoringLimit.Value));

                    if (wallPosts.WallPosts.Count < loadCount)
                        allPostsRecieved = false;
                    offset += (ulong)loadCount;
                    loadCount = MAX_LOAD_COUNT;
                }
            }

            // отсекаем все первые элементы перед постом с id = fromId
            int i = 0;
            if (fromId != 0)
            {
                while (i < res.Count && res.ElementAt(i).Id >= fromId)
                    i++;
            }
            return res.Skip(i).ToList();
        }

        /// <summary>
        /// Из загруженных постов выбирает все, начиная с самого нового и заканчивая постоv с id=border
        /// </summary>
        /// <param name="wallPosts">Посты, из которых надо выбрать необходимые</param>
        /// <param name="allPostsRecieved">Флаг, отвечающий, что все необходимые посты выбраны</param>
        /// <param name="border">id последнего поста, который нужно выбрать</param>
        /// <returns></returns>
        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool allPostsRecieved, long border)
        {
            List<Post> res = new List<Post>();
            for (int i = 0; i < wallPosts.WallPosts.Count; i++)
            {
                var post = wallPosts.WallPosts.ElementAt(i);
                if (post.Id >= border)
                    res.Add(post);
                else
                {
                    allPostsRecieved = false;
                    break;
                }
            }
            return res;
        }

        /// <summary>
        /// Из загруженных постов выбирает все, начиная с самого нового и заканчивая постом с датой публикации не больше monitoringTimeBorder
        /// </summary>
        /// <param name="wallPosts">Посты, из которых надо выбрать необходимые</param>
        /// <param name="allPostsRecieved">Флаг, отвечающий, что все необходимые посты выбраны</param>
        /// <param name="border">id последнего поста, который нужно выбрать</param>
        /// <returns></returns>
        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool allPostsRecieved, DateTime monitoringTimeBorder)
        {
            List<Post> res = new List<Post>();
            for (int i = 0; i < wallPosts.WallPosts.Count; i++)
            {
                var post = wallPosts.WallPosts.ElementAt(i);
                if (post.Date >= monitoringTimeBorder)
                    res.Add(post);
                else
                {
                    allPostsRecieved = false;
                    break;
                }
            }
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wallPosts"></param>
        /// <param name="flag"></param>
        /// <param name="monitoringNumBorder"></param>
        /// <param name="monitoringLimit"></param>
        /// <returns></returns>
        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool flag, ref long? monitoringNumBorder, int monitoringLimit)
        {
            List<Post> res = new List<Post>();
            for (int i = 0; i < wallPosts.WallPosts.Count; i++)
            {
                var post = wallPosts.WallPosts.ElementAt(i);
                if (monitoringNumBorder < monitoringLimit)
                {
                    res.Add(post);
                    monitoringNumBorder++;
                }
                else
                {
                    flag = false;
                    break;
                }
            }
            return res;
        }

        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool flag, DateTime monitoringTimeBorder, ref long? monitoringNumBorder, int monitoringLimit)
        {
            List<Post> res = new List<Post>();
            for (int i = 0; i < wallPosts.WallPosts.Count; i++)
            {
                var post = wallPosts.WallPosts.ElementAt(i);
                if (monitoringNumBorder < monitoringLimit || post.Date < monitoringTimeBorder)
                {
                    res.Add(post);
                    monitoringNumBorder++;
                }
                else
                {
                    flag = false;
                    break;
                }
            }
            return res;
        }

        private static WallGetObject LoadPosts(IWallCategory wall, long groupId, WallFilter filter, int count, ulong offset)
        {
            byte countOfLoadingAttempts = 1;
            while (countOfLoadingAttempts <= 5)
            {
                try
                {
                    // получаем следующую сотню записей со стены
                    var wallPosts = wall.Get(new Model.RequestParams.WallGetParams()
                    {
                        // Идентификатор пользователя или сообщества, со стены которого необходимо получить записи
                        OwnerId = groupId,
                        //Domain 
                        Offset = offset,
                        Count = Convert.ToUInt64(count),
                        Extended = false,
                        Filter = filter,
                        // Список дополнительных полей для профилей и групп
                        //Fields
                    });
                    return wallPosts;
                }
                catch (CaptchaNeededException ex)
                {
                    //_logger.LogError($"{nameof(VkNetExtWallWatcher)}: Error while processing wall post deletion. Failed to load posts. Offset: {offset}.{Environment.NewLine}{ex.Message}");
                    throw ex;
                }
                catch (VkApiException vkApiEx)
                {
                    if (countOfLoadingAttempts == 5)
                    {
                        //_logger.LogError($"{nameof(VkNetExtWallWatcher)}: Error while processing wall post deletion. Failed to load posts. Offset: {offset}.{Environment.NewLine}{vkApiEx.Message}");
                        throw vkApiEx;
                    }
                    countOfLoadingAttempts++;
                }
                catch (System.Exception ex)
                {
                    //_logger.LogError($"{nameof(VkNetExtWallWatcher)}: Error while processing wall post deletion. Failed to load posts. Offset: {offset}.{Environment.NewLine}{ex.Message}");
                    throw ex;
                }
            }
            return null;
        }
    }
}
