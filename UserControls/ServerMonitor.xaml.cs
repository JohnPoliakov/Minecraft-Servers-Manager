using Minecraft_Server_Manager.ViewModels;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class ServerMonitor : System.Windows.Controls.UserControl
    {

        private ServerMonitorViewModel _currentVm;

        public ServerMonitor()
        {
            InitializeComponent();

            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_currentVm != null)
            {
                _currentVm.LogEntryReceived -= AppendColoredLog;
            }

            if (e.NewValue is ServerMonitorViewModel vm)
            {
                _currentVm = vm;
                LoadInitialLogs(vm.ServerLogs);
                vm.LogEntryReceived += AppendColoredLog;
            }
        }

        private void LoadInitialLogs(string fullLogs)
        {
            ConsoleOutput.Document.Blocks.Clear();
            if (string.IsNullOrEmpty(fullLogs)) return;

            string[] lines = fullLogs.Split('\n');
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    AppendColoredLog(line);
            }
            ConsoleOutput.ScrollToEnd();
        }
        private void AppendColoredLog(string text)
        {
            text = text.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(text)) return;

            bool isAtBottom = (ConsoleOutput.VerticalOffset + ConsoleOutput.ViewportHeight) >= (ConsoleOutput.ExtentHeight - 5);

            Paragraph paragraph = new Paragraph();

            if (text.Contains("ERROR") || text.Contains("Exception") || text.Contains("Error"))
            {
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
                paragraph.FontWeight = System.Windows.FontWeights.Bold;
            }
            else if (text.Contains("WARN") || text.Contains("Warning"))
            {
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 196, 15));
            }
            else if (text.Contains("INFO"))
            {
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 240, 241));
            }
            else if (text.Contains("joined the game") || text.Contains("left the game"))
            {
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
            }
            else
            {
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(189, 195, 199));
            }

            paragraph.Inlines.Add(new Run(text));
            ConsoleOutput.Document.Blocks.Add(paragraph);

            if (ConsoleOutput.Document.Blocks.Count > 1000)
            {
                ConsoleOutput.Document.Blocks.Remove(ConsoleOutput.Document.Blocks.FirstBlock);
            }

            if (isAtBottom)
            {
                ConsoleOutput.ScrollToEnd();
            }
        }

        private void CommandInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                tb.CaretIndex = tb.Text.Length;
            }
        }

        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ConsoleOutput.ScrollToEnd();
        }
    }
}
