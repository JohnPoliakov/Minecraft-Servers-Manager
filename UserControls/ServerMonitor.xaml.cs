using Minecraft_Server_Manager.ViewModels;
using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class ServerMonitor : System.Windows.Controls.UserControl
    {

        private ServerMonitorViewModel _currentVm;

        private ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private DispatcherTimer _logUpdateTimer;

        public ServerMonitor()
        {
            InitializeComponent();

            this.DataContextChanged += OnDataContextChanged;

            _logUpdateTimer = new DispatcherTimer();
            _logUpdateTimer.Interval = TimeSpan.FromMilliseconds(100);
            _logUpdateTimer.Tick += ProcessLogQueue;
            _logUpdateTimer.Start();
        }

        private void EnqueueLog(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _logQueue.Enqueue(text);
        }

        private void ProcessLogQueue(object sender, EventArgs e)
        {
            if (_logQueue.IsEmpty) return;

            bool shouldScroll = (ConsoleOutput.VerticalOffset + ConsoleOutput.ViewportHeight) >= (ConsoleOutput.ExtentHeight - 10);
            int batchSize = 0;

            // On traite par paquets (max 50 lignes à la fois pour rester fluide)
            while (_logQueue.TryDequeue(out string text) && batchSize < 50)
            {
                AppendColoredLogInternal(text); // Votre ancienne méthode AppendColoredLog
                batchSize++;
            }

            if (shouldScroll)
            {
                ConsoleOutput.ScrollToEnd();
            }
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_currentVm != null)
            {
                _currentVm.LogEntryReceived -= EnqueueLog;
            }

            if (e.NewValue is ServerMonitorViewModel vm)
            {
                _currentVm = vm;
                LoadInitialLogs(vm.ServerLogs);

                vm.LogEntryReceived += EnqueueLog;
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
                    AppendColoredLogInternal(line);
            }
            ConsoleOutput.ScrollToEnd();
        }
        private void AppendColoredLogInternal(string text)
        {
            text = text.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(text)) return;

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
