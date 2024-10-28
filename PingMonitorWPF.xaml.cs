using System.Net.NetworkInformation;
using System.Windows;
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
        int timeoutsCount = 0;
        const string Address = "8.8.8.8";
        const int max = 30;
        long[] values = new long[max];
        int index = 0;
        List<long> data = [];
        List<long> dataX = [];
        List<double> dataY = [];
        long[] valuesX = new long[max];
        double[] valuesY = new double[max];
        Scatter scatter;

        public MainWindow()
        {
            InitializeComponent();

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
            scatter.ConnectStyle = ScottPlot.ConnectStyle.StepVertical;
            scatter.LineWidth = 2;
            scatter.FillY= true;
            scatter.FillYColor = scatter.Color.WithAlpha(0.3);
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
            dataX.TakeLast(max).ToArray().CopyTo(valuesX, 0);
            dataY.TakeLast(max).ToArray().CopyTo(valuesY, 0);
            scatter.MinRenderIndex = 0;
            scatter.MaxRenderIndex = Math.Min(index, max);
            ScatterPlot.Plot.Axes.AutoScale();
            
            ScatterPlot.Refresh();
            ++index;
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
    }
}