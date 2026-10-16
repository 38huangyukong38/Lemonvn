using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class BasicDialog
{
    public bool CheckBoxChecked { get; private set; }
    public bool PrimaryButtonClicked { get; private set; }
    public string InputText => InputTextBox.Text;
    
    public BasicDialog(string title, string? message = null, string? primaryButton = null, string? cancelButton = null,
        double? minWidth = null,
        string? checkBoxText = null, bool inputBox = false, string? inputBoxPlaceHolder = null)
    {
        InitializeComponent();
        Title = title;
        TextBlock.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        TextBlock.Text = message;
        InputTextBox.Visibility = inputBox ? Visibility.Visible : Visibility.Collapsed;
        InputTextBox.PlaceholderText = inputBoxPlaceHolder ?? string.Empty;
        PrimaryButtonText = primaryButton ?? "Yes".GetLocalized();
        CloseButtonText = cancelButton ?? "Cancel".GetLocalized();
        CheckBox.Visibility = checkBoxText is null ?  Visibility.Collapsed : Visibility.Visible;
        CheckBoxLine.Visibility = CheckBox.Visibility;
        CheckBox.Content = checkBoxText;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        
        if (minWidth.HasValue) MinWidth = minWidth.Value;
        
        PrimaryButtonClick += (_, _) =>
        {
            CheckBoxChecked = CheckBox.IsChecked ?? false;
            PrimaryButtonClicked = true;
        };
    }
}