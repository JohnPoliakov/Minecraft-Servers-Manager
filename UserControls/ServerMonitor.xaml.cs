using Minecraft_Server_Manager.ViewModels;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class ServerMonitor : System.Windows.Controls.UserControl
    {
        #region Fields
        private ServerMonitorViewModel? _currentVm;

        // Système de file d'attente pour éviter de surcharger le Thread UI
        private ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private int _droppedLines;
        private DispatcherTimer _logUpdateTimer;

        // Brushes statiques réutilisables (Frozen = thread-safe + pas de leak)
        private static readonly SolidColorBrush BrushError;
        private static readonly SolidColorBrush BrushWarning;
        private static readonly SolidColorBrush BrushInfo;
        private static readonly SolidColorBrush BrushPlayer;
        private static readonly SolidColorBrush BrushDefault;
        #endregion

        #region Static Constructor
        static ServerMonitor()
        {
            BrushError = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            BrushError.Freeze();

            BrushWarning = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 196, 15));
            BrushWarning.Freeze();

            BrushInfo = new SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 240, 241));
            BrushInfo.Freeze();

            BrushPlayer = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
            BrushPlayer.Freeze();

            BrushDefault = new SolidColorBrush(System.Windows.Media.Color.FromRgb(189, 195, 199));
            BrushDefault.Freeze();
        }
        #endregion

        #region Constructor
        public ServerMonitor()
        {
            InitializeComponent();

            this.DataContextChanged += OnDataContextChanged;
            this.Loaded += OnViewLoaded;
            this.Unloaded += OnViewUnloaded;

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

                // Redémarrer le timer si nécessaire
                if (!_logUpdateTimer.IsEnabled)
                    _logUpdateTimer.Start();
            }
        }

        private void OnViewUnloaded(object sender, RoutedEventArgs e)
        {
            // Arrêter le timer quand le contrôle n'est plus visible
            _logUpdateTimer.Stop();

            if (_currentVm != null)
            {
                _currentVm.LogEntryReceived -= EnqueueLog;
                _currentVm.ClearLogsRequested -= ClearConsole;
            }
        }
        #endregion

        #region Log Processing Engine (Queue & Timer)
        private void EnqueueLog(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _logQueue.Enqueue(text);
            while (_logQueue.Count > 5000 && _logQueue.TryDequeue(out _)) System.Threading.Interlocked.Increment(ref _droppedLines);
        }

        private void ProcessLogQueue(object? sender, EventArgs e)
        {
            if (_logQueue.IsEmpty) return;

            bool userIsAtBottom = (ConsoleOutput.VerticalOffset + ConsoleOutput.ViewportHeight) >= (ConsoleOutput.ExtentHeight - 50);
            int batchSize = 0;

            int dropped = System.Threading.Interlocked.Exchange(ref _droppedLines, 0);
            if (dropped > 0) AppendColoredLogInternal($"[MSM] {dropped} lignes non affichées (console saturée).");
            while (batchSize < 100 && _logQueue.TryDequeue(out string? text))
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
            while (_logQueue.TryDequeue(out _)) { }
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
        /// Utilise des Brushes statiques Frozen pour éviter les allocations.
        /// </summary>
        private void AppendColoredLogInternal(string text)
        {
            text = text.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(text)) return;

            Paragraph paragraph = new Paragraph();

            if (text.Contains("ERROR") || text.Contains("Exception") || text.Contains("Error"))
            {
                paragraph.Foreground = BrushError;
                paragraph.FontWeight = FontWeights.Bold;
            }
            else if (text.Contains("WARN") || text.Contains("Warning"))
            {
                paragraph.Foreground = BrushWarning;
            }
            else if (text.Contains("INFO"))
            {
                paragraph.Foreground = BrushInfo;
            }
            else if (text.Contains("joined the game") || text.Contains("left the game"))
            {
                paragraph.Foreground = BrushPlayer;
            }
            else
            {
                paragraph.Foreground = BrushDefault;
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
            if (_currentVm != null)
            {
                _currentVm.LogEntryReceived -= EnqueueLog;
                _currentVm.ClearLogsRequested -= ClearConsole;
                _currentVm.LogEntryReceived += EnqueueLog;
                _currentVm.ClearLogsRequested += ClearConsole;
            }
            _logUpdateTimer.Start();
            Dispatcher.InvokeAsync(() =>
            {
                ConsoleOutput.ScrollToEnd();
            }, DispatcherPriority.Background);
        }
        private void CommandInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key is Key.Up or Key.Down && Keyboard.Modifiers == ModifierKeys.None &&
                sender is System.Windows.Controls.TextBox tb && tb.DataContext is ServerMonitorViewModel vm)
            {
                vm.NavigateHistoryCommand.Execute(e.Key == Key.Up ? "Up" : "Down");
                tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
                tb.CaretIndex = tb.Text.Length;
                e.Handled = true;
            }
        }

        #endregion
    }
}
