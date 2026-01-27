using Minecraft_Server_Manager.ViewModels;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class ServerMonitor : System.Windows.Controls.UserControl
    {
        #region Fields
        private ServerMonitorViewModel _currentVm;

        // Système de file d'attente pour éviter de surcharger le Thread UI
        private ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private DispatcherTimer _logUpdateTimer;
        #endregion

        #region Constructor
        public ServerMonitor()
        {
            InitializeComponent();

            this.DataContextChanged += OnDataContextChanged;
            this.Loaded += OnViewLoaded;

            _logUpdateTimer = new DispatcherTimer();
            _logUpdateTimer.Interval = TimeSpan.FromMilliseconds(100);
            _logUpdateTimer.Tick += ProcessLogQueue;
            _logUpdateTimer.Start();
        }
        #endregion

        #region Lifecycle & DataContext Management
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_currentVm != null)
            {
                _currentVm.LogEntryReceived -= EnqueueLog;
                _currentVm.ClearLogsRequested -= ClearConsole;
            }

            if (e.NewValue is ServerMonitorViewModel vm)
            {
                _currentVm = vm;

                LoadInitialLogs(vm.ServerLogs);

                vm.LogEntryReceived += EnqueueLog;
                vm.ClearLogsRequested += ClearConsole;
            }
        }
        #endregion

        #region Log Processing Engine (Queue & Timer)
        private void EnqueueLog(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _logQueue.Enqueue(text);
        }

        private void ProcessLogQueue(object sender, EventArgs e)
        {
            if (_logQueue.IsEmpty) return;

            bool userIsAtBottom = (ConsoleOutput.VerticalOffset + ConsoleOutput.ViewportHeight) >= (ConsoleOutput.ExtentHeight - 50);
            int batchSize = 0;

            while (_logQueue.TryDequeue(out string text) && batchSize < 50)
            {
                AppendColoredLogInternal(text);
                batchSize++;
            }

            if (userIsAtBottom)
            {
                ConsoleOutput.ScrollToEnd();
            }
        }

        private void ClearConsole()
        {
            while (_logQueue.TryDequeue(out _)) { }

            ConsoleOutput.Document.Blocks.Clear();
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
        #endregion

        #region UI Formatting & Coloring
        /// <summary>
        /// Crée un paragraphe coloré et l'ajoute au RichTextBox.
        /// </summary>
        private void AppendColoredLogInternal(string text)
        {
            text = text.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(text)) return;

            Paragraph paragraph = new Paragraph();

            if (text.Contains("ERROR") || text.Contains("Exception") || text.Contains("Error"))
            {
                // Rouge
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
                paragraph.FontWeight = FontWeights.Bold;
            }
            else if (text.Contains("WARN") || text.Contains("Warning"))
            {
                // Jaune
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 196, 15));
            }
            else if (text.Contains("INFO"))
            {
                // Blanc cassé
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 240, 241));
            }
            else if (text.Contains("joined the game") || text.Contains("left the game"))
            {
                // Vert
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
            }
            else
            {
                // Gris
                paragraph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(189, 195, 199));
            }

            // --- Ajout au document ---
            paragraph.Inlines.Add(new Run(text));
            ConsoleOutput.Document.Blocks.Add(paragraph);

            if (ConsoleOutput.Document.Blocks.Count > 1000)
            {
                ConsoleOutput.Document.Blocks.Remove(ConsoleOutput.Document.Blocks.FirstBlock);
            }
        }
        #endregion

        #region UI Event Handlers
        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ConsoleOutput.ScrollToEnd();
            }, DispatcherPriority.Background);
        }
        private void CommandInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                tb.CaretIndex = tb.Text.Length;
            }
        }

        #endregion
    }
}