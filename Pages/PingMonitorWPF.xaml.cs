using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ScottPlot.Plottables;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

namespace PingMonitorWPF
{
    public partial class MainWindow : Window
    {
        readonly DispatcherTimer timer = new();
        readonly Ping ping = new();
        int _delayTime = 500;
        const string Address = "8.8.8.8";
        const int maxTimeframeWidth = 600;
        int chartTimeWindow = 10;
        int index = 0;
        readonly List<DateTime> dataX = [];
        readonly List<double> dataY = [];
        readonly DateTime[] valuesX = new DateTime[maxTimeframeWidth];
        readonly double[] valuesY = new double[maxTimeframeWidth];
        Scatter scatter = null!;
        HorizontalLine hrMax = null!;
        HorizontalLine hrMin = null!;
        HorizontalLine hrAverage = null!;
        HorizontalLine hrMedian = null!;
        List<VerticalLine> vrTimeouts = [];
        long medianFactor = 10;
        readonly List<Marker> markers = [];

        public MainWindow()
        {
            InitializeComponent();

            InitScatterPlot();

            timer.Interval = TimeSpan.FromMilliseconds(_delayTime);
            timer.Tick += Timer_Tick;

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void InitScatterPlot()
        {
            scatter = ScatterPlot.Plot.Add.Scatter(valuesX, valuesY);
            scatter.ConnectStyle = ScottPlot.ConnectStyle.StepHorizontal;
            scatter.LineWidth = 2;
            scatter.MarkerShape = ScottPlot.MarkerShape.None;
            scatter.FillY = true;
            scatter.FillYColor = scatter.Color.WithAlpha(0.2);
            SetupLogScale();
        }

        private void SetupLogScale()
        {
            ScottPlot.TickGenerators.LogMinorTickGenerator minorTickGenerator = new() { Divisions = 10 };
            ScottPlot.TickGenerators.NumericAutomatic tickGen = new()
            {
                MinorTickGenerator = minorTickGenerator,
                IntegerTicksOnly = true,
                LabelFormatter = (double y) => $"{Math.Pow(10, y):N0}"
            };
            ScatterPlot.Plot.Axes.Left.TickGenerator = tickGen;
            ScatterPlot.Plot.Axes.DateTimeTicksBottom();
            ScatterPlot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(0.15);
            ScatterPlot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(0.05);
            ScatterPlot.Plot.Grid.MinorLineWidth = 1;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            PingReply reply;

            try
            {
                reply = ping.Send(Address);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Got exception: " + ex.Message);
                Thread.Sleep(1000);
                return;
            }

            SolidColorBrush color = GetBrushByRoundtripTime(reply.RoundtripTime);
            LabelInfo.Foreground = color;
            ProcessScatterPlot(reply);

            LabelInfo.Content = $"[{DateTime.Now.ToLongTimeString()}] {reply.RoundtripTime,5}";

        }

        private static SolidColorBrush GetBrushByRoundtripTime(long RoundtripTime)
        {
            return RoundtripTime switch
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
        private static ScottPlot.Color GetColorByRoundtripTime(long RoundtripTime)
        {
            return
                ScottPlot.Color.FromColor(
                RoundtripTime switch
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

            dataY.Add(reply.RoundtripTime == 0 ? double.NaN : Math.Log10(reply.RoundtripTime));
            dataX.Add(DateTime.Now);
            //dataX.Add(index);
            dataX.TakeLast(chartTimeWindow).ToArray().CopyTo(valuesX, 0);
            dataY.TakeLast(chartTimeWindow).ToArray().CopyTo(valuesY, 0);

            //markers.Add(ScatterPlot.Plot.Add.Marker(
            //    index, dataY.Last(),
            //    color: GetColorByRoundtripTime(reply.RoundtripTime),
            //    shape: ScottPlot.MarkerShape.FilledCircle,
            //    size: 5
            //    ));

            scatter.MinRenderIndex = 0;
            scatter.MaxRenderIndex = Math.Min(index, chartTimeWindow - 2);
            ScatterPlot.Plot.Axes.AutoScale();

            ScatterPlot.Refresh();
            UpdateLimitsLines();
            ScatterPlot.Plot.Axes.SetLimitsY(
                Math.Min(hrMin.Position * .95, 0.95),
                Math.Max(hrMax.Position * 1.05, 2.05));

            ++index;

            if (index % medianFactor == 0)
            {
                UpdateMedianLimitLine();
            }

            if (index > medianFactor * 10)
            {
                medianFactor *= 10;
            }

            RemoveOutdatedTimeoutLines(index);
        }

        private void RemoveOutdatedTimeoutLines(int index)
        {
            vrTimeouts.AsEnumerable().Where(vl => vl.Position < index - chartTimeWindow).ToList()
                .ForEach(ScatterPlot.Plot.Remove);
            vrTimeouts = vrTimeouts.AsEnumerable().Where(vl => vl.Position >= index - chartTimeWindow).ToList();

            markers.AsEnumerable().Where(m => m.Position.X < index - chartTimeWindow).ToList().ForEach(ScatterPlot.Plot.Remove);
        }

        private void AddTimeoutLine(PingReply reply)
        {
            if (reply.RoundtripTime == 0)
            {
                AddTimeoutLine(index);
            }
        }

        private void UpdateMedianLimitLine()
        {
            ScatterPlot.Plot.Remove(hrMedian);

            var sorted = dataY.Where(x => !Double.IsNaN(x)).OrderBy(x => x).ToList();
            int count = sorted.Count;
            int mid = count / 2;
            double median;

            median = (count % 2 == 0) ? median = (sorted[mid - 1] + sorted[mid]) / 2.0 : median = sorted[mid];

            hrMedian = ScatterPlot.Plot.Add.HorizontalLine(median);
            hrMedian.Color = ScottPlot.Colors.Blue;
            hrMedian.LineWidth = 1;
            hrMedian.LinePattern = ScottPlot.LinePattern.Dotted;
        }

        private void UpdateLimitsLines()
        {
            ScatterPlot.Plot.Remove(hrMax);
            ScatterPlot.Plot.Remove(hrMin);
            ScatterPlot.Plot.Remove(hrAverage);

            hrMax = ScatterPlot.Plot.Add.HorizontalLine(dataY.Max());
            hrMax.Color = ScottPlot.Colors.Red;
            hrMax.LineWidth = 1;
            hrMax.LinePattern = ScottPlot.LinePattern.Dotted;

            hrMin = ScatterPlot.Plot.Add.HorizontalLine(dataY.Where(x => !Double.IsNaN(x)).Min());
            hrMin.Color = ScottPlot.Colors.Green;
            hrMin.LineWidth = 1;
            hrMin.LinePattern = ScottPlot.LinePattern.Dotted;

            hrAverage = ScatterPlot.Plot.Add.HorizontalLine(dataY.Where(x => !Double.IsNaN(x)).Average());
            hrAverage.Color = ScottPlot.Colors.DarkOrange;
            hrAverage.LineWidth = 1;
            hrAverage.LinePattern = ScottPlot.LinePattern.Dotted;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (timer.IsEnabled)
            {
                timer.Stop();
            }
            else
            {
                timer.Start();
            }
        }

        private void CBFrameWidth_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selectedItem = (TextBlock)CBFrameWidth.SelectedItem;
            string? value = selectedItem.Text.ToString();

            if (!string.IsNullOrEmpty(value))
            {
                var newValue = Convert.ToInt32(value);
                if (newValue > chartTimeWindow)
                {
                    AddMissingTimeoutLines(index - newValue);
                }

                chartTimeWindow = newValue;
            }
        }

        private void AddMissingTimeoutLines(int minIndex)
        {
            if (dataY.Count == 0)
                return;

            for (int i = Math.Max(0, minIndex); i < index; i++)
            {
                if (double.IsNaN(dataY[i]) && !vrTimeouts.Any(vr => vr.Position == i + 1))
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

            vrTimeouts.Add(vrTimeout);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.TimeframeWidhtIndex = CBFrameWidth.SelectedIndex;
            Properties.Settings.Default.DelayTime = CBDelayTime.SelectedIndex;

            Properties.Settings.Default.WinTop = this.Top;
            Properties.Settings.Default.WinLeft = this.Left;
            Properties.Settings.Default.WinWidth = this.Width;
            Properties.Settings.Default.WinHeight = this.Height;
            Properties.Settings.Default.WinState = this.WindowState.ToString();
            Properties.Settings.Default.Save();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CBFrameWidth.SelectedIndex = Properties.Settings.Default.TimeframeWidhtIndex;
            CBDelayTime.SelectedIndex = Properties.Settings.Default.DelayTime;

            if (Properties.Settings.Default.WinWidth != 0)
            {
                this.Top = Properties.Settings.Default.WinTop;
                this.Left = Properties.Settings.Default.WinLeft;
                this.Width = Properties.Settings.Default.WinWidth;
                this.Height = Properties.Settings.Default.WinHeight;

                if (Enum.TryParse(Properties.Settings.Default.WinState, out WindowState state))
                {
                    this.WindowState = state;
                }
            }
        }

        private void ButtonAct_Click(object sender, RoutedEventArgs e)
        {
            dataY.Add(double.NaN);
            dataX.Add(DateTime.Now);
            //dataX.Add(index++);

            var vrTimeout = ScatterPlot.Plot.Add.VerticalLine(index);
            vrTimeout.Color = ScottPlot.Colors.Red;
            vrTimeout.LineWidth = 3;
            vrTimeout.LinePattern = ScottPlot.LinePattern.Dashed;

            vrTimeouts.Add(vrTimeout);
        }

        private void CBDelayTime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = (TextBlock)CBDelayTime.SelectedItem;
            string? value = selectedItem.Text.ToString();

            if (!string.IsNullOrEmpty(value))
            {
                _delayTime = Convert.ToInt32(value);

                timer.Interval = TimeSpan.FromMilliseconds(_delayTime);
            }
        }
    }
}