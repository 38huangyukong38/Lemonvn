using System.Collections.Concurrent;
using GalgameManager.Helpers;

namespace GalgameManager.Models.BgTasks;

public abstract class QueueTaskBase<TQueueItem> : BgTaskBase where TQueueItem : notnull
{
    public ConcurrentQueue<TQueueItem> Queue = new();
    protected virtual int MaxRunning() => 3;
    private readonly ConcurrentBag<TQueueItem> _fetchingItems = [];
    protected readonly object ChangeMsgLock = new();

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal()
    {
        return Task.Run((async Task () =>
        {
            await InitializeAsync();
            await Task.Delay(500); //防止创建任务时立即结束
            while (true)
            {
                if (Queue.IsEmpty && _fetchingItems.IsEmpty) break;
                while (_fetchingItems.Count < MaxRunning() && Queue.TryDequeue(out TQueueItem? s))
                {
                    _fetchingItems.Add(s);
                    _ = Task.Run(async ()=>
                    {
                        await ProcessItemAsync(s);
                    }).ContinueWith(t =>
                    {
                        _fetchingItems.TryTake(out _);
                        UpdateProgressMsg();
                    });
                }
                UpdateProgressMsg();
                await Task.Delay(500);
            }
        })!);
    }
    
    protected void UpdateProgressMsg()
    {
        lock (ChangeMsgLock)
        {
            var msg = string.Empty;
            msg += $"{ProgressTitle().GetLocalized(Queue.Count)}";
            if (_fetchingItems.Count > 0)
            {
                msg = _fetchingItems.Aggregate(msg, (current, item) => current + ProgressMsg(item));
                msg += $"\n{ProgressWaitingMsg().GetLocalized(Queue.Count)}";
            }
            else
                msg += "QueueTaskBase_Empty".GetLocalized();
            ChangeProgress(_fetchingItems.Count, _fetchingItems.Count + Queue.Count, msg);
        }
    }

    public override bool OnSearch(string key) => true;
    
    protected abstract Task ProcessItemAsync(TQueueItem item);

    /// 应该返回一个“xxxx {0}”，其中{0}将会被填入当前正在处理的item数目
    protected abstract string ProgressTitle();

    /// 返回当前处理item->string的映射，用于更新当前进度
    protected abstract string ProgressMsg(TQueueItem item);
    
    /// 应该返回一个“xxxx {0}”，其中{0}将会被填入当等待前队列长度
    protected abstract string ProgressWaitingMsg();

    protected virtual Task InitializeAsync() => Task.CompletedTask;
}