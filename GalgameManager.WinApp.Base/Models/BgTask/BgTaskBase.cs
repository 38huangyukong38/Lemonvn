using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.WinApp.Base.Helpers;
using Newtonsoft.Json;

namespace GalgameManager.Models.BgTasks;

public abstract class BgTaskBase
{
    [JsonIgnore] public static BgTaskBase Empty { get; } = new EmptyBgTask();

    /// <summary>
    /// (当前进度，总进度，信息)， 当前进度>=总进度时可以理解为任务完成
    /// </summary>
    public event Action<Progress>? OnProgress;
    /// 当任务成功时弹出通知（ChangeProcess里设置notifyWhenSuccess为true）时，自定义通知上按钮的行为
    public Action? EventAction { get; protected set; }
    /// 当任务成功时弹出通知（ChangeProcess里设置notifyWhenSuccess为true）时，自定义通知上按钮的文本
    public string? EventActionText { get; protected set; }
    public Progress CurrentProgress { get; private set; } = new();

    [JsonIgnore] public Task Task { get; private set; } = Task.CompletedTask;

    /// 是否在托盘图标上显示进度
    public virtual bool ProgressOnTrayIcon => false;

    /// <summary>
    /// 任务是否支持取消，子类可重写此属性返回true以支持取消
    /// </summary>
    [JsonIgnore] public virtual bool CanCancel => false;
    [JsonIgnore] protected CancellationTokenSource? CancellationTokenSource;
    [JsonIgnore] protected CancellationToken? CancellationToken => CancellationTokenSource?.Token;

    /// <summary>
    /// 任务是否已被取消
    /// </summary>
    [JsonIgnore] public bool IsCancelled => CancellationTokenSource?.IsCancellationRequested ?? false;

    protected bool StartFromBg;

    public Task RecoverFromJson()
    {
        StartFromBg = true;
        return RecoverFromJsonInternal();
    }

    protected abstract Task RecoverFromJsonInternal();

    public Task Run()
    {
        Task = RunInternal();
        Task.ContinueWith(t =>
        {
            ChangeProgress(-1, 1, t.Exception?.Message ?? "Task Failed");
        }, TaskContinuationOptions.OnlyOnFaulted);
        return Task;
    }

    protected abstract Task RunInternal();

    public virtual bool OnSearch(string key) => false;

    public abstract string Title { get; }

    public bool IsRunning => CurrentProgress.Current < CurrentProgress.Total && CurrentProgress.Current >= 0;

    /// <summary>
    /// 修改进度
    /// </summary>
    /// <param name="current">当前进度，若此值低于0则任务任务失败</param>
    /// <param name="total">总进度，若current>=total则认为任务完成</param>
    /// <param name="message">信息</param>
    /// <param name="notifyWhenSuccess">部分任务完成时不需要全局的提醒，若不需要提醒则将此值赋为false</param>
    protected void ChangeProgress(long current, long total, string message, bool notifyWhenSuccess = true)
    {
        CurrentProgress = new Progress
        {
            Current = current,
            Total = total,
            Message = message,
            NotifyWhenSuccess = notifyWhenSuccess
        };
        UiThreadInvokeHelper.Invoke(() =>
        {
            try
            {
                OnProgress?.Invoke(CurrentProgress);
            }
            catch (COMException)
            {
                //ignore
            }
        });
    }

    /// <summary>
    /// 取消任务
    /// </summary>
    public virtual void Cancel() => CancelAsync().GetAwaiter().GetResult();

    /// <summary>
    /// 尝试取消任务，不抛异常
    /// </summary>
    /// <returns>如果任务支持取消并成功触发取消则返回true，否则返回false</returns>
    public virtual bool TryCancel() => TryCancelAsync().GetAwaiter().GetResult();

    /// <summary>
    /// 异步取消任务
    /// </summary>
    public async virtual Task CancelAsync()
    {
        if (!CanCancel)
            throw new InvalidOperationException("Task is not cancellable.");
        if (!IsRunning)
            throw new InvalidOperationException("Task is not running.");
        if (CancellationTokenSource == null)
            throw new InvalidOperationException("CancellationTokenSource is null.");
        if (!IsCancelled)
            await CancellationTokenSource.CancelAsync();
    }

    /// <summary>
    /// 异步尝试取消任务，不抛异常
    /// </summary>
    /// <returns>如果任务支持取消并成功触发取消则返回true，否则返回false</returns>
    public async virtual Task<bool> TryCancelAsync()
    {
        if (!CanCancel)
            return false;
        try
        {
            await CancelAsync();
        }
        catch
        {
            return false;
        }
        return true;
    }
}
