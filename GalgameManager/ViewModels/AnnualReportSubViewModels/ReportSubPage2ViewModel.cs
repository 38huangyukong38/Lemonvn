using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.ViewModels;
using KnowledgePicker.WordCloud;
using KnowledgePicker.WordCloud.Coloring;
using KnowledgePicker.WordCloud.Drawing;
using KnowledgePicker.WordCloud.Layouts;
using KnowledgePicker.WordCloud.Primitives;
using KnowledgePicker.WordCloud.Sizers;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace GalgameManager.ViewModels;
public partial class ReportSubPage2ViewModel : ObservableObject, INavigationAware
{
    private Dictionary<string, int> wordFrequencies = new();
    private DispatcherTimer tipCloseTimer = null!;
    private AnnualReportData _annualReportData = null!;

    [ObservableProperty]
    private bool isWordTipOpen;

    [ObservableProperty]
    private FrameworkElement? wordTipTarget;

    [ObservableProperty]
    private TextBlock wordTipContent = new();

    [ObservableProperty]
    private Canvas? wordCloudCanvas;

    public ReportSubPage2ViewModel()
    {
        InitializeTimer();
    }

    public void OnNavigatedTo(object parameter)
    {
        Debug.Assert(parameter is AnnualReportData);
        _annualReportData = (AnnualReportData)parameter;
        wordFrequencies = _annualReportData.TagFrequencies;
        GenerateWordCloud();
    }

    public void OnNavigatedFrom()
    {

    }

        private void InitializeTimer()
        {
            tipCloseTimer = new DispatcherTimer();
            tipCloseTimer.Interval = TimeSpan.FromMilliseconds(200);
            tipCloseTimer.Tick += (s, e) =>
            {
                IsWordTipOpen = false;
                tipCloseTimer.Stop();
            };
        }

        private void GenerateWordCloud()
        {
            if (WordCloudCanvas == null) return;
            
            WordCloudCanvas.Children.Clear();

            var wordCloud = new WordCloudInput(
                wordFrequencies.Select(p => new WordCloudEntry(p.Key, p.Value)))
            {
                Width = 1200,
                Height = 600,
                MinFontSize = 16,
                MaxFontSize = 64
            };

            var sizer = new LogSizer(wordCloud);
            var typeface = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Normal);
            using var engine = new SkGraphicEngine(sizer, wordCloud, typeface);
            var layout = new SpiralLayout(wordCloud);
            var colorizer = new RandomColorizer();
            var wcg = new WordCloudGenerator<SKBitmap>(wordCloud, engine, layout, colorizer);

            foreach (var (item, fontSize) in wcg.Arrange())
            {
                var textBlock = new TextBlock
                {
                    Text = item.Entry.Word,
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(GetWinUIColor(colorizer.GetColor(item))),
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Microsoft YaHei")
                };

                Canvas.SetLeft(textBlock, item.Location.X);
                Canvas.SetTop(textBlock, item.Location.Y);

                textBlock.PointerEntered += (s, args) =>
                {
                    tipCloseTimer.Stop();
                    WordTipContent.Text = $"关键词：{item.Entry.Word}\n相关游戏数量：{item.Entry.Count}";
                    WordTipTarget = textBlock;
                    IsWordTipOpen = true;
                };

                textBlock.PointerExited += (s, args) =>
                {
                    tipCloseTimer.Start();
                };

                WordCloudCanvas.Children.Add(textBlock);
            }
        }

        private Windows.UI.Color GetWinUIColor(System.Drawing.Color? color) =>
            color.HasValue ? Windows.UI.Color.FromArgb(color.Value.A, color.Value.R, color.Value.G, color.Value.B) 
                          : Colors.Black;
}
