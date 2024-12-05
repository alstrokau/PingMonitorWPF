using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ScottPlot.Plottables;

namespace PingMonitorWPF.Pages
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer = new();
        private readonly Ping _ping = new();
        private int _delayTime = 500;
        private const string Address = "8.8.8.8";
        private const int MaxTimeframeWidth = 600;
        private int _chartTimeWindow = 10;
        private int _index;
        private readonly List<long> _dataX = [];
        private readonly List<double> _dataY = [];
        private readonly long[] _valuesX = new long[MaxTimeframeWidth];
        private readonly double[] _valuesY = new double[MaxTimeframeWidth];
        private Scatter _scatter = null!;
        private HorizontalLine _hrMax = null!;
        private HorizontalLine _hrMin = null!;
        private HorizontalLine _hrAverage = null!;
        private HorizontalLine _hrMedian = null!;
        private List<VerticalLine> _vrTimeouts = [];
        private long _medianFactor = 10;
        private readonly List<Marker> _markers = [];
        private Task<PingReply> _pingTask;

        public MainWindow()
        {
            InitializeComponent();

            InitScatterPlot();
            _pingTask = _ping.SendPingAsync(Address);

            _timer.Interval = TimeSpan.FromMilliseconds(_delayTime);
            _timer.Tick += Timer_Tick;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void InitScatterPlot()
        {
            _scatter = ScatterPlot.Plot.Add.Scatter(_valuesX, _valuesY);
            _scatter.ConnectStyle = ScottPlot.ConnectStyle.StepHorizontal;
            _scatter.LineWidth = 2;
            _scatter.MarkerShape = ScottPlot.MarkerShape.None;
            _scatter.FillY = true;
            _scatter.FillYColor = _scatter.Color.WithAlpha(0.2);
            SetupLogScale();
        }

        private void SetupLogScale()
        {
            ScottPlot.TickGenerators.LogMinorTickGenerator minorTickGenerator = new() { Divisions = 10 };
            ScottPlot.TickGenerators.NumericAutomatic tickGen = new()
            {
                MinorTickGenerator = minorTickGenerator,
                IntegerTicksOnly = true,
                LabelFormatter = y => $"{Math.Pow(10, y):N0}"
            };
            ScatterPlot.Plot.Axes.Left.TickGenerator = tickGen;
            ScatterPlot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(0.15);
            ScatterPlot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(0.05);
            ScatterPlot.Plot.Grid.MinorLineWidth = 1;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            TickProcessor();
        }

        private void ProcessPingReply(PingReply reply)
        {
            var color = GetBrushByRoundtripTime(reply.RoundtripTime);
            LabelInfo.Foreground = color;
            ProcessScatterPlot(reply);

            LabelInfo.Content = $"[{DateTime.Now.ToLongTimeString()}] {reply.RoundtripTime,5}";
        }

        private void TickProcessor()
        {
            if (!_pingTask.IsCompletedSuccessfully)
            {
                if (_pingTask.Status == TaskStatus.Faulted)
                {
                    Thread.Sleep(1000);
                    _pingTask = _ping.SendPingAsync(Address);
                }
                else
                {
                    return;
                }
            }

            ProcessPingReply(_pingTask.Result);
            _pingTask = _ping.SendPingAsync(Address);
        }

        private static SolidColorBrush GetBrushByRoundtripTime(long roundtripTime)
        {
            return roundtripTime switch
            {
                0 => Brushes.Black,
                <= 100 => Brushes.Green,
                <= 250 => Brushes.DarkGreen,
                <= 500 => Brushes.DarkOrange,
                <= 1500 => Brushes.Red,
                <= 2000 => Brushes.DarkRed,
                _ => Brushes.Black
            };
        }
        private static ScottPlot.Color GetColorByRoundtripTime(long roundtripTime)
        {
            return
                ScottPlot.Color.FromColor(
                roundtripTime switch
                {
                    0 => System.Drawing.Color.Black,
                    <= 100 => System.Drawing.Color.Green,
                    <= 250 => System.Drawing.Color.DarkGreen,
                    <= 500 => System.Drawing.Color.DarkOrange,
                    <= 1500 => System.Drawing.Color.Red,
                    <= 2000 => System.Drawing.Color.DarkRed,
                    _ => System.Drawing.Color.Black
                });
        }

        private void ProcessScatterPlot(PingReply reply)
        {
            AddTimeoutLine(reply);

            _dataY.Add(reply.RoundtripTime == 0 ? double.NaN : Math.Log10(reply.RoundtripTime));
            _dataX.Add(_index);
            _dataX.TakeLast(_chartTimeWindow).ToArray().CopyTo(_valuesX, 0);
            _dataY.TakeLast(_chartTimeWindow).ToArray().CopyTo(_valuesY, 0);

            _markers.Add(ScatterPlot.Plot.Add.Marker(
                _index, _dataY.Last(),
                color: GetColorByRoundtripTime(reply.RoundtripTime),
                shape: ScottPlot.MarkerShape.FilledCircle,
                size: 5
                ));

            _scatter.MinRenderIndex = 0;
            _scatter.MaxRenderIndex = Math.Min(_index, _chartTimeWindow - 1);
            ScatterPlot.Plot.Axes.AutoScale();

            ScatterPlot.Refresh();
            UpdateLimitsLines();
            ScatterPlot.Plot.Axes.SetLimitsY(
                Math.Min(_hrMin.Position * .95, 0.95),
                Math.Max(_hrMax.Position * 1.05, 2.05));

            ++_index;

            if (_index % _medianFactor == 0)
            {
                UpdateMedianLimitLine();
            }

            if (_index > _medianFactor * 10)
            {
                _medianFactor *= 10;
            }

            RemoveOutdatedTimeoutLines(_index);
        }

        private void RemoveOutdatedTimeoutLines(int index)
        {
            _vrTimeouts.AsEnumerable().Where(vl => vl.Position < index - _chartTimeWindow).ToList()
                .ForEach(ScatterPlot.Plot.Remove);
            _vrTimeouts = _vrTimeouts.AsEnumerable().Where(vl => vl.Position >= index - _chartTimeWindow).ToList();

            _markers.AsEnumerable().Where(m => m.Position.X < index - _chartTimeWindow)
                .ToList().ForEach(ScatterPlot.Plot.Remove);
        }

        private void AddTimeoutLine(PingReply reply)
        {
            if (reply.RoundtripTime == 0)
            {
                AddTimeoutLine(_index);
            }
        }

        private void UpdateMedianLimitLine()
        {
            ScatterPlot.Plot.Remove(_hrMedian);

            var sorted = _dataY.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            int count = sorted.Count;
            int mid = count / 2;

            var median = (count % 2 == 0) ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];

            _hrMedian = ScatterPlot.Plot.Add.HorizontalLine(median);
            _hrMedian.Color = ScottPlot.Colors.Blue;
            _hrMedian.LineWidth = 1;
            _hrMedian.LinePattern = ScottPlot.LinePattern.Dotted;
        }

        private void UpdateLimitsLines()
        {
            ScatterPlot.Plot.Remove(_hrMax);
            ScatterPlot.Plot.Remove(_hrMin);
            ScatterPlot.Plot.Remove(_hrAverage);

            _hrMax = ScatterPlot.Plot.Add.HorizontalLine(_dataY.Max());
            _hrMax.Color = ScottPlot.Colors.Red;
            _hrMax.LineWidth = 1;
            _hrMax.LinePattern = ScottPlot.LinePattern.Dotted;

            _hrMin = ScatterPlot.Plot.Add.HorizontalLine(_dataY.Where(x => !double.IsNaN(x)).Min());
            _hrMin.Color = ScottPlot.Colors.Green;
            _hrMin.LineWidth = 1;
            _hrMin.LinePattern = ScottPlot.LinePattern.Dotted;

            _hrAverage = ScatterPlot.Plot.Add.HorizontalLine(_dataY.Where(x => !double.IsNaN(x)).Average());
            _hrAverage.Color = ScottPlot.Colors.DarkOrange;
            _hrAverage.LineWidth = 1;
            _hrAverage.LinePattern = ScottPlot.LinePattern.Dotted;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }
            else
            {
                _timer.Start();
            }
        }

        private void CBFrameWidth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = (TextBlock)CBFrameWidth.SelectedItem;
            string? value = selectedItem.Text;

            if (string.IsNullOrEmpty(value))
                return;
            var newValue = Convert.ToInt32(value);
            if (newValue > _chartTimeWindow)
            {
                AddMissingTimeoutLines(_index - newValue);
            }

            _chartTimeWindow = newValue;
        }

        private void AddMissingTimeoutLines(int minIndex)
        {
            if (_dataY.Count == 0)
                return;

            for (int i = Math.Max(0, minIndex); i < _index; i++)
            {
                if (double.IsNaN(_dataY[i]) && !_vrTimeouts.Any(vr => vr.Position == i + 1))
                {
                    AddTimeoutLine(i + 1);
                }
            }
        }

        private void AddTimeoutLine(int position)
        {
            var vrTimeout = ScatterPlot.Plot.Add.VerticalLine(position);
            vrTimeout.Color = ScottPlot.Colors.Red;
            vrTimeout.LineWidth = 3;
            vrTimeout.LinePattern = ScottPlot.LinePattern.Dashed;

            _vrTimeouts.Add(vrTimeout);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.TimeframeWidhtIndex = CBFrameWidth.SelectedIndex;
            Properties.Settings.Default.DelayTime = CBDelayTime.SelectedIndex;

            Properties.Settings.Default.WinTop = Top;
            Properties.Settings.Default.WinLeft = Left;
            Properties.Settings.Default.WinWidth = Width;
            Properties.Settings.Default.WinHeight = Height;
            Properties.Settings.Default.WinState = WindowState.ToString();
            Properties.Settings.Default.Save();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CBFrameWidth.SelectedIndex = Properties.Settings.Default.TimeframeWidhtIndex;
            CBDelayTime.SelectedIndex = Properties.Settings.Default.DelayTime;

            if (Properties.Settings.Default.WinWidth == 0)
                return;
            Top = Properties.Settings.Default.WinTop;
            Left = Properties.Settings.Default.WinLeft;
            Width = Properties.Settings.Default.WinWidth;
            Height = Properties.Settings.Default.WinHeight;

            if (Enum.TryParse(Properties.Settings.Default.WinState, out WindowState state))
            {
                WindowState = state;
            }
        }

        private void ButtonAct_Click(object sender, RoutedEventArgs e)
        {
            _dataY.Add(double.NaN);
            _dataX.Add(_index++);

            var vrTimeout = ScatterPlot.Plot.Add.VerticalLine(_index);
            vrTimeout.Color = ScottPlot.Colors.Red;
            vrTimeout.LineWidth = 3;
            vrTimeout.LinePattern = ScottPlot.LinePattern.Dashed;

            _vrTimeouts.Add(vrTimeout);
        }

        private void CBDelayTime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = (TextBlock)CBDelayTime.SelectedItem;
            string? value = selectedItem.Text;

            if (string.IsNullOrEmpty(value))
                return;
            _delayTime = Convert.ToInt32(value);

            _timer.Interval = TimeSpan.FromMilliseconds(_delayTime);
        }
    }
}