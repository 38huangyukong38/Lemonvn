using GalgameManager.Helpers;

namespace GalgameManager.Models.Exceptions;

public class PvnPathNotExist : PvnException
{
    public PvnPathNotExist(string path) : base("PathNotExist_Brief".GetLocalized())
    {
        Path = path;
        FullMsg = "PathNotExist".GetLocalized(path);
    }

    public string Path { get; }
}
