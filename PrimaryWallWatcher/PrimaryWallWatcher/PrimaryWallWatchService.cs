using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostsProvider;
using PrimaryWallWatcher.Models;
using PrimaryWallWatcher.PostsProvider;
using VkNet;
using VkNet.Abstractions;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.Attachments;
using VkNetExtend.WallWatcher.Models;

namespace PrimaryWallWatcher
{
    public class PrimaryWallWatchService: IPrimaryWallWatchService
    {
        #region PRIVATE MEMBERS

        private readonly IVkApi _api;
        private readonly ILogger _logger;
        private readonly IPostsProvider _postsProvider;
        private readonly IOptions<PrimaryWallWatcherOptions> _wallWatcherOptions;

        private Timer _commonWatchTimer; 
        private StartWallWatchModel _model;

        // config
        private TimeSpan _periodOfShortCheck;     // период ожидания между коротками проверками (то есть только на новые публикации и отслежка отложек и предложек)
        private TimeSpan _periodOfLongCheck;      // период ожидания между длинными проверками (то есть на новые публикации, отслежка отложек и предложек и изменения опубликованных постов)


        // state
        private DateTime? _lastLongCheckTime;    // время последней проверки на появление новых постов и редактирование предложек и отложек
        private DateTime? _lastShortCheckTime;     // время последней проверки на все изменения постов на стене
        private bool _isRunningOldPublishedPostsChecking = false;
        private long _lastIdInShortCheck = 0;

        private readonly PostType[] _publishedType = { PostType.Post, PostType.Copy, PostType.Reply };
        private readonly PostType[] _postponedType = { PostType.Postpone };
        private readonly PostType[] _suggestedType = { PostType.Suggest };


        #endregion

        #region PROPERTIES

        /// <summary>
        /// запущен ли WallWatcher ?
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public bool Enabled { get; private set; }

        #endregion

        #region EVENTS

        public event WallPostsChangesDelegate PrimaryWallPostsEvents;

        #endregion

        #region CONSTRUCTORS

        public PrimaryWallWatchService(ILogger<IPrimaryWallWatchService> logger, IPostsProvider postsProvider, IOptions<PrimaryWallWatcherOptions> options, IVkApi vkApi)
        {
            _logger = logger;
            _postsProvider = postsProvider;
            _wallWatcherOptions = options;
            _api = vkApi;
        }

        #endregion

        #region INTERFACE METHOD

        public async Task StartWatchAsync(StartWallWatchModel model)
        {
            if (!IsActive)
            {
                //ev?.Invoke(this, OrdinaryWallEvents);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger?.Log(LogLevel.Trace, $"Starting wall watcher for {ApiTargetDescriptor()}");
                }
                else
                    _logger?.Log(LogLevel.Information, $"Starting wall watcher{(_api.UserId.HasValue ? $" for user {_api.UserId.Value}." : ".")}");

                IsActive = true;
                _model = model;

                _commonWatchTimer = new Timer(WatchStepAsync, null, Timeout.Infinite, Timeout.Infinite);
                await FirstStepAsync();
            }
            else
                _logger?.Log(LogLevel.Trace, $"Attemption to start active watcher for {ApiTargetDescriptor()}.");
        }

        #endregion

        #region PRIVATE METHODS

        //private async void _firstStep() => await FirstStepAsync(); //FirstStepAsync().GetAwaiter().GetResult();
        //private async void _watchStep(object state) => await WatchStepAsync();//WatchStepAsync().GetAwaiter().GetResult();
        private async Task FirstStepAsync()
        {
            if (IsActive)                   // если не поставили на паузу
            {
                await ShortCheck();       // первый раз запускаем короткую проверку ShortCheck: грузим посты: часть опубликованных + предложки, отложки   
                await LongCheck();        // в длинной проверке загружаем оставшиеся опубликованные посты за лимит                  
                _commonWatchTimer?.Change(_periodOfShortCheck.Milliseconds, Timeout.Infinite);
            }
        }

        protected async void WatchStepAsync(object state)
        {
            if (IsActive) // если не поставили на паузу
            {
                // если время подошло и  если ранее уже не запущен данный вид проверки, то запускаем длинную проверку и забываем о ней
                if (DateTime.UtcNow - _lastLongCheckTime >= _periodOfLongCheck && !_isRunningOldPublishedPostsChecking)
                    Task.Run(()=>LongCheck()); 
                // если время подошло, запускаем проверку на новые и неопубликованные посты 
                if (DateTime.UtcNow - _lastShortCheckTime >= _periodOfShortCheck)
                    await ShortCheck();

                // сдвигаем таймер
                _commonWatchTimer?.Change(_periodOfShortCheck.Milliseconds, Timeout.Infinite);
            }
        }

        private async Task LongCheck()   // проверка опубликованных постов на удаление и редактирование 
        {
            if (!_isRunningOldPublishedPostsChecking) //
            {
                _isRunningOldPublishedPostsChecking = true;
                var lastWallEvents = await GetPostsEventsAsync(TypesOfChecking.Long).ConfigureAwait(false);
                _lastLongCheckTime = DateTime.UtcNow;

                if (lastWallEvents.Any())
                {
                    // TODO: НУжны ли здесь трай-кэтч
                    try
                    {
                        PrimaryWallPostsEvents?.Invoke(lastWallEvents);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                // TODO: реализовать перерасчёт времени
                _periodOfLongCheck = _model.PeriodBetweenLongChecks;
                // помечаем, что длинная проверка закончена
                _isRunningOldPublishedPostsChecking = false;
            }
        }

        private async Task ShortCheck()
        {
            try
            {
                var lastWallEvents = await GetPostsEventsAsync(TypesOfChecking.Short).ConfigureAwait(false);
            

            _lastShortCheckTime = DateTime.UtcNow;
            if (lastWallEvents.Any())
                PrimaryWallPostsEvents?.Invoke(lastWallEvents);

            // TODO: реализовать перерасчёт времени
            _periodOfShortCheck = _model.PeriodBetweenShortChecks;
                //_periodOfShortCheck += _model.IncrementSleepTime; 
                //if (_periodOfShortCheck > _model.MaxSleepTime)
                //    _periodOfShortCheck = _model.MaxSleepTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }





        protected async Task<IEnumerable<PrimaryWallEvent>> GetPostsEventsAsync(TypesOfChecking typeOfChecking)
        {
            PostsCollection newPosts = new PostsCollection();

            #region 1. ОПРЕДЕЛЯЕМ ГРАНИЦЫ (ВРЕМЕННЫЕ или ПО ID) И ЗАГРУЖАЕМ НОВЫЕ ПОСТЫ

            long idBorderInPreList = 0;

            try
            {
                if (typeOfChecking == TypesOfChecking.Long)
                {
                    newPosts = GetPostsForLongChecking(out idBorderInPreList);
                }
                else if (typeOfChecking == TypesOfChecking.Short)
                {
                    newPosts = GetPostsForShortChecking(out idBorderInPreList);
                }
            }
            catch (Exception ex)
            {
                // TODO:  Если не удалось загрузить, то будем некоторое время работать вхолостую
            }
            #endregion

            #region 2. СВЕРКА НОВЫХ И СТАРЫХ ПОСТОВ

            List<PrimaryWallEvent> result = new List<PrimaryWallEvent>();

            // Сравниваем полученные посты с тем, что уже было. Пишем результаты в NewWallPosts, DeletedWallPosts, EditedWallPosts
            result.AddRange(ComparePosts(newPosts[_publishedType], _publishedType, typeOfChecking, idBorderInPreList));
            result.AddRange(ComparePosts(newPosts[_suggestedType], _suggestedType, typeOfChecking, 0));
            result.AddRange(ComparePosts(newPosts[_postponedType], _postponedType, typeOfChecking, 0));


            #endregion

            return result;
        }
        #region  Загрузка постов

        private PostsCollection GetPostsForLongChecking(out long idBorderInPreList)
        {
            PostsCollection res = new PostsCollection();
            // TODO: Надо ли? 
            int loadInFirstCount = (_lastIdInShortCheck != 0 ? 100 : 30);
            List<Post> posts = _api.Wall.GetPosts(_model.WallId, loadInFirstCount, WallFilter.All, _model.MonitoringLimits, _lastIdInShortCheck);
            res.Add(posts);
            idBorderInPreList = posts.Last().Id ?? 0;
            return res;
        }

        private PostsCollection GetPostsForShortChecking(out long idBorderInPreList)
        {
            idBorderInPreList = 0;
            PostsCollection res = new PostsCollection();
            if (!_postsProvider.AnyExistingPosts(_model.WallId, _publishedType, 0)) // если _prePublishedList пустой, то грузим не до самого нового имющегося поста (так как его у нас нет), а за период
            {
                int loadInFirstCount = 30;                  // TODO: how to calcucalte???
                List<Post> publishedPosts = _api.Wall.GetPosts(_model.WallId, loadInFirstCount, WallFilter.All, _model.MonitoringLimits, 0);
                res.Add(publishedPosts);
                _lastIdInShortCheck = publishedPosts.Last()?.Id ?? 0;
                res.Add(GetNotPublishedPosts(_suggestedType, WallFilter.Suggests));
                res.Add(GetNotPublishedPosts(_postponedType, WallFilter.Postponed));
            }
            else
            {
                res.Add(GetNewAndOldPublishedPosts(out idBorderInPreList));
                res.Add(GetNotPublishedPosts(_suggestedType, WallFilter.Suggests));
                res.Add(GetNotPublishedPosts(_postponedType, WallFilter.Postponed));
            }
            return res;
        }

        private const int MaxLoadCount = 100;
        private List<Post> GetNewAndOldPublishedPosts(out long idBorderInPreList)
        {
            idBorderInPreList = 0;

            if (_model.ShortMonitoringLimits.MonitoringPeriod != null)
                // здесь вообще должен быть не _periodOfShortCheck, а время которое реально прошло после последней короткой проверки (оно будет чуть больше)
                idBorderInPreList = _postsProvider.GetMinIdForPeriod(_model.WallId, _publishedType, _model.ShortMonitoringLimits.MonitoringPeriod.Value + _periodOfShortCheck); // возвращает id первого поста, идущего за указанным периодом
            if (_model.ShortMonitoringLimits.MonitoringLimit != null)
            {
                long idBorderForLimit = _postsProvider.GetIdNPostFromTop(_model.WallId, _publishedType, _model.ShortMonitoringLimits.MonitoringLimit.Value); // возвращает id ( N+1)-ого сверху поста 
                if (idBorderForLimit < idBorderInPreList)   // TODO: берем нижнюю или верхнюю границу?
                    idBorderInPreList = idBorderForLimit;
            }
            int loadInFirstCount = 30;                  // TODO: how to calcucalte???
            List<Post> publishedPosts = _api.Wall.GetPosts(_model.WallId, loadInFirstCount, WallFilter.All, idBorderInPreList);  // ЗАМЕНИТЬ НА ВАРИК С MONITORINGLIMITS ???
            _lastIdInShortCheck = publishedPosts.Last()?.Id ?? 0;
            return publishedPosts;
        }

        private List<Post> GetNotPublishedPosts(PostType[] postTypes, WallFilter wf)
        {
            var loadInFirstCount = _postsProvider.GetPostsCount(_model.WallId, postTypes) + 20; // первый раз грузим столько же записей сколько было на предыдщуей итерации + 20 сверху
            if (loadInFirstCount > MaxLoadCount)
                loadInFirstCount = MaxLoadCount;
            // TODO: Может надо обработать?
            return _api.Wall.GetPosts(_model.WallId, loadInFirstCount, wf);
        }

        #endregion

        #region Сравнение постов

        private IEnumerable<PrimaryWallEvent> ComparePosts(IEnumerable<Post> newList, IEnumerable<PostType> postTypes, TypesOfChecking typeOfChecking, long idBorderInPreList)
        {
            List<PostModel> preList = null;
            List<PrimaryWallEvent> result = new List<PrimaryWallEvent>();
            bool? anyExistingPosts = null;
            int shiftIndex = 0;         // индекс для сдвига по-новому листу, чтобы не начинать каждый раз поиск с начала листа. Сначала ставим указатель на первый элемент


            if (newList.Any())          // если в заново загруженных постах есть что-то сначала выбираем новые
            {
                anyExistingPosts = _postsProvider.AnyExistingPosts(_model.WallId, postTypes, idBorderInPreList);
                if (anyExistingPosts.Value)                                  // если есть старые посты данного типа
                {
                    preList = _postsProvider.GetPreList(_model.WallId, postTypes, idBorderInPreList);
                    //SaveCountOfPreLists(preList.Count, postTypes); возможно нужно, чтобы грузить следующий раз? не помню крч
                    long? idBorder = preList.First().Post.Id;                // id первого поста в preList, то есть все посты с большим id сразу относим к новым
                    result.AddRange(PullingOutNewPosts(newList, idBorder, ref shiftIndex));
                }
                else
                {
                    result.AddRange(PullingOutNewPosts(newList, 0, ref shiftIndex));  // если старых постов нет, то все посты считаем новыми
                    return result;                                                    // новые посты сохранены, старых нет, заканчиваем работу
                }
            }

            // ДАЛЬШЕ ПРОВЕРЯЕМ НА УДАЛЕНИЕ И РЕДАКТИРОВАНИЕ СТАРЫЕ ПОСТЫ

            if ((anyExistingPosts ?? _postsProvider.AnyExistingPosts(_model.WallId, postTypes, idBorderInPreList)))  // если у нас есть старые посты
            {
                if (preList == null)                                        // если прелист еще не заполнен, то заполняем его сейчас
                {
                    preList = _postsProvider.GetPreList(_model.WallId, postTypes, idBorderInPreList);
                    //SaveCountOfPreLists(preList.Count, postTypes);
                }

                if (newList.Count() > shiftIndex)  // если есть не новые
                {
                    foreach (var pre in preList)                           // перебираем все посты из preList
                    {
                        Post newPost;
                        if (SearchingForTheSamePost(pre.Post.Id, newList, ref shiftIndex, out newPost))  // если пост не удалён, то 
                        {
                            if (_model.WatchEditing && IsEditedPost(pre, newPost)) // проверяем не отредактирован ли он. Если нет, то оставляем его в покое
                            {
                                result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.EditedWallPost, null, newPost));
                                _postsProvider.UpdatePost(_model.WallId, new PostModel(newPost, 1)); //TODO: check for null
                            }
                        }
                        else                                             // если пост всё-таки удалён, то 
                        {
                            result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.DeletedWallPosts, pre.Post.Id.Value, null));    // заносим его в удаленные                                                                    
                            _postsProvider.DeletePost(_model.WallId, pre.Post.Id.Value);
                        }
                    }
                }
                else                // если новых нет, то значит все посты удалены
                {
                    foreach (var pre in preList)
                    {
                        result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.DeletedWallPosts, pre.Post.Id.Value, null)); // заносим его в удаленные
                        _postsProvider.DeletePost(_model.WallId, pre.Post.Id.Value);
                    }

                }
            }
            return result;
        }

        private void PullingOutNewPosts(IEnumerable<Post> newList, List<long> NewWallPosts)
        {
            foreach (var newPost in newList)
            {
                NewWallPosts.Add(newPost.Id.Value);
                _postsProvider.AddPost(_model.WallId, new PostModel(newPost, 1));
            }
        }

        private IEnumerable<PrimaryWallEvent> PullingOutNewPosts(IEnumerable<Post> newList, long? idBorder, ref int indexOfFirstOldPost)
        {
            List<PrimaryWallEvent> result = new List<PrimaryWallEvent>();
            foreach (var newPost in newList)
            {
                if (newPost.Id > idBorder)
                {
                    result.Add(new PrimaryWallEvent(PrimaryWallTypeEvents.NewWallPost, null, newPost));
                    _postsProvider.AddPost(_model.WallId, new PostModel(newPost, 1));
                    indexOfFirstOldPost++;
                }
                else                                                      // если id загруженного поста меньше или равно idBorder, значит это уже не новый пост. 
                    return result;                                        // Прерываем цикл
            }
            return result;
        }

        private bool SearchingForTheSamePost(long? id, IEnumerable<Post> newList, ref int skipIndex, out Post post)
        {
            foreach (var n in newList.Skip(skipIndex))
            {
                if (id == n.Id)
                {
                    post = n;
                    ++skipIndex;
                    return true;
                }
            }
            post = null;
            return false;
        }

        private bool IsEditedPost(PostModel oldPost, Post post)
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
