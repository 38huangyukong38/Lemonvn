using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace GalgameManager.Helpers;

internal static class Hr
{
    // 等价于 C 宏：HRESULT_FROM_WIN32(x)
    public static int FromWin32(WIN32_ERROR err)
    {
        var x = (int)err;
        if (x <= 0) return x;
        return unchecked((int)(0x80070000 | (uint)x));
    }
}

public enum PickerResult
{
    None = 0,
    OK = 1,
    Cancel = 2,
    Abort = 3
}

[SupportedOSPlatform("windows6.0.6000")]
public abstract class PvnBasePicker
{
    internal IFileOpenDialog? _dialog;
    public string? Title { get; set; } = null;
    public string? OkButtonLabel { get; set; } = null;
    public string? InitialDirectory { get; set; } = null;
    public bool ChangeCurrentDirectory { get; set; } = false;
    public bool ForceFileSystem { get; set; } = true;

    public string? SelectedPath { get; protected set; }

    public virtual void ConfigureFilters()
    {
    }

    public virtual void ConfigureExtra()
    {
    }

    internal virtual FILEOPENDIALOGOPTIONS CreateOptions()
    {
        FILEOPENDIALOGOPTIONS opts = default;

        if (ForceFileSystem)
            opts |= FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;

        if (!ChangeCurrentDirectory)
            opts |= FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR;
        return opts;
    }

    protected virtual void InitResult()
    {
        if (_dialog is null) throw new InvalidOperationException("Dialog not initialized.");
        _dialog.GetResult(out IShellItem? shellItem);
        shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out PWSTR psz);
        SelectedPath = psz.ToString();
        unsafe
        {
            var p = (nint)psz.Value;
            if (p != 0) Marshal.FreeCoTaskMem(p);
        }
    }

    public virtual PickerResult ShowDialog(nint owner = 0)
    {
        unsafe
        {
            // 确保在 STA（WinUI/WPF 主线程默认 STA）
            _ = PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED);
        }

        try
        {
            HRESULT hr;

            var obj = Activator.CreateInstance(
                          Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"))!)
                      ?? throw new Win32Exception("Failed to create FileOpenDialog instance.");
            _dialog = (IFileOpenDialog)obj;

            FILEOPENDIALOGOPTIONS options = CreateOptions();
            _dialog.SetOptions(options);

            try
            {
                // Check if file
                if (InitialDirectory != null) 
                {
                    InitialDirectory = Path.GetFullPath(InitialDirectory);
                    if (!File.GetAttributes(InitialDirectory).HasFlag(FileAttributes.Directory))
                    {
                        InitialDirectory = Path.GetDirectoryName(InitialDirectory);
                    }
                }
            }
            catch (Exception)
            {
                InitialDirectory = null;
            }

            if (!string.IsNullOrWhiteSpace(InitialDirectory))
            {
                hr = PInvoke.SHCreateItemFromParsingName(InitialDirectory, null, typeof(IShellItem).GUID,
                    out var itemObj);
                if (hr.Succeeded && itemObj is IShellItem folderItem)
                {
                    _dialog.SetFolder(folderItem);
                }
                else if (hr.Failed)
                {
                    if (hr.Value != (uint)WIN32_ERROR.ERROR_FILE_NOT_FOUND)
                    {
                        Marshal.ThrowExceptionForHR(hr);
                    }
                }
            }

            if (!string.IsNullOrEmpty(Title))
                _dialog.SetTitle(Title);
            if (!string.IsNullOrEmpty(OkButtonLabel))
                _dialog.SetOkButtonLabel(OkButtonLabel);

            ConfigureFilters();
            ConfigureExtra();

            HWND hwnd = owner != 0 ? (HWND)owner : PInvoke.GetActiveWindow();
            _dialog.Show(hwnd);
            InitResult();
            return PickerResult.OK;
        }
        catch (COMException ex)
        {
            var hrCancelled = Hr.FromWin32(WIN32_ERROR.ERROR_CANCELLED);
            return ex.HResult == hrCancelled ? PickerResult.Cancel : PickerResult.Abort;
        }
        finally
        {
            PInvoke.CoUninitialize();
        }
    }
}

[SupportedOSPlatform("windows6.0.6000")]
public sealed class PvnFolderPicker : PvnBasePicker
{
    internal override FILEOPENDIALOGOPTIONS CreateOptions()
    {
        FILEOPENDIALOGOPTIONS opts = base.CreateOptions();
        opts |= FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS;
        return opts;
    }
}

[SupportedOSPlatform("windows6.0.6000")]
public sealed class PvnFilePicker : PvnBasePicker, IDisposable
{
    private readonly List<nint> memPool = new();

    public List<string> SelectedFiles = [];

    public List<Filter> Filters { get; set; } = new();
    public bool AllowMultiSelect { get; set; } = false;

    public void Dispose()
    {
        foreach (var ptr in memPool) Marshal.FreeHGlobal(ptr);
        memPool.Clear();
    }

    internal override FILEOPENDIALOGOPTIONS CreateOptions()
    {
        FILEOPENDIALOGOPTIONS opts = base.CreateOptions();
        if (AllowMultiSelect)
            opts |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;
        return opts;
    }

    public override void ConfigureFilters()
    {
        if (Filters.Count == 0)
            return;

        COMDLG_FILTERSPEC[] specs = new COMDLG_FILTERSPEC[Filters.Count];

        for (var i = 0; i < Filters.Count; i++)
        {
            unsafe
            {
                var namePtr = Marshal.StringToHGlobalUni(Filters[i].Name);
                var patternPtr = Marshal.StringToHGlobalUni(Filters[i].Pattern);
                memPool.Add(namePtr);
                memPool.Add(patternPtr);

                specs[i] = new COMDLG_FILTERSPEC
                {
                    pszName = new PCWSTR((char*)namePtr),
                    pszSpec = new PCWSTR((char*)patternPtr)
                };
            }
        }

        _dialog.SetFileTypes(specs);
    }

    protected override void InitResult()
    {
        if (_dialog is null) throw new InvalidOperationException("Dialog not initialized.");
        if (!AllowMultiSelect)
        {
            base.InitResult();
            return;
        }

        _dialog.GetResults(out IShellItemArray? items);
        if (items is not null)
        {
            items.GetCount(out var count);
            SelectedFiles.Clear();
            for (uint i = 0; i < count; i++)
            {
                items.GetItemAt(i, out IShellItem? si);
                si.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out PWSTR psz);
                var path = psz.ToString();
                unsafe
                {
                    var p = (nint)psz.Value;
                    if (p != 0) Marshal.FreeCoTaskMem(p);
                }

                if (!string.IsNullOrEmpty(path))
                    SelectedFiles.Add(path);
            }

            if (SelectedFiles.Count > 0)
                SelectedPath = SelectedFiles[0];
        }
    }

    public override PickerResult ShowDialog(IntPtr owner = 0)
    {
        try
        {
            return base.ShowDialog(owner);
        }
        finally
        {
            Dispose();
        }
    }

    public sealed class Filter
    {
        public string Name { get; set; } = "All Files";
        public string Pattern { get; set; } = "*.*";
    }
}