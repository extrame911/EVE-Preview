using EvePreview.App.Services;
using EvePreview.App.ViewModels;

namespace EvePreview.App.Tests;

[Collection("WPF Application")]
public sealed class LocalizationManagerTests(WpfApplicationFixture wpf)
{
    [Fact]
    public void EnglishTranslatesKnownUiTextAndLeavesUserTextUntouched()
    {
        wpf.Invoke(() =>
        {
            try
            {
                LocalizationManager.SetLanguage("en-US");

                Assert.Equal("Overview", LocalizationManager.Translate("总览"));
                Assert.Equal("Preview running", LocalizationManager.Translate("预览运行中"));
                Assert.Equal("主账号", LocalizationManager.Translate("主账号"));
            }
            finally
            {
                LocalizationManager.SetLanguage("zh-CN");
            }
        });
    }

    [Fact]
    public void SwitchingBackToChineseRestoresTheOriginalLabel()
    {
        wpf.Invoke(() =>
        {
            try
            {
                LocalizationManager.SetLanguage("en-US");
                Assert.Equal("Clients", LocalizationManager.Translate("客户端"));
                LocalizationManager.SetLanguage("zh-CN");
                Assert.Equal("客户端", LocalizationManager.Translate("客户端"));
            }
            finally
            {
                LocalizationManager.SetLanguage("zh-CN");
            }
        });
    }

    [Theory]
    [InlineData("保存窗口布局", "Save Window Layout")]
    [InlineData("应用窗口布局", "Apply Window Layout")]
    [InlineData("保存所有窗口位置", "Save All Window Positions")]
    [InlineData("最小化全部", "Minimize All")]
    [InlineData("新建循环组", "New Cycle List")]
    [InlineData("打开目录", "Open Folder")]
    public void Design_action_labels_have_exact_english_translations(string source, string expected)
    {
        wpf.Invoke(() =>
        {
            var originalLanguage = LocalizationManager.CurrentLanguage;
            try
            {
                LocalizationManager.SetLanguage("en-US");

                Assert.Equal(expected, LocalizationManager.Translate(source));
            }
            finally
            {
                LocalizationManager.SetLanguage(originalLanguage);
            }
        });
    }

    [Fact]
    public void NewAndExistingCycleGroupNamesFollowTheCurrentLanguage()
    {
        wpf.Invoke(() =>
        {
            var originalLanguage = LocalizationManager.CurrentLanguage;
            try
            {
                LocalizationManager.SetLanguage("en-US");
                var viewModel = new CycleGroupsViewModel();

                viewModel.AddGroupCommand.Execute(null);

                Assert.Equal("Cycle List 06", viewModel.SelectedGroup!.DisplayText);
                Assert.Equal("Cycle List 6 settings", viewModel.SelectedGroup.TitleText);

                LocalizationManager.SetLanguage("zh-CN");
                viewModel.RefreshLocalizedText();

                Assert.Equal("循环组 06", viewModel.SelectedGroup.DisplayText);
                Assert.Equal("循环组 6 设置", viewModel.SelectedGroup.TitleText);
            }
            finally
            {
                LocalizationManager.SetLanguage(originalLanguage);
            }
        });
    }
}
