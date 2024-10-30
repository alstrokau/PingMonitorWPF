using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PingConMonitor.DataStructures;
using ScottPlot.Plottables;

namespace PingMonitorWPF
{
    public partial class MainWindow : Window
    {
        readonly DispatcherTimer timer = new();
        readonly Ping ping = new();
        readonly List<PingPoint> points = [];
        readonly LastTimes lastTimes = new();
        readonly int _sleepDuration = 500;
        const string Address = "8.8.8.8";
        const int max = 600;
        int chartTimeWindow = 10;
        readonly long[] values = new long[max];
        int index = 0;
        readonly List<long> data = [];
        readonly List<long> dataX = [];
        readonly List<double> dataY = [];
        readonly long[] valuesX = new long[max];
        readonly double[] valuesY = new double[max];
        Scatter scatter = null!;
        HorizontalLine hrMax = null!;
        HorizontalLine hrMin = null!;
        HorizontalLine hrAverage = null!;

        public MainWindow()
        {
            InitializeComponent();

            CBFrameWidth.SelectedIndex = 1;

            InitScatterPlot();
            InitSignalPlot();

            timer.Interval = TimeSpan.FromMilliseconds(_sleepDuration);
            timer.Tick += Timer_Tick;
        }

        private void InitSignalPlot()
        {
            var sig = SignalPlot.Plot.Add.Signal(values);
            sig.LinePattern = ScottPlot.LinePattern.Solid;
            sig.LineWidth = 2;
            SignalPlot.Plot.Axes.AutoScale();
            SignalPlot.Visibility = Visibility.Collapsed;
        }

        private void InitScatterPlot()
        {
            scatter = ScatterPlot.Plot.Add.Scatter(valuesX, valuesY);
            scatter.ConnectStyle = ScottPlot.ConnectStyle.StepHorizontal;
            scatter.LineWidth = 2;
            scatter.FillY = true;
            scatter.FillYColor = scatter.Color.WithAlpha(0.2);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            PingReply reply = ping.Send(Address);
            points.Add(new PingPoint(reply.RoundtripTime));
            lastTimes.Update(reply.RoundtripTime);

            ProcessSignalPlot(reply);
            ProcessScatterPlot(reply);

            LabelInfo.Content = $"[{DateTime.Now.ToLongTimeString()}] {reply.RoundtripTime,5}";
            LabelInfo.Foreground = reply.RoundtripTime switch
            {
                0 => Brushes.Black,
                <= 100 => Brushes.Green,
                <= 250 => Brushes.DarkGreen,
                <= 500 => Brushes.DarkOrange,
                <= 1500 => Brushes.Red,
                _ => Brushes.DarkRed
            };
        }

        private void ProcessSignalPlot(PingReply reply)
        {
            data.Add(reply.RoundtripTime);
            data.TakeLast(max).ToArray().CopyTo(values, 0);
            SignalPlot.Plot.Axes.AutoScale();
            SignalPlot.Refresh();
        }

        private void ProcessScatterPlot(PingReply reply)
        {
            dataY.Add(reply.RoundtripTime == 0 ? double.NaN : (double)reply.RoundtripTime);
            dataX.Add(index);
            dataX.TakeLast(chartTimeWindow).ToArray().CopyTo(valuesX, 0);
            dataY.TakeLast(chartTimeWindow).ToArray().CopyTo(valuesY, 0);
            scatter.MinRenderIndex = 0;
            scatter.MaxRenderIndex = Math.Min(index, chartTimeWindow - 1);
            ScatterPlot.Plot.Axes.AutoScale();

            ScatterPlot.Plot.Remove(hrMax);
            ScatterPlot.Plot.Remove(hrMin);
            ScatterPlot.Plot.Remove(hrAverage);

            ScatterPlot.Refresh();
            UpdateLimitsLines();
            ++index;
        }

        private void UpdateLimitsLines()
        {
            hrMax = ScatterPlot.Plot.Add.HorizontalLine(dataY.Max());
            hrMax.Color = ScottPlot.Colors.Red;
            hrMax.LineWidth = 1;
            hrMax.LinePattern = ScottPlot.LinePattern.Dotted;

            hrMin = ScatterPlot.Plot.Add.HorizontalLine(dataY.Where(x => !Double.IsNaN(x)).Min());
            hrMin.Color = ScottPlot.Colors.Green;
            hrMin.LineWidth = 1;
            hrMin.LinePattern = ScottPlot.LinePattern.Dotted;

            hrAverage = ScatterPlot.Plot.Add.HorizontalLine(dataY.Where(x=>!Double.IsNaN(x)).Average());
            hrAverage.Color = ScottPlot.Colors.Blue;
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
                chartTimeWindow = Convert.ToInt32(value);
        }
    }
}