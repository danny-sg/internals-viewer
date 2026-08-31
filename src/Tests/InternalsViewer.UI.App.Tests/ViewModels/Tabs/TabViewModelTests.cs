using InternalsViewer.UI.App.ViewModels.Tabs;

namespace InternalsViewer.UI.App.Tests.ViewModels.Tabs;

[Trait("Category", "Unit")]
public class TabViewModelTests
{
    private sealed class TestTabViewModel : TabViewModel
    {
        public bool RunOnDispatcher(Action action) => DispatcherQueue.TryEnqueue(() => action());
    }

    [Fact]
    public void Constructs_Without_A_UI_Thread()
    {
        using var viewModel = new TestTabViewModel();

        Assert.NotEqual(string.Empty, viewModel.TabId);
    }

    [Fact]
    public void Dispatches_Inline_When_There_Is_No_Dispatcher_Queue()
    {
        using var viewModel = new TestTabViewModel();

        var ran = false;

        var accepted = viewModel.RunOnDispatcher(() => ran = true);

        Assert.True(accepted);
        Assert.True(ran);
    }
}
