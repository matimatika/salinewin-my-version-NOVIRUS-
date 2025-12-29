using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using NAudio.Wave;

namespace DesktopSpinnerApp
{
    public class SpinnerForm : Form
    {
        private Bitmap? _screenSnapshot;
        private float _angle = 0, _speed = 0.1f, _acceleration = 0.05f;
        private System.Windows.Forms.Timer _timer;
        private DateTime _startTime;
        private bool _isGlitchPhase = false, _isCurrentlyInverted = false, _isLiquid = false, _isEndStage = false, _isTwinStage = false;
        private float _waveOffset = 0, _scrollOffset = 0;
        private Random _rng = new Random();
        private int _currentTrack = 0;

        private IWavePlayer? _outputDevice;
        private AudioFileReader? _audioFile;

        private float _leftAngle = 0, _rightAngle = 0;
        private long _bytebeatT = 0;
        private byte _lastLeftByte = 0;
        private byte _lastRightByte = 0;

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        public SpinnerForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.DoubleBuffered = true;
            this.TopMost = true;
            this.BackColor = Color.Black;
            Cursor.Hide();

            HideAllWindows();
            System.Threading.Thread.Sleep(600);
            _screenSnapshot = CaptureScreen();
            _startTime = DateTime.Now;

          
            PlayMusic("track1.webm", 1);

            _timer = new System.Windows.Forms.Timer { Interval = 15 };
            _timer.Tick += (s, e) => {
                double elapsed = (DateTime.Now - _startTime).TotalSeconds;
                
             
                _isCurrentlyInverted = (int)(elapsed * 5) % 2 == 0;

               
                if (elapsed > 28) 
                {
                    if (_currentTrack != 4) StartStereoBytebeat();
                    _isTwinStage = true;
                    _isEndStage = false;
                    UpdateTwinVisuals();
                }
                else if (elapsed > 22) 
                {
                    if (_currentTrack != 3) PlayMusic("track3.webm", 3);
                    _isEndStage = true; _isLiquid = false;
                    _scrollOffset += 30;
                    if (_scrollOffset >= this.Height) _scrollOffset = 0;
                }
                else if (elapsed > 15) 
                {
                    if (_currentTrack != 2) PlayMusic("track2.webm", 2);
                    _isLiquid = true; _isGlitchPhase = false; _angle = 0; _waveOffset += 0.15f;
                }
                else 
                {
                 
                    _angle += _speed; _speed += _acceleration;
                    if (_speed > 60f) _speed = 60f;


                    if (elapsed > 5) _isGlitchPhase = true;
                }
                this.Invalidate();
            };
            _timer.Start();

            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Application.Exit(); };
        }

        private void StartStereoBytebeat()
        {
            _currentTrack = 4;
            _outputDevice?.Stop();
            _outputDevice?.Dispose();

            int sampleRate = 8000;

            byte[] buffer = new byte[sampleRate * 60 * 2]; 
            for (int t = 0; t < buffer.Length / 2; t++)
            {
                byte left = (byte)(t * ((t >> 9 | t >> 13) & 25 & t >> 6));
                byte right = (byte)((t + 100) * (((t + 100) >> 9 | (t + 100) >> 13) & 25 & (t + 100) >> 6));
                
                buffer[t * 2] = left;
                buffer[t * 2 + 1] = right;
            }

            var ms = new MemoryStream(buffer);
            var stream = new RawSourceWaveStream(ms, new WaveFormat(sampleRate, 8, 2));
            
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(stream);
            _outputDevice.Play();
        }

        private void UpdateTwinVisuals()
        {
            _bytebeatT += 140; 
            
            byte leftSample = (byte)(_bytebeatT * ((_bytebeatT >> 9 | _bytebeatT >> 13) & 25 & _bytebeatT >> 6));
            byte rightSample = (byte)((_bytebeatT + 100) * (((_bytebeatT + 100) >> 9 | (_bytebeatT + 100) >> 13) & 25 & (_bytebeatT + 100) >> 6));

          
            if (leftSample > 160 && _lastLeftByte <= 160) _leftAngle = _rng.Next(-25, 26);
            if (rightSample > 160 && _lastRightByte <= 160) _rightAngle = _rng.Next(-25, 26);

     
            _leftAngle *= 0.88f;
            _rightAngle *= 0.88f;

            _lastLeftByte = leftSample;
            _lastRightByte = rightSample;
        }

        private void PlayMusic(string fileName, int trackId)
        {
            try {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                if (!File.Exists(fullPath)) return;
                _currentTrack = trackId;
                _outputDevice?.Stop(); _outputDevice?.Dispose(); _audioFile?.Dispose();
                _outputDevice = new WaveOutEvent();
                _audioFile = new AudioFileReader(fullPath);
                _outputDevice.Init(_audioFile);
                _outputDevice.Play();
            } catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_screenSnapshot == null) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_isTwinStage)
            {
                int halfW = Width / 2;
                DrawSplitScreen(g, new Rectangle(0, 0, halfW, Height), _leftAngle);
                DrawSplitScreen(g, new Rectangle(halfW, 0, halfW, Height), _rightAngle);
            }
            else if (_isEndStage) 
            {
                ImageAttributes chaosAttr = new ImageAttributes();
                float t = (float)(DateTime.Now - _startTime).TotalSeconds * 5.0f;
                float inv = _isCurrentlyInverted ? -1f : 1f;
                ColorMatrix cm = new ColorMatrix(new float[][] {
                    new float[] { (float)Math.Sin(t) * inv, (float)Math.Cos(t), 0, 0, 0 },
                    new float[] { 0, (float)Math.Sin(t*1.2f) * inv, 0, 0, 0 },
                    new float[] { (float)Math.Cos(t*0.5f), 0, inv, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0.2f, 0, 0, 0, 1 }
                });
                chaosAttr.SetColorMatrix(cm);
                g.DrawImage(_screenSnapshot, new Rectangle(_rng.Next(-15, 16), (int)_scrollOffset, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, chaosAttr);
                g.DrawImage(_screenSnapshot, new Rectangle(_rng.Next(-15, 16), (int)_scrollOffset - Height, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, chaosAttr);
            }
            else if (_isLiquid) 
            {
                ImageAttributes attr = new ImageAttributes();
                if (_isCurrentlyInverted) attr.SetColorMatrix(new ColorMatrix(new float[][] { new float[] {-1,0,0,0,0}, new float[] {0,1,0,0,0}, new float[] {0,0,1,0,0}, new float[] {0,0,0,1,0}, new float[] {1,0,0,0,1} }));
                for (int y = 0; y < Height; y += 4) {
                    float xOff = (float)Math.Sin((y / 40.0) + _waveOffset) * 70;
                    g.DrawImage(_screenSnapshot, new Rectangle((int)xOff, y, Width, 4), 0, y, Width, 4, GraphicsUnit.Pixel, attr);
                }
            }
            else 
            {
                g.TranslateTransform(Width / 2f, Height / 2f);
                g.RotateTransform(_angle);
                ImageAttributes? attr = null;
                if (_isGlitchPhase && _isCurrentlyInverted) {
                    attr = new ImageAttributes();
                    attr.SetColorMatrix(new ColorMatrix(new float[][] { new float[] {-1,0,0,0,0}, new float[] {0,-1,0,0,0}, new float[] {0,0,-1,0,0}, new float[] {0,0,0,1,0}, new float[] {1,1,1,0,1} }));
                }
                g.DrawImage(_screenSnapshot, new Rectangle((int)(-Width/2), (int)(-Height/2), Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, attr);
            }
        }

        private void DrawSplitScreen(Graphics g, Rectangle rect, float angle)
        {
            if (_screenSnapshot == null) return;
            GraphicsState state = g.Save();
            g.SetClip(rect);
            g.TranslateTransform(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            if (Math.Abs(angle) > 0.01) g.RotateTransform(angle);
            g.DrawImage(_screenSnapshot, -rect.Width / 2f, -rect.Height / 2f, rect.Width, rect.Height);
            g.Restore(state);
        }

        private void HideAllWindows()
        {
            keybd_event(0x5B, 0, 0, 0); keybd_event(0x44, 0, 0, 0);
            keybd_event(0x44, 0, 2, 0); keybd_event(0x5B, 0, 2, 0);
        }

        private Bitmap CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            Bitmap b = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, bounds.Size);
            return b;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _audioFile?.Dispose();
            base.OnFormClosing(e);
        }

        [STAThread] static void Main() { Application.Run(new SpinnerForm()); }
    }
}
