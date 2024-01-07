using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public class GetStartedViewModel(MainViewModel parent) : TabViewModel(parent)
{
    public override TabType TabType => TabType.GetStarted;
}
