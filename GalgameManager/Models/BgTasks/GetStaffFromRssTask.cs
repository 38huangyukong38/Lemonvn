using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;

namespace GalgameManager.Models.BgTasks;

using RssStaffQueueItem = (Staff staff, RssType rss);

public class GetStaffFromRssTask(StaffService staffService) : QueueTaskBase<RssStaffQueueItem>
{
    public override string Title => "GetStaffFromRssTask_Title".GetLocalized();
    
    public void AddStaff(Staff staff, RssType rss)
    {
        Queue.Enqueue((staff, rss));
        UpdateProgressMsg();
    }

    protected override Task ProcessItemAsync(RssStaffQueueItem item) =>
        staffService.ParseStaffAsync(item.staff, item.rss);

    protected override string ProgressTitle() => "GetStaffFromRssTask_Progress";

    protected override string ProgressMsg(RssStaffQueueItem item) => $"{item.staff.Name} ";

    protected override string ProgressWaitingMsg() => "GetStaffFromRssTask_Progress_Waiting";
}