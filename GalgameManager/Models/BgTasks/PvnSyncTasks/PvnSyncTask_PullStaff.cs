using System.Diagnostics.CodeAnalysis;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using PotatoVN.Client.Model;
using Career = GalgameManager.Enums.Career;

namespace GalgameManager.Models.BgTasks;

[SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
[SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
public class PvnSyncTaskPullStaff(
    IStaffService staffService,
    IGalgameCollectionService gameService,
    IInfoService infoService)
    : QueueTaskBase<StaffDto>
{
    protected override int MaxRunning() => 20;
    public override string Title => "PvnSyncTask_PullStaff_Title".GetLocalized();
    public string Result { get; private set; } = string.Empty;

    public void AddRange(IList<StaffDto> staffList)
    {
        foreach (StaffDto staff in staffList)
            Queue.Enqueue(staff);
        UpdateProgressMsg();
    }

    protected async override Task ProcessItemAsync(StaffDto dto)
    {
        await UiThreadInvokeHelper.InvokeAsync(async Task () =>
        {
            try
            {
                Staff staff = staffService.GetStaff(new StaffIdentifier
                    {
                        ChineseName = dto.ChineseName,
                        EnglishName = dto.EnglishName,
                        JapaneseName = dto.JapaneseName,
                    }
                    .SetId(RssType.Bangumi, dto.BgmId)
                    .SetId(RssType.Vndb, dto.VndbId)
                    .SetId(RssType.Ymgal, dto.YmgalId)
                    .SetId(RssType.PotatoVn, dto.Id.ToString())) ?? new Staff();

                if (dto.IsDeleted)
                {
                    staffService.Delete(staff);
                    Result += "PvnSyncTask_PullStaff_Deleted".GetLocalized(staff.Name ?? string.Empty, dto.Id) + "\n";
                    return;
                }

                staff.Ids[(int)RssType.PotatoVn] = dto.Id.ToString();
                staff.Ids[(int)RssType.Bangumi] = dto.BgmId;
                staff.Ids[(int)RssType.Vndb] = dto.VndbId;
                staff.Ids[(int)RssType.Ymgal] = dto.YmgalId;
                staff.JapaneseName = dto.JapaneseName ?? staff.JapaneseName;
                staff.EnglishName = dto.EnglishName ?? staff.EnglishName;
                staff.ChineseName = dto.ChineseName ?? staff.ChineseName;
                if (dto.Gender is not null) staff.Gender = (Gender)dto.Gender;
                staff.Description = dto.Description ?? staff.Description;

                if (dto.BirthDateTimestamp > 0)
                {
                    staff.BirthDate = DateTimeOffset.FromUnixTimeSeconds(dto.BirthDateTimestamp).LocalDateTime;
                    if (staff.BirthDate.Value.Year < 1900 || staff.BirthDate.Value > DateTime.Now)
                        staff.BirthDate = new DateTime(1, staff.BirthDate.Value.Month, staff.BirthDate.Value.Day);
                }

                staff.ImagePath = await DownloadHelper.DownloadAndSaveImageWithDiffThread(dto.ImageUrl) ??
                                  await DownloadHelper.DownloadAndSaveImageWithDiffThread(dto.ExternalImageLink) ??
                                  staff.ImagePath;

                UiThreadInvokeHelper.IgnoreComException(() =>
                {
                    HashSet<Galgame> newList = [];
                    foreach (StaffGameDto tmp in dto.StaffGames)
                    {
                        Galgame? game = gameService.GetGalgameFromId(tmp.GameId.ToString(), RssType.PotatoVn);
                        if (game is null) continue;
                        staff.AddGame(game, tmp.Relation.Select(r => (Career)r).ToList());
                        newList.Add(game);
                    }

                    List<StaffGame> toRemove = staff.Games.Where(sg => !newList.Contains(sg.Game)).ToList();
                    foreach (StaffGame sg in toRemove)
                        staff.RemoveGame(sg.Game);
                });

                staffService.Save(staff, false);
                Result += "PvnSyncTask_PullStaff_Success".GetLocalized(staff.Name ?? string.Empty,
                    staff.Ids[(int)RssType.PotatoVn] ?? string.Empty) + "\n";
            }
            catch (Exception e)
            {
                infoService.DeveloperEvent(msg: $"Exception on Sync Staff: {e.Message}");
                Result += "PvnSyncTask_PullStaff_Error".GetLocalized(dto.JapaneseName ??
                                                                     dto.ChineseName ??
                                                                     dto.EnglishName ?? string.Empty) + "\n";
            }
        });
    }

    protected override string ProgressTitle() => "PvnSyncTask_PullStaff_Progress";

    protected override string ProgressMsg(StaffDto item) =>
        item.JapaneseName ?? item.ChineseName ?? item.EnglishName ?? string.Empty;

    protected override string ProgressWaitingMsg() => "PvnSyncTask_PullStaff_Waiting";
}