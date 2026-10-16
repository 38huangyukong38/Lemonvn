using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using GalgameManager.WinApp.Base.Contracts.NavigationApi.NavigateParameters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.Mvvm.Input;
using DependencyPropertyGenerator;

namespace GalgameManager.Views.GalgamePagePanel;

[DependencyProperty("AddCharacterCommand", typeof(IRelayCommand<GalgameCharacter?>))]
[DependencyProperty("DeleteCharacterCommand", typeof(IRelayCommand<GalgameCharacter?>))]
public partial class GameCharacterPanel
{
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    private GalgameCharacter? _currentContextCharacter;

    public GameCharacterPanel()
    {
        InitializeComponent();
    }

    protected override void Update() =>
        Panel.Visibility = Game?.Characters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not GalgameCharacter character) return;
        _navigationService.NavigateTo(typeof(GalgameCharacterViewModel).FullName!,
            new GalgameCharacterPageNavParameter { GalgameCharacter = character });
    }

    private void ButtonBase_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GalgameCharacter character)
        {
            _currentContextCharacter = character;
            CharacterFlyout.ShowAt(button, e.GetPosition(button));
        }
    }

    private void AddCharacter_Click(object sender, RoutedEventArgs e) =>
        AddCharacterCommand?.Execute(_currentContextCharacter);

    private void DeleteCharacter_Click(object sender, RoutedEventArgs e) =>
        DeleteCharacterCommand?.Execute(_currentContextCharacter);
}