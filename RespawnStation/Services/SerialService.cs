using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RespawnStation.Services
{
    public class SerialService : IDisposable
    {
        private SerialPort _port;
        private CancellationTokenSource _cts;
        private Task _readerTask;
        private readonly object _lock = new object();
        private StreamWriter _logWriter;

        public bool IsOpen => _port != null && _port.IsOpen;
        public bool Simulation { get; set; } = true; // <- start em SIMulação por padrão
        public int SimulationIntervalMs { get; set; } = 1000;

        // Eventos
        public event Action<double> OnPVUpdate;
        public event Action<double> OnSPUpdate;
        public event Action<string> OnStatusChange;

        public SerialService()
        {
            Directory.CreateDirectory("C:\\RespawnOS\\logs");
            _logWriter = new StreamWriter("C:\\RespawnOS\\logs\\serial_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log", true) { AutoFlush = true };
        }

        public string[] GetPorts() => SerialPort.GetPortNames();

        public void Open(string portName, int baud = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            if (Simulation)
            {
                OnStatusChange?.Invoke($"SIMULATION MODE ON (no COM opened)");
                return;
            }

            lock (_lock)
            {
                if (_port != null && _port.IsOpen) Close();

                _port = new SerialPort(portName, baud, parity, dataBits, stopBits)
                {
                    Encoding = Encoding.ASCII,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _port.Open();
                _cts = new CancellationTokenSource();
                _readerTask = Task.Run(() => ReaderLoop(_cts.Token));
                OnStatusChange?.Invoke($"Open: {portName} @ {baud}");
                Log($"OPEN {portName} {baud}");
            }
        }

        public void Close()
        {
            if (Simulation)
            {
                OnStatusChange?.Invoke("Simulation mode -> nothing to close");
                return;
            }

            lock (_lock)
            {
                try
                {
                    _cts?.Cancel();
                    _readerTask?.Wait(500);
                }
                catch { }

                if (_port != null)
                {
                    try { _port.Close(); } catch { }
                    _port.Dispose();
                    _port = null;
                }
                OnStatusChange?.Invoke("Closed");
                Log("CLOSED");
            }
        }

        private void ReaderLoop(CancellationToken token)
        {
            var buffer = new StringBuilder();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var b = _port.ReadExisting();
                    if (!string.IsNullOrEmpty(b))
                    {
                        buffer.Append(b);
                        Log($"RX: {b}");
                        // parse simples por linhas
                        if (buffer.ToString().Contains("\n"))
                        {
                            var lines = buffer.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                ParseLine(line.Trim());
                            }
                            buffer.Clear();
                        }
                    }
                }
                catch (TimeoutException) { }
                catch (Exception ex)
                {
                    OnStatusChange?.Invoke("Serial Read Error: " + ex.Message);
                    Log("ERR: " + ex.ToString());
                    Thread.Sleep(200);
                }
            }
        }

        // Exemplo de parser minimal que aceita: "PV:123.4" ou "SP:180"
        private void ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            if (line.StartsWith("PV:", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(line.Substring(3), out double v))
                {
                    OnPVUpdate?.Invoke(v);
                }
            }
            else if (line.StartsWith("SP:", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(line.Substring(3), out double v))
                {
                    OnSPUpdate?.Invoke(v);
                }
            }
            else
            {
                // outras mensagens de status
                OnStatusChange?.Invoke(line);
            }
        }

        public void Write(string text)
        {
            if (Simulation)
            {
                Log("[SIM WRITE] " + text);
                return;
            }
            try
            {
                _port?.Write(text);
                Log("TX: " + text);
            }
            catch (Exception ex)
            {
                OnStatusChange?.Invoke("Write err: " + ex.Message);
                Log("TXERR: " + ex.ToString());
            }
        }

        public void StartSimulation()
        {
            Simulation = true;
            Task.Run(async () =>
            {
                double t = 0;
                while (Simulation)
                {
                    // Aqui você pode gerar a rampa que quiser
                    double pv = 20 + 180.0 * Math.Sin(t * 0.05);
                    OnPVUpdate?.Invoke(Math.Round(pv, 2));
                    OnSPUpdate?.Invoke(180.0);
                    Log("[SIM] PV=" + pv);
                    t += SimulationIntervalMs / 1000.0;
                    await Task.Delay(SimulationIntervalMs);
                }
            });
            OnStatusChange?.Invoke("Simulation started");
        }

        public void StopSimulation()
        {
            Simulation = false;
            OnStatusChange?.Invoke("Simulation stopped");
        }

        private void Log(string s)
        {
            try
            {
                _logWriter.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{s}");
            }
            catch { }
        }

        public void Dispose()
        {
            Close();
            _logWriter?.Dispose();
        }
    }
}

