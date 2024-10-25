using System.Net.NetworkInformation;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using PingConMonitor.DataStructures;

namespace PingMonitorWPF
{    public partial class MainWindow : Window
    {
        readonly DispatcherTimer timer = new();
        readonly Ping ping = new();
        readonly List<PingPoint> points = [];
        readonly LastTimes lastTimes = new();
        readonly int _sleepDuration = 500;
        int timeoutsCount = 0;
        const string Address = "8.8.8.8";
        const int max = 100;
        long[] values = new long[max];
        List<long> data = [];
        int index = 0;

        public MainWindow()
        {
            InitializeComponent();

            double[] dataX = [0];
            double[] dataY = [0];
            var z = WpfPlot1.Plot.Add.Scatter(dataX, dataY);
            //z.ConnectStyle = ScottPlot.ConnectStyle.Straight
            //WpfPlot1.Refresh();
            var sig = WpfPlot1.Plot.Add.Signal(values);
            sig.LinePattern = ScottPlot.LinePattern.Solid;
            sig.LineWidth = 2;
            WpfPlot1.Plot.Axes.AutoScale();

            timer.Interval = TimeSpan.FromMilliseconds(_sleepDuration);
            timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            PingReply reply = ping.Send(Address);
            points.Add(new PingPoint(reply.RoundtripTime));
            lastTimes.Update(reply.RoundtripTime);

            data.Add(reply.RoundtripTime);
            data.TakeLast(max).ToArray().CopyTo(values, 0);
            WpfPlot1.Plot.Axes.AutoScale();
            WpfPlot1.Refresh();

            LabelInfo.Content = DateTime.Now.ToLongTimeString() + "\t" + reply.RoundtripTime;
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