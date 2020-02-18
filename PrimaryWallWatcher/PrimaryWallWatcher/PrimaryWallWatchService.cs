using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Flurl.Http;
using VkNet;
using VkNet.Abstractions;
using VkNet.Enums.SafetyEnums;
using VkNet.Exception;
using VkNet.Model.Attachments;
using VkNetExtend.WallWatcher.Models;

using PostsProvider;
using PrimaryWallWatcher.Extensions;
using PrimaryWallWatcher.Models;
using PrimaryWallWatcher.PostsProvider;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace PrimaryWallWatcher
{
    public class PrimaryWallWatchService : IPrimaryWallWatchService
    {
        private static readonly PostType[] _publishedType = { PostType.Post, PostType.Copy, PostType.Reply };
        private static readonly PostType[] _postponedType = { PostType.Postpone };
        private static readonly PostType[] _suggestedType = { PostType.Suggest };

        private readonly ILogger _logger;
        private readonly PrimaryWallWatcherOptions _wallWatcherOptions;
        private readonly IPostsProvider _postsProvider;

        private IVkApi _api;
        private WallWatchModel _model;

        private Timer _wallWatchTimer;

        #region PROPERTIES

        // config
        /// <summary>
        /// период ожидания между коротками проверками (то есть только на новые публикации и отслежка отложек и предложек)
        /// </summary>
        private TimeSpan? _shortCheckPeriod = null;
        /// <summary>
        /// период ожидания между длинными проверками (то есть на новые публикации, отслежка отложек и предложек и изменения опубликованных постов)
        /// </summary>
        private TimeSpan? _longCheckPeriod = null;

        // state  // TODO: implement
        /// <summary>
        /// время последней проверки на появление новых постов и редактирование предложек и отложек
        /// </summary>
        private DateTime _nextLongCheck = DateTime.MaxValue;
        /// <summary>
        /// время последней проверки на все изменения постов на стене
        /// </summary>
        private DateTime _nextShortCheck = DateTime.MaxValue;

        #endregion

        /// <summary>
        /// запущен ли WallWatcher ?
        /// </summary>
        public bool IsActive { get; private set; }

        public int MaxLoadCount => _wallWatcherOptions?.MaxLoadPostsCount ?? 100;

        #region EVENTS

        public event WallPostsChangesDelegate PrimaryWallPostsEvents;
        public event EventHandler<Exception> ProcessExceptionEvent; 

        #endregion

        public PrimaryWallWatchService(ILogger<IPrimaryWallWatchService> logger, 
            IOptions<PrimaryWallWatcherOptions> options,
            IPostsProvider postsProvider)
        {
            _logger = logger;
            _wallWatcherOptions = options.Value;
            _postsProvider = postsProvider;
        }

        public async Task StartWatchAsync(IVkApi api, WallWatchModel model)
        {
            if (!IsActive)
            {
                IsActive = true;
                _api = api;
                _model = model;

                _logger?.LogTrace($"Starting wall watcher for {ApiTargetDescriptor()}");

                _longCheckPeriod = _model.MonitoringPeriod;
                _shortCheckPeriod = _model.ShortMonitoringPeriod;

                // Do maximum coverage check first
                await ProcessWallWatch(TypesOfChecking.Long);
                _nextLongCheck = DateTime.UtcNow + model.MonitoringPeriod;
                _nextShortCheck = DateTime.UtcNow + model.ShortMonitoringPeriod;

                if (IsActive)
                {
                    _wallWatchTimer = new Timer(WatchStepAsync, null,
                        dueTime: Convert.ToInt32(_model.ShortMonitoringPeriod.TotalMilliseconds),
                        period: Timeout.Infinite);
                }
            }
            else
                _logger?.Log(LogLevel.Trace, $"Attemption to start active watcher for {ApiTargetDescriptor()}.");
        }

        protected async void WatchStepAsync(object state)
        {
            if (IsActive)
            {
                DateTime now = DateTime.UtcNow;
                if (now >= _nextLongCheck)
                {
                    _logger.LogTrace($"Long check started at {DateTime.Now}");
                    await ProcessWallWatch(TypesOfChecking.Long);
                    _nextLongCheck = now + (_longCheckPeriod ?? _model.MonitoringPeriod);
                    _nextShortCheck = now + (_shortCheckPeriod ?? _model.ShortMonitoringPeriod);
                }
                else
                {
                    _logger.LogTrace($"Short check started at {DateTime.Now}");
                    await ProcessWallWatch(TypesOfChecking.Short);
                    _nextShortCheck = now + (_shortCheckPeriod ?? _model.ShortMonitoringPeriod);
                }

                if (_nextLongCheck < _nextShortCheck)
                {
                    var fireAfter = now - _nextLongCheck;
                    _wallWatchTimer?.Change(Convert.ToInt32(fireAfter.TotalMilliseconds), Timeout.Infinite);
                }
                else
                    _wallWatchTimer?.Change(Convert.ToInt32((_shortCheckPeriod ?? _model.ShortMonitoringPeriod).TotalMilliseconds), Timeout.Infinite);

                // TODO: Добавить очистку провайдера постов
            }
            else
                _logger.LogTrace($"Wall watcher for {_model.WallId} has not been prolonged.");
        }

        #region PRIVATE METHODS

        /// <summary>
        /// проверка опубликованных постов на удаление и редактирование 
        /// </summary>
        private async Task ProcessWallWatch(TypesOfChecking checkType)
        {
            PostsCollection newPosts = null;

            #region 1. ЗАГРУЖАЕМ НОВЫЕ ПОСТЫ
            try
            {
                newPosts = await LoadWallPosts();
            }
            catch (FlurlHttpException ex)
            {
                _logger.LogError($"No Internet connection. WallWatchService ended up with error event.\n{ex}");
                ProcessExceptionEvent?.Invoke(null, ex);
            }
            catch (UserAuthorizationFailException ex)
            {
                _logger.LogError($"User authorization was failed. Try to get new api instance.\n{ex}");
                ProcessExceptionEvent?.Invoke(null, ex);
            }
            catch (VkApiException ex)
            {
                _logger.LogError($"User authorization was failed. Try to get new api instance.\n{ex}");
                ProcessExceptionEvent?.Invoke(null, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error while downloading posts in {TypesOfChecking.Long.ToString()} check in wall with id {-_model.WallId}. " +
                                 $"Try to pray to God(means developer).\n{ex}");
                ProcessExceptionEvent?.Invoke(null, ex);
            }
            #endregion

            #region 2. СВЕРКА НОВЫХ И СТАРЫХ ПОСТОВ

            List<PrimaryWallEvent> lastWallEvents = new List<PrimaryWallEvent>();
            // Сравниваем полученные посты с тем, что уже было. Пишем результаты в NewWallPosts, DeletedWallPosts, EditedWallPosts
            lastWallEvents.AddRange(ComparePosts(newPosts[_publishedType], _publishedType, idBorderInPreList));
            lastWallEvents.AddRange(ComparePosts(newPosts[_suggestedType], _suggestedType, 0));
            lastWallEvents.AddRange(ComparePosts(newPosts[_postponedType], _postponedType, 0));
            
            #endregion

            #region 3. ПУБЛИКАЦИЯ СОБЫТИЙ НА СТЕНЕ

            if (lastWallEvents.Any())
            {
                try
                {
                    PrimaryWallPostsEvents?.Invoke(lastWallEvents);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error of handling {nameof(PrimaryWallPostsEvents)}event{ex.Message}");
                }
            }

            #endregion
            
            if (lastWallEvents.Any())
            {
                _shortCheckPeriod = null;
                _longCheckPeriod = null;
            }
            else
            {
                _shortCheckPeriod = (_shortCheckPeriod ?? _model.ShortMonitoringPeriod) 
                    + _wallWatcherOptions.IncrementSleepTimeForShortCheck;
                _longCheckPeriod = (_longCheckPeriod ?? _model.MonitoringPeriod)
                    + _wallWatcherOptions.IncrementSleepTimeForLongCheck;

                if (_shortCheckPeriod > _wallWatcherOptions.MaxSleepTimeForShortCheck)
                    _shortCheckPeriod = _wallWatcherOptions.MaxSleepTimeForShortCheck;
                if (_longCheckPeriod > _wallWatcherOptions.MaxSleepTime)
                    _longCheckPeriod = _wallWatcherOptions.MaxSleepTime;
            }
        }


        #region  Загрузка постов

        private const int FirstLoadCount = 30;
        private async Task<PostsCollection> LoadWallPosts()
        {
            long latestOldPostId = -1;
            PostsCollection res = new PostsCollection();
            int loadCount = FirstLoadCount; // TODO: how to calculate

            var getParams = new WallGetParams()
            {
                OwnerId = _model.WallId
            };
            WallGetObject wallGetRes = null;


            _api.Wall.Get(getParams);


            DownloadingPostsOptions downloadingPostsOptions = new DownloadingPostsOptions(_model.WallId, loadInFirstCount, WallFilter.All, _wallWatcherOptions.TimeForReconnecting);
            // если _prePublishedList пустой, то грузим не до самого нового имющегося поста (так как его у нас нет), а за MonitoringLimits(период и/или количество)
            if (!_postsProvider.AnyExistingPosts(_model.WallId, _publishedType, 0)) 
            {
                List<Post> publishedPosts = _api.Wall.GetPosts(_logger, downloadingPostsOptions, _model.MonitoringLimits);
                res.AddPosts(publishedPosts);
            }
            else
                res.AddPosts(GetNewAndOldPublishedPosts(downloadingPostsOptions, out latestOldPostId));

            // Download suggests and postpone
            downloadingPostsOptions.CountInFirstLoad = MaxLoadCount;
            downloadingPostsOptions.WallFilter = WallFilter.Suggests;
            res.AddPosts(_api.Wall.GetPosts(_logger, downloadingPostsOptions));
            downloadingPostsOptions.WallFilter = WallFilter.Postponed;
            res.AddPosts(_api.Wall.GetPosts(_logger, downloadingPostsOptions));
            return res;
        }

        
        private List<Post> GetNewAndOldPublishedPosts(DownloadingPostsOptions downloadingPostsOptions, out long idBorderInPreList)
        {
            idBorderInPreList = 0;
            if (_typeOfCurrentChecking == TypesOfChecking.Short)
            {
                if (_model.ShortMonitoringLimits.MonitoringPeriod != null)
                    // здесь вообще должен быть не _periodOfShortCheck, а время которое реально прошло после последней короткой проверки (оно будет чуть больше)
                    idBorderInPreList = _postsProvider.GetMinIdForPeriod(_model.WallId, _publishedType, _model.ShortMonitoringLimits.MonitoringPeriod.Value + _shortCheckPeriod); // возвращает id первого поста, идущего за указанным периодом

                if (_model.ShortMonitoringLimits.MonitoringLimit != null)
                {
                    long idBorderForLimit = _postsProvider.GetIdNumPostFromTop(_model.WallId, _publishedType, _model.ShortMonitoringLimits.MonitoringLimit.Value); // возвращает id ( N+1)-ого сверху поста 
                    if (idBorderForLimit < idBorderInPreList)
                        idBorderInPreList = idBorderForLimit;
                }
            }
            else
            {
                if (_model.MonitoringLimits.MonitoringPeriod != null)
                    // здесь вообще должен быть не _periodOfShortCheck, а время которое реально прошло после последней короткой проверки (оно будет чуть больше)
                    idBorderInPreList = _postsProvider.GetMinIdForPeriod(_model.WallId, _publishedType, _model.MonitoringLimits.MonitoringPeriod.Value + _longCheckPeriod); // возвращает id первого поста, идущего за указанным периодом

                if (_model.MonitoringLimits.MonitoringLimit != null)
                {
                    long idBorderForLimit = _postsProvider.GetIdNumPostFromTop(_model.WallId, _publishedType, _model.MonitoringLimits.MonitoringLimit.Value); // возвращает id ( N+1)-ого сверху поста 
                    if (idBorderForLimit < idBorderInPreList)
                        idBorderInPreList = idBorderForLimit;
                }
            }

            List<Post> publishedPosts = _api.Wall.GetPosts(_logger, downloadingPostsOptions, idBorderInPreList);
            return publishedPosts;
        }


        #endregion

        #region Сравнение постов

        private IEnumerable<PrimaryWallEvent> ComparePosts(IEnumerable<Post> newList, IEnumerable<PostType> postTypes, long idBorderInPreList)
        {
            List<PostModel> preList = null;
            List<PrimaryWallEvent> result = new List<PrimaryWallEvent>();
            bool? anyExistingPosts = null;
            int shiftIndex = 0;         // индекс для сдвига по-новому листу, чтобы не начинать каждый раз поиск с начала листа. Сначала ставим указатель на первый элемент

            if (newList.Any())          // если в заново загруженных постах есть что-то, то сначала выбираем новые
            {
                anyExistingPosts = _postsProvider.AnyExistingPosts(_model.WallId, postTypes, idBorderInPreList);
                if (anyExistingPosts.Value)                                  // если есть старые посты данного типа
                {
                    preList = _postsProvider.GetPreList(_model.WallId, postTypes, idBorderInPreList);
                    long idBorder = preList.First().PostId;                // id первого поста в preList, то есть все посты с большим id сразу относим к новым
                    result.AddRange(FilterNewPosts(newList, idBorder, ref shiftIndex));
                }
                else
                {
                    result.AddRange(FilterNewPosts(newList, 0, ref shiftIndex));  // если старых постов нет, то все посты считаем новыми
                    return result;                                                                    // новые посты сохранены, старых нет, заканчиваем работу
                }
            }

            // ДАЛЬШЕ ПРОВЕРЯЕМ НА УДАЛЕНИЕ И РЕДАКТИРОВАНИЕ СТАРЫЕ ПОСТЫ

            if (anyExistingPosts ?? _postsProvider.AnyExistingPosts(_model.WallId, postTypes, idBorderInPreList))  // если у нас есть старые посты
            {
                if (preList == null)                                        // если прелист еще не заполнен, то заполняем его сейчас
                {
                    preList = _postsProvider.GetPreList(_model.WallId, postTypes, idBorderInPreList);
                }

                if (newList.Count() > shiftIndex+1)                         // если есть не новые
                {
                    foreach (var pre in preList)                            // перебираем все посты из preList
                    {
                        if (ContainsPost(newList, pre.PostId, ref shiftIndex, out var newPost))  // если пост не удалён, то 
                        {
                            if (_model.WatchEditing && IsPostEdited(pre, newPost)) // проверяем не отредактирован ли он. Если нет, то оставляем его в покое
                            {
                                result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.EditedWallPost, null, newPost));
                                _postsProvider.UpdatePost(_model.WallId, new PostModel(newPost, 1));
                            }
                        }
                        else                                               // если пост всё-таки удалён, то 
                        {
                            result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.DeletedWallPosts, pre.PostId, null));    // заносим его в удаленные
                            _postsProvider.DeletePost(_model.WallId, pre.PostId);
                        }
                    }
                }
                else                // если новых нет, то значит все посты удалены
                {
                    foreach (var pre in preList)
                    {
                        result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.DeletedWallPosts, pre.PostId, null)); // заносим его в удаленные
                        _postsProvider.DeletePost(_model.WallId, pre.PostId);
                    }
                }
            }
            return result;
        }

        private List<PrimaryWallEvent> FilterNewPosts(IEnumerable<Post> postsList, long borderPostId, ref int lastOldPostIndex)
        {
            List<PrimaryWallEvent> result = new List<PrimaryWallEvent>();
            for (; lastOldPostIndex < postsList.Count(); lastOldPostIndex++)
            {
                var post = postsList.ElementAt(lastOldPostIndex);
                if (post.Id > borderPostId)
                {
                    result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.NewWallPost, null, post));
                    _postsProvider.AddPost(_model.WallId, new PostModel(post, 1));
                }
                else
                    break;
            }
            return result;
        }

        private bool ContainsPost(IEnumerable<Post> postsList, long postId, ref int index, out Post post)
        {
            for (int i = index; i < postsList.Count(); i++)
            {
                Post currentPost = postsList.ElementAt(i);
                if (postId == currentPost.Id)
                {
                    post = currentPost;
                    index = i;
                    return true;
                }
                else if (currentPost.Id < postId)
                {

                }
            }
            post = null;
            return false;
        }

        private bool IsPostEdited(PostModel oldPost, Post post)
        {
            return (PostModel.GetContentHash(post) != oldPost.ContentHash);
        }

        #endregion
        #endregion


        #region Helper

        protected string ApiTargetDescriptor(bool upperCapitals = false)
        {
            if (upperCapitals)
                return _api.UserId.HasValue ? $"User {_api.UserId.Value}" : $"Token {_api.Token}";
            else
                return _api.UserId.HasValue ? $"user {_api.UserId.Value}" : $"token {_api.Token}";
        }

        #endregion

    }
}
