using ScottPlot;
using System;
using System.IO.Ports;
using System.Linq;
using System.Timers;
using System.Windows;

namespace RespawnStation
{
    public partial class MainWindow : Window
    {
        private Timer _timer;
        private double[] times = new double[1000];
        private double[] pv = new double[1000];
        private int idx = 0;
        private SerialPort _serial;

        public MainWindow()
        {
            InitializeComponent();

            // preparar gráfico
            wpfPlot.Plot.Title("Perfil térmico (Simulado)");
            wpfPlot.Plot.YLabel("Temperatura (°C)");
            wpfPlot.Plot.XLabel("Tempo (s)");
            wpfPlot.Plot.AddSignal(pv);
            wpfPlot.Refresh();

            // timer que simula PV
            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;

            // listar portas
            cbPorts.ItemsSource = SerialPort.GetPortNames().OrderBy(s => s);
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // simulação simples de rampa
            Dispatcher.Invoke(() =>
            {
                if (idx >= pv.Length) idx = 0;
                double val = 50 + 150 * Math.Sin(idx * 0.05);
                pv[idx] = val;
                times[idx] = idx;
                idx++;
                wpfPlot.Plot.Clear();
                wpfPlot.Plot.AddScatter(times, pv);
                wpfPlot.Refresh();

                lblPV.Text = $"PV: {val:F1} °C";
                lblSP.Text = $"SP: 180.0 °C";
            });
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _timer.Start();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_serial == null || !_serial.IsOpen)
            {
                if (cbPorts.SelectedItem == null) { MessageBox.Show("Selecione COM"); return; }
                string port = cbPorts.SelectedItem.ToString();
                try
                {
                    _serial = new SerialPort(port, 9600, Parity.None, 8, StopBits.One);
                    _serial.Open();
                    lblComm.Text = "Status: Online";
                    lblComm.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro abrir porta: " + ex.Message);
                }
            }
            else
            {
                _serial.Close();
                lblComm.Text = "Status: Offline";
                lblComm.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Editor de perfil - em desenvolvimento");
        }
    }
}

