namespace GalgameManager.Helpers;

public static class DirectoryInfoExtensions
{
    public static bool IsChildOf(this DirectoryInfo self, DirectoryInfo father)
    {
        DirectoryInfo? tmp = self;
        do
        {
            if (tmp.FullName.Equals(father.FullName)) return true;
            tmp = tmp.Parent;
        } while (tmp is not null);
        return false;
    }
}
