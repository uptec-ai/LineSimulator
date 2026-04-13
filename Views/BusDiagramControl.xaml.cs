using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TestMcAlgorithm.ViewModels;

namespace TestMcAlgorithm.Views
{
    /// <summary>
    /// BusDiagramControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BusDiagramControl : UserControl
    {
        public BusDiagramControl()
        {
            InitializeComponent();
        }

        private async void FeederCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BusDiagram viewModel || sender is not FrameworkElement { Tag: string feederLabel }) // 데이터 컨텍스트가 MainViewModel이 아니거나, 이벤트를 발생시킨 요소의 Tag가 문자열이 아닌 경우
            {
                return;
            }

            await viewModel.HandleKBusClickAsync(feederLabel);
            e.Handled = true; // 이벤트 처리 완료 표시
        }

        private async void OutputBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BusDiagram viewModel || sender is not FrameworkElement { Tag: string outputTitle })
            {
                return;
            }

            await viewModel.HandleOutputClickAsync(outputTitle);
            e.Handled = true;
        }

        private async void MarkerStackPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BusDiagram viewModel || sender is not FrameworkElement { Tag: string deviceKey } || string.IsNullOrWhiteSpace(deviceKey))
            {
                return;
            }

            await viewModel.HandleMarkerClickAsync(deviceKey);
            e.Handled = true;
        }
    }
}
