using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using PostsProvider;
using VkNet.Enums.SafetyEnums;

namespace PrimaryWallWatcher.PostsProvider
{
    public interface IPostsProvider
    {
        /// <summary>
        ///  Возвращает пост со стены wallId по его Id
        /// </summary>
        /// <param name="wallId">ID стены</param>
        /// <param name="postId">ID поста</param>
        /// <returns></returns>
        PostModel GetPostById(long wallId, long postId);

        /// <summary>
        /// Добавить пост в PostProvider
        /// </summary>
        /// <param name="wallId">ID стены</param>
        /// <param name="post">Пост</param>
        void AddPost(long wallId, [NotNull] PostModel post);

        /// <summary>
        /// Обновить пост в PostProvider
        /// </summary>
        /// <param name="wallId">ID стены</param>
        /// <param name="post">Пост</param>
        void UpdatePost(long wallId, [NotNull] PostModel post);

        /// <summary>
        /// Пытается удалить пост из PostsProvider. Возвращает true, если пост успешно удален
        /// </summary>
        /// <param name="wallId"></param>
        /// <param name="postId"></param>
        bool DeletePost(long wallId, long postId);

        /// <summary>
        /// Возвращает общее кол-во постов в PostsProvider типов postTypes
        /// </summary>
        /// <param name="postTypes">Тип постов</param>
        int GetPostsCount(long wallId, [ItemNotNull] IEnumerable<PostType> postTypes);

        /// <summary>
        /// Возвращает ID первого поста, идущего за постами попадающими в указанный период  
        /// </summary>
        /// <param name="wallId">Id стены</param>
        /// <param name="postTypes">Массив тип постов</param>
        /// <param name="period">Период до настоящего времени</param>
        long GetMinIdForPeriod(long wallId, [ItemNotNull] PostType[] postTypes, TimeSpan period);

        /// <summary>
        /// Возвращает id (num+1)-ого сверху поста
        /// </summary>
        /// <param name="wallId">Id стены</param>
        /// <param name="postTypes">Массив тип постов</param>
        /// <param name="num"></param>
        long GetIdNumPostFromTop(long wallId, [ItemNotNull] PostType[] postTypes, int num);

        /// <summary>
        /// Показывает если посты определенного типа с id большим idBorderInPreList
        /// </summary>
        /// <param name="wallId"> ID стены</param>
        /// <param name="postTypes"> Типы постов</param>
        /// <param name="idBorderInPreList"> Граничный ID</param>
        /// <returns></returns>
        bool AnyExistingPosts(long wallId, [ItemNotNull] IEnumerable<PostType> postTypes, long idBorderInPreList);

        /// <summary>
        /// Загружает список постов определенного типа из PostProvider с ID не меньшим idBorderInPreList
        /// </summary>
        /// <param name="wallId">Id стены</param>
        /// <param name="postTypes">Массив типов постов</param>
        /// <param name="idBorderInPreList">Id крайнего загружаемого поста </param>
        /// <returns></returns>
        List<PostModel> GetPreList(long wallId, [ItemNotNull] IEnumerable<PostType> postTypes, long idBorderInPreList);
    }
}
