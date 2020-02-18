using System;
using System.Collections.Generic;
using System.Linq;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using VkNet.Abstractions;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNetExtend.WallWatcher.Models;

namespace PrimaryWallWatcher.Extensions
{
    public static class WallCategoryExtension
    {
        private const int MaxLoadCount = 100;
        private static readonly string NewLine = Environment.NewLine;

        public static List<Post> GetPosts(this IWallCategory wall,
            ILogger logger,
            DownloadingPostsOptions downloadingPostsOptions)
        {
            List<Post> res = new List<Post>();
            ulong offset = 0;
            int loadCount = downloadingPostsOptions.CountInFirstLoad;
            while (true)
            {
                WallGetObject wallPosts = LoadPosts(wall, logger, downloadingPostsOptions, loadCount, offset);
                foreach (var post in wallPosts.WallPosts)
                    res.Add(post);

                if (wallPosts.WallPosts.Count < loadCount)
                    break;
                offset += (ulong)loadCount;
                loadCount = MaxLoadCount;
            }
            return res;
        }

        public static List<Post> GetPosts(this IWallCategory wall,
            ILogger logger,
            DownloadingPostsOptions downloadingPostsOptions,
            long border)
        {
            List<Post> res = new List<Post>();
            bool allPostsReceived = true;
            ulong offset = 0;
            int loadCount = downloadingPostsOptions.CountInFirstLoad;
            while (allPostsReceived)
            {
                WallGetObject wallPosts = LoadPosts(wall, logger, downloadingPostsOptions, loadCount, offset);
                res.AddRange(SelectionLoadedPosts(wallPosts, ref allPostsReceived, border));

                if (wallPosts.WallPosts.Count < downloadingPostsOptions.CountInFirstLoad)
                    allPostsReceived = false;
                offset += (ulong)downloadingPostsOptions.CountInFirstLoad;
                loadCount = MaxLoadCount;
            }
            return res;
        }

        /// <summary>
        /// Загрузка постов по лимитам установленным в модели (Все посты за период ИЛИ заданное кол-во постов)
        /// </summary>
        /// <param name="wall"></param>
        /// <param name="downloadingPostsOptions"></param>
        /// <param name="monitoringLimits"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static List<Post> GetPosts(this IWallCategory wall,
            ILogger logger,
            DownloadingPostsOptions downloadingPostsOptions,
            MonitoringLimits monitoringLimits)
        {
            List<Post> res = new List<Post>();
            int loadCount = downloadingPostsOptions.CountInFirstLoad;
            bool allPostsReceived = true;
            ulong offset = 0;
            long? monitoringNumBorder = null;
            DateTime? monitoringTimeBorder = null;

            if (monitoringLimits.MonitoringLimit != null)
                monitoringNumBorder = 0;
            if (monitoringLimits.MonitoringPeriod != null)
                monitoringTimeBorder = DateTime.UtcNow - monitoringLimits.MonitoringPeriod;

            while (allPostsReceived)
            {
                WallGetObject wallPosts = LoadPosts(wall, logger, downloadingPostsOptions, loadCount, offset);
                if (wallPosts.WallPosts.Count != 0)
                {
                    if (monitoringNumBorder == null)
                        res.AddRange(SelectionLoadedPosts(wallPosts, ref allPostsReceived, monitoringTimeBorder.Value));
                    else if (monitoringTimeBorder == null)
                        res.AddRange(SelectionLoadedPosts(wallPosts, ref allPostsReceived, ref monitoringNumBorder, monitoringLimits.MonitoringLimit.Value));
                    else
                        res.AddRange(SelectionLoadedPosts(wallPosts, ref allPostsReceived, monitoringTimeBorder.Value, ref monitoringNumBorder, monitoringLimits.MonitoringLimit.Value));

                    if (wallPosts.WallPosts.Count < loadCount)
                        allPostsReceived = false;
                    offset += (ulong)loadCount;
                    loadCount = MaxLoadCount;
                }
            }

            return res;
        }

        /// <summary>
        /// Из загруженных постов выбирает сверху все, начиная с самого нового и заканчивая постом с id=border
        /// </summary>
        /// <param name="wallPosts">Посты, из которых надо выбрать необходимые</param>
        /// <param name="allPostsReceived">Флаг, отвечающий, что все необходимые посты выбраны</param>
        /// <param name="border">id последнего поста, который нужно выбрать</param>
        /// <returns></returns>
        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool allPostsReceived, long border)
        {
            List<Post> res = new List<Post>();
            for (int i = 0; i < wallPosts.WallPosts.Count; i++)
            {
                var post = wallPosts.WallPosts.ElementAt(i);
                if (post.Id >= border)
                    res.Add(post);
                else
                {
                    allPostsReceived = false;
                    break;
                }
            }
            return res;
        }

        /// <summary>
        /// Из загруженных постов выбирает все, начиная с самого нового и заканчивая постом с датой публикации не больше monitoringTimeBorder
        /// </summary>
        /// <param name="wallPosts">Посты, из которых надо выбрать необходимые</param>
        /// <param name="allPostsReceived">Флаг, отвечающий, что все необходимые посты выбраны</param>
        /// <param name="monitoringTimeBorder">Временная граница</param>
        /// <returns></returns>
        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool allPostsReceived, DateTime monitoringTimeBorder)
        {
            List<Post> res = new List<Post>();
            for (int i = 0; i < wallPosts.WallPosts.Count; i++)
            {
                var post = wallPosts.WallPosts.ElementAt(i);
                if (post.Date >= monitoringTimeBorder)
                    res.Add(post);
                else
                {
                    allPostsReceived = false;
                    break;
                }
            }
            return res;
        }

        /// <summary>
        /// Из загруженных постов выбирает максимальное количество не превышающий параметр monitoringLimit(количество). Назад возвращает сколько постов выбрано (monitoringNumBorder)
        /// </summary>
        /// <param name="wallPosts">Посты, из которых надо выбрать необходимые</param>
        /// <param name="allPostsReceived">>Флаг, отвечающий, что все необходимые посты выбраны</param>
        /// <param name="monitoringNumBorder"></param>
        /// <param name="monitoringLimit"></param>
        /// <returns></returns>
        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool allPostsReceived, ref long? monitoringNumBorder, int monitoringLimit)
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
                    allPostsReceived = false;
                    break;
                }
            }
            return res;
        }

        /// <summary>
        /// Из загруженных постов выбирает сверху все, пока не дойдет до границы: будет  выбрано не меньше monitoringLimit штук и и все посты до monitoringTimeBorder
        /// </summary>
        /// <param name="wallPosts">Посты, из которых надо выбрать необходимые</param>
        /// <param name="allPostsReceived">Флаг, отвечающий, что все необходимые посты выбраны</param>
        /// <param name="monitoringTimeBorder">Временнная граница</param>
        /// <param name="monitoringNumBorder">Количество выбранных постов</param>
        /// <param name="monitoringLimit">Количественная граница</param>
        /// <returns></returns>
        private static List<Post> SelectionLoadedPosts(WallGetObject wallPosts, ref bool allPostsReceived, DateTime monitoringTimeBorder, ref long? monitoringNumBorder, int monitoringLimit)
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
                    allPostsReceived = false;
                    break;
                }
            }
            return res;
        }

        private static WallGetObject LoadPosts(IWallCategory wall,
            ILogger logger,
            DownloadingPostsOptions downloadingPostsOptions,
            int count,
            ulong offset)
        {
            byte countOfLoadingAttempts = 1;
            DateTime? firstDisconnectionTime = null;
            while (countOfLoadingAttempts <= 5)
            {
                try
                {
                    // получаем следующую сотню записей со стены
                    var wallPosts = wall.Get(new VkNet.Model.RequestParams.WallGetParams()
                    {
                        OwnerId = downloadingPostsOptions.WallId,
                        Offset = offset,
                        Count = Convert.ToUInt64(count),
                        Extended = false,
                        Filter = downloadingPostsOptions.WallFilter,
                    });
                    return wallPosts;
                }
                catch (FlurlHttpException ex)
                {
                    logger.LogInformation($"There's no connection to the Internet. Waiting for a reconnection.... Bz-z-z");
                    if (firstDisconnectionTime == null)
                        firstDisconnectionTime = DateTime.UtcNow;
                    if (DateTime.UtcNow - firstDisconnectionTime > downloadingPostsOptions.TimeForReconnection)
                    {
                        logger.LogInformation($"No Internet connection. Try to pray to God or just resolve connection problem.{NewLine}{ex}");
                        throw;
                    }
                }
                catch (CaptchaNeededException ex)
                {
                    logger.LogInformation($"Captcha problem. Apply to poor indian children.{NewLine}{ex}");
                    throw;
                }
                catch (VkApiException vkApiEx)
                {
                    if (countOfLoadingAttempts == 5)
                    {
                        logger.LogInformation($"VkApiException has occured.{NewLine}{vkApiEx.Message}");
                        throw;
                    }
                    countOfLoadingAttempts++;
                }
                catch (System.Exception ex)
                {
                    logger.LogInformation($"Unpredictable exception happened. Say \"Vladik Spasi\".{NewLine}{ex}");
                    throw ex;
                }
            }
            return null;
        }
    }
}
