using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;

namespace DesktopSpinnerApp
{
    public class SpinnerForm : Form
    {
        private Bitmap? _screenSnapshot;
        private float _angle = 0, _speed = 0.1f, _acceleration = 0.05f;
        private System.Windows.Forms.Timer _timer;
        private DateTime _startTime;
        
        private bool _isGlitchPhase = false, _isCurrentlyInverted = false, _isLiquid = false;
        private bool _isEndStage = false, _isTwinStage = false, _isBsodStage = false, _isGreenStage = false;
        
        private float _waveOffset = 0, _scrollOffset = 0, _zoomScale = 1.0f;
        private Random _rng = new Random();
        private List<Point> _paintPoints = new List<Point>();

        private IWavePlayer? _outputDevice;
        private AudioFileReader? _audioFile;
        private RawSourceWaveStream? _finalStream;
        private int _currentTrack = 0;

        private bool _isMirroredLeft = false, _isMirroredRight = false;
        private long _bytebeatT = 0;
        private byte _lastLeftByte = 0, _lastRightByte = 0;

        private DateTime _bsodStartTime;
        private List<string> _errorMessages = new List<string>();
        private float _bsodShakeIntensity = 0;
        private bool _bsodComplete = false;

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;

        public SpinnerForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.DoubleBuffered = true;
            this.TopMost = true;
            this.BackColor = Color.Black;
            Cursor.Hide();

            PrepareErrorMessages();

            HideAllWindows();
            System.Threading.Thread.Sleep(600);
            _screenSnapshot = CaptureScreen();
            _startTime = DateTime.Now;

            PlayMusic("track1.webm", 1);

            _timer = new System.Windows.Forms.Timer { Interval = 15 };
            _timer.Tick += (s, e) => {
                double elapsed = (DateTime.Now - _startTime).TotalSeconds;
                _isCurrentlyInverted = (int)(elapsed * 5) % 2 == 0;

                if (elapsed > 60) { 
                    _timer.Stop();
                    Task.Run(() => ShowFinalCrash());
                }
                else if (elapsed > 45) { 
                    if (!_isGreenStage) StartGreenStage();
                    _paintPoints.Add(new Point(_rng.Next(Width), _rng.Next(Height)));
                    if (_paintPoints.Count > 500) _paintPoints.RemoveAt(0);
                }
                else if (elapsed > 28) { 
                    if (!_isBsodStage) StartBsodSequence();
                    
                    _bsodShakeIntensity = Math.Max(0, 20 - (float)(DateTime.Now - _bsodStartTime).TotalSeconds * 2);
                    
                    if (_zoomScale > 0.05f && !_bsodComplete) 
                        _zoomScale -= 0.003f;
                    else if (!_bsodComplete)
                    {
                        _zoomScale = 0;
                        _bsodComplete = true;
                        ShowAllErrorsSimultaneously();
                    }
                }
                else if (elapsed > 22) { 
                    _isTwinStage = true; _isEndStage = false;
                    UpdateTwinVisuals();
                }
                else if (elapsed > 15) { 
                    if (_currentTrack != 3) PlayMusic("track3.webm", 3);
                    _isEndStage = true; _isLiquid = false;
                    _scrollOffset += 30; if (_scrollOffset >= Height) _scrollOffset = 0;
                }
                else if (elapsed > 8) { 
                    if (_currentTrack != 2) PlayMusic("track2.webm", 2);
                    _isLiquid = true; _isGlitchPhase = false; _angle = 0; _waveOffset += 0.15f;
                }
                else { 
                    _angle += _speed; _speed += _acceleration;
                    if (_speed > 60f) _speed = 60f;
                    if (elapsed > 3) _isGlitchPhase = true;
                }
                this.Invalidate();
            };
            _timer.Start();
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Application.Exit(); };
        }

        private void PrepareErrorMessages()
        {
            _errorMessages.AddRange(new string[]
            {
                "CRITICAL_PROCESS_DIED",
                "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED",
                "IRQL_NOT_LESS_OR_EQUAL",
                "PAGE_FAULT_IN_NONPAGED_AREA",
                "KERNEL_SECURITY_CHECK_FAILURE",
                "VIDEO_TDR_FAILURE",
                "SYSTEM_SERVICE_EXCEPTION",
                "DPC_WATCHDOG_VIOLATION",
                "MACHINE_CHECK_EXCEPTION",
                "HYPERVISOR_ERROR",
                "UNEXPECTED_KERNEL_MODE_TRAP",
                "KERNEL_MODE_HEAP_CORRUPTION",
                "PFN_LIST_CORRUPT",
                "VIDEO_DXGKRNL_FATAL_ERROR",
                "MEMORY_MANAGEMENT",
                "DRIVER_IRQL_NOT_LESS_OR_EQUAL",
                "BAD_POOL_HEADER",
                "DRIVER_POWER_STATE_FAILURE",
                "ATTEMPTED_WRITE_TO_READONLY_MEMORY",
                "REGISTRY_FILTER_DRIVER_EXCEPTION"
            });
        }

        private void StartBsodSequence() {
            _isBsodStage = true; _isTwinStage = false;
            _bsodStartTime = DateTime.Now;
            _bsodComplete = false;
            _zoomScale = 1.0f;
            
            Task.Run(() => {
                for (int i = 0; i < 20; i++) 
                {
                    try { Process.Start("cmd.exe"); } catch { }
                    System.Threading.Thread.Sleep(50);
                }
                System.Threading.Thread.Sleep(500);
                
                _screenSnapshot = CaptureScreen();
                
                foreach (var proc in Process.GetProcessesByName("cmd")) 
                {
                    try { proc.Kill(); } catch { }
                }
                foreach (var proc in Process.GetProcessesByName("conhost"))
                {
                    try { proc.Kill(); } catch { }
                }
                
                StartBytebeat();
            });
        }

        private void ShowAllErrorsSimultaneously()
        {
            Task.Run(() =>
            {
                var tasks = new List<Task>();
                
                for (int i = 0; i < _errorMessages.Count; i++)
                {
                    int index = i;
                    tasks.Add(Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(_rng.Next(0, 200)); 
                        
                        var errorForm = new Form
                        {
                            FormBorderStyle = FormBorderStyle.FixedDialog,
                            ControlBox = false,
                            Text = "Windows Error",
                            StartPosition = FormStartPosition.CenterScreen,
                            Size = new Size(400, 200),
                            TopMost = true
                        };
                       
                        errorForm.Location = new Point(
                            _rng.Next(0, Screen.PrimaryScreen.Bounds.Width - 400),
                            _rng.Next(0, Screen.PrimaryScreen.Bounds.Height - 200)
                        );
                        
                        var label = new Label
                        {
                            Text = $"ERROR: {_errorMessages[index]}\n\nError Code: 0x{_rng.Next(0x1000, 0xFFFF):X8}\n\nYour PC will restart automatically.",
                            Font = new Font("Segoe UI", 10),
                            ForeColor = Color.White,
                            BackColor = Color.FromArgb(0, 120, 215),
                            Dock = DockStyle.Fill,
                            TextAlign = ContentAlignment.MiddleCenter,
                            Padding = new Padding(20)
                        };
                        
                        errorForm.Controls.Add(label);
                        errorForm.BackColor = Color.FromArgb(0, 120, 215);
                        
                        errorForm.Show();
                       
                        System.Threading.Thread.Sleep(_rng.Next(5000, 10000));
                        try { errorForm.Invoke((MethodInvoker)(() => errorForm.Close())); } catch { }
                    }));
                }
                
               
                Task.Run(() =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        ShowFullScreenBsod();
                    }
                });
            });
        }

        private void ShowFullScreenBsod()
        {
            try
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    var bsodForm = new Form
                    {
                        FormBorderStyle = FormBorderStyle.None,
                        WindowState = FormWindowState.Maximized,
                        TopMost = true,
                        BackColor = Color.FromArgb(0, 120, 215),
                        ControlBox = false
                    };
                    
                    var mainLabel = new Label
                    {
                        Text = ":(\nYour PC ran into a problem and needs to restart.\nWe're just collecting some error info, and then we'll restart for you.",
                        Font = new Font("Segoe UI", 24),
                        ForeColor = Color.White,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Padding = new Padding(100)
                    };
                    
                    var errorLabel = new Label
                    {
                        Text = $"Stop code: {_errorMessages[_rng.Next(_errorMessages.Count)]}\n\nIf you call a support person, give them this info:\nError code: 0x{_rng.Next(0x100000, 0xFFFFFF):X6}",
                        Font = new Font("Segoe UI", 14),
                        ForeColor = Color.White,
                        Dock = DockStyle.Bottom,
                        Height = 200,
                        TextAlign = ContentAlignment.TopCenter
                    };
                    
                    bsodForm.Controls.Add(mainLabel);
                    bsodForm.Controls.Add(errorLabel);
                    
                    bsodForm.Show();
                    
                    
                    System.Threading.Timer timer = null;
                    timer = new System.Threading.Timer(_ =>
                    {
                        bsodForm.Invoke((MethodInvoker)(() => bsodForm.Close()));
                        timer?.Dispose();
                    }, null, _rng.Next(3000, 6000), System.Threading.Timeout.Infinite);
                }));
            }
            catch { }
        }

        private void StartGreenStage() {
            _isGreenStage = true; _isBsodStage = false;
            _screenSnapshot = CaptureScreen(); 
            
            Task.Run(() => {
                for (int i = 0; i < 10; i++) {
                    try
                    {
                        this.Invoke((MethodInvoker)(() =>
                        {
                            MessageBox.Show(this, 
                                "CRITICAL_SYSTEM_CORRUPTION\n\nAll system components have failed.\nThe matrix is collapsing.",
                                "FATAL ERROR", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Stop, 
                                MessageBoxDefaultButton.Button1, 
                                MessageBoxOptions.ServiceNotification);
                        }));
                    }
                    catch { }
                    System.Threading.Thread.Sleep(300);
                }
            });
        }

        private void ShowFinalCrash()
        {
            this.Invoke((MethodInvoker)(() =>
            {
               
                this.BackColor = Color.Black;
                this.Invalidate();
                
              
                var finalForm = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    WindowState = FormWindowState.Maximized,
                    TopMost = true,
                    BackColor = Color.Black
                };
                
                var label = new Label
                {
                    Text = "SYSTEM NORMAL COMPLETE\n\n[BY LITMATI]\n[popac.exe has stopped working]\n\nPress any key to exit...",
                    Font = new Font("Consolas", 24),
                    ForeColor = Color.Lime,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                
                finalForm.Controls.Add(label);
                finalForm.KeyDown += (s, e) => Application.Exit();
                finalForm.Show();
                
                this.Hide();
            }));
        }

        private void StartBytebeat() {
            _currentTrack = 5; 
            _outputDevice?.Stop();
            
           
            int sr = 8000; 
            byte[] buf = new byte[sr * 90]; 
            
            for (int t = 0; t < buf.Length; t++) 
            {
                // Более сложная и шумная формула
                byte val = (byte)(
                    ((t >> 6 | t | t >> (t >> 12)) * 20 + ((t >> 11) & 15)) ^ 
                    (t >> 4) | (t << 3)
                );
                buf[t] = val;
            }
            
            _finalStream = new RawSourceWaveStream(new MemoryStream(buf), new WaveFormat(sr, 8, 1));
            _outputDevice = new WaveOutEvent(); 
            _outputDevice.Init(_finalStream); 
            _outputDevice.Play();
            
            // Зацикливание с небольшими изменениями
            _outputDevice.PlaybackStopped += (s, e) =>
            {
                if (_isBsodStage)
                {
                    _finalStream.Position = 0;
                    _outputDevice.Play();
                }
            };
        }

        private void UpdateTwinVisuals() {
            _bytebeatT += 140;
            byte l = (byte)(_bytebeatT * ((_bytebeatT >> 9 | _bytebeatT >> 13) & 25 & _bytebeatT >> 6));
            byte r = (byte)((_bytebeatT + 100) * (((_bytebeatT + 100) >> 9 | (_bytebeatT + 100) >> 13) & 25 & (_bytebeatT + 100) >> 6));
            if (l > 160 && _lastLeftByte <= 160) _isMirroredLeft = !_isMirroredLeft;
            if (r > 160 && _lastRightByte <= 160) _isMirroredRight = !_isMirroredRight;
            _lastLeftByte = l; _lastRightByte = r;
        }

        private void PlayMusic(string file, int id) {
            try {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                if (!File.Exists(path)) return;
                _currentTrack = id; 
                _outputDevice?.Stop(); 
                _outputDevice?.Dispose(); 
                _audioFile?.Dispose();
                _audioFile = new AudioFileReader(path); 
                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(_audioFile); 
                _outputDevice.Play();
            } catch { }
        }

        protected override void OnPaint(PaintEventArgs e) {
            if (_screenSnapshot == null) return;
            Graphics g = e.Graphics;

            if (_isGreenStage) {
                ImageAttributes attr = new ImageAttributes();
                attr.SetColorMatrix(new ColorMatrix(new float[][] { 
                    new float[] {0,0,0,0,0}, 
                    new float[] {0,1.8f,0,0,0}, 
                    new float[] {0,0,0,0,0}, 
                    new float[] {0,0,0,1,0}, 
                    new float[] {0,0.3f,0,0,1} 
                }));
                g.DrawImage(_screenSnapshot, new Rectangle(0,0,Width,Height), 0,0,Width,Height, GraphicsUnit.Pixel, attr);
                
                using (Pen p = new Pen(Color.Lime, 60)) 
                { 
                    foreach (var pt in _paintPoints) 
                        g.DrawEllipse(p, pt.X - 30, pt.Y - 30, 60, 60); 
                }
                
                // Добавляем текст поверх
                using (var font = new Font("Arial", 48, FontStyle.Bold))
                {
                    g.DrawString("SYSTEM CORRUPTED", font, Brushes.Lime, 
                        Width/2 - 300 + _rng.Next(-5,5), 
                        Height/2 - 50 + _rng.Next(-5,5));
                }
            }
            else if (_isBsodStage) {
                if (_zoomScale > 0.1f && !_bsodComplete) {
                    float w = Width * _zoomScale, h = Height * _zoomScale;
                    
                    // Эффект дрожания
                    float shakeX = _rng.Next(-(int)_bsodShakeIntensity, (int)_bsodShakeIntensity + 1);
                    float shakeY = _rng.Next(-(int)_bsodShakeIntensity, (int)_bsodShakeIntensity + 1);
                    
                    g.DrawImage(_screenSnapshot, 
                        (Width-w)/2 + shakeX, 
                        (Height-h)/2 + shakeY, 
                        w, h);
                        
                    // Затемнение по краям
                    using (var brush = new LinearGradientBrush(
                        new Rectangle(0,0,Width,Height), 
                        Color.FromArgb(150, 0, 120, 215), 
                        Color.Transparent, 
                        45f))
                    {
                        g.FillRectangle(brush, 0, 0, Width, Height);
                    }
                } 
                else if (_bsodComplete) {
                    // Полноэкранный BSOD с эффектами
                    g.Clear(Color.FromArgb(0, 120, 215));
                    
                    // Дрожащий текст
                    int shake = _rng.Next(-3, 4);
                    using (var font = new Font("Segoe UI", 80, FontStyle.Bold))
                    {
                        g.DrawString(":(", font, Brushes.White, 
                            100 + shake, 
                            100 + shake);
                    }
                    
                    using (var font = new Font("Segoe UI", 25))
                    {
                        string error = _errorMessages[_rng.Next(_errorMessages.Count)];
                        g.DrawString($"ERROR: {error}", font, Brushes.White, 
                            100, 250);
                        g.DrawString($"Stop code: 0x{_rng.Next(0x100000, 0xFFFFFF):X6}", font, Brushes.White, 
                            100, 300);
                        g.DrawString($"{_rng.Next(0, 100)}% complete", font, Brushes.White, 
                            100, 350);
                    }
                    
                    // Мигающий курсор
                    if ((DateTime.Now.Millisecond / 500) % 2 == 0)
                    {
                        g.FillRectangle(Brushes.White, 100, 380, 20, 4);
                    }
                }
            }
            else if (_isTwinStage) {
                DrawSplit(g, new Rectangle(0,0,Width/2,Height), _isMirroredLeft);
                DrawSplit(g, new Rectangle(Width/2,0,Width/2,Height), _isMirroredRight);
                
                // Разделительная линия
                using (var pen = new Pen(Color.Red, 3))
                {
                    g.DrawLine(pen, Width/2, 0, Width/2, Height);
                }
            }
            else if (_isEndStage) {
                ImageAttributes attr = new ImageAttributes();
                float t = (float)(DateTime.Now - _startTime).TotalSeconds * 5.0f;
                float inv = _isCurrentlyInverted ? -2f : 2f;
                attr.SetColorMatrix(new ColorMatrix(new float[][] { 
                    new float[] {(float)Math.Sin(t)*inv,0,0,0,0}, 
                    new float[] {0,(float)Math.Sin(t*1.2f)*inv,0,0,0}, 
                    new float[] {0,0,inv,0,0}, 
                    new float[] {0,0,0,1,0}, 
                    new float[] {0.3f,0.3f,0.3f,0,1} 
                }));
                
                g.DrawImage(_screenSnapshot, 
                    new Rectangle(_rng.Next(-15,16), (int)_scrollOffset, Width, Height), 
                    0,0,Width,Height, GraphicsUnit.Pixel, attr);
                g.DrawImage(_screenSnapshot, 
                    new Rectangle(_rng.Next(-15,16), (int)_scrollOffset-Height, Width, Height), 
                    0,0,Width,Height, GraphicsUnit.Pixel, attr);
            }
            else if (_isLiquid) {
                ImageAttributes attr = new ImageAttributes();
                float b = 1.6f; float inv = _isCurrentlyInverted ? -b : b;
                attr.SetColorMatrix(new ColorMatrix(new float[][] { 
                    new float[] {inv,0,0,0,0}, 
                    new float[] {0,b,0,0,0}, 
                    new float[] {0,0,b,0,0}, 
                    new float[] {0,0,0,1,0}, 
                    new float[] {1,0.2f,0.2f,0,1} 
                }));
                for (int y = 0; y < Height; y += 4) {
                    int xOff = (int)(Math.Sin((y / 40.0) + _waveOffset) * 70);
                    g.DrawImage(_screenSnapshot, 
                        new Rectangle(xOff, y, Width, 4), 
                        0, y, Width, 4, GraphicsUnit.Pixel, attr);
                }
            }
            else {
                g.TranslateTransform(Width/2f, Height/2f); 
                g.RotateTransform(_angle);
                
                ImageAttributes? attr = null;
                if (_isGlitchPhase && _isCurrentlyInverted) {
                    attr = new ImageAttributes();
                    attr.SetColorMatrix(new ColorMatrix(new float[][] { 
                        new float[] {-1.2f,0,0,0,0}, 
                        new float[] {0,-1.2f,0,0,0}, 
                        new float[] {0,0,-1.2f,0,0}, 
                        new float[] {0,0,0,1,0}, 
                        new float[] {1,1,1,0,1} 
                    }));
                }
                
                g.DrawImage(_screenSnapshot, 
                    new Rectangle((int)(-Width/2), (int)(-Height/2), Width, Height), 
                    0,0,Width,Height, GraphicsUnit.Pixel, attr);
                    
                // Эффект виньетирования при вращении
                if (_speed > 30)
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(-Width/2, -Height/2, Width, Height);
                        using (var brush = new PathGradientBrush(path))
                        {
                            brush.CenterColor = Color.Transparent;
                            brush.SurroundColors = new Color[] { Color.FromArgb(150, 0, 0, 0) };
                            g.FillEllipse(brush, -Width/2, -Height/2, Width, Height);
                        }
                    }
                }
            }
        }

        private void DrawSplit(Graphics g, Rectangle r, bool m) {
            GraphicsState s = g.Save(); 
            g.SetClip(r);
            g.TranslateTransform(r.X + r.Width/2f, r.Y + r.Height/2f);
            if (m) g.ScaleTransform(-1, 1);
            if (_screenSnapshot != null) 
                g.DrawImage(_screenSnapshot, -r.Width/2f, -r.Height/2f, r.Width, r.Height);
            g.Restore(s);
        }

        private void HideAllWindows() {
            keybd_event(0x5B, 0, 0, 0); 
            keybd_event(0x44, 0, 0, 0);
            keybd_event(0x44, 0, 2, 0); 
            keybd_event(0x5B, 0, 2, 0);
            System.Threading.Thread.Sleep(100);
        }

        private Bitmap CaptureScreen() {
            Rectangle b = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0,0,1920,1080);
            Bitmap bmp = new Bitmap(b.Width, b.Height);
            using (Graphics g = Graphics.FromImage(bmp)) 
                g.CopyFromScreen(0,0,0,0, b.Size);
            return bmp;
        }

        [STAThread] 
        static void Main() { 
            Application.EnableVisualStyles(); 
            Application.SetCompatibleTextRenderingDefault(false); 
            Application.Run(new SpinnerForm()); 
        }
    }
}
