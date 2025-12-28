using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace DesktopSpinnerApp
{
    public class SpinnerForm : Form
    {
        private Bitmap? _screenSnapshot;
        private float _angle = 0, _speed = 0.1f, _acceleration = 0.05f;
        private System.Windows.Forms.Timer _timer;
        private DateTime _startTime;
        private bool _isGlitchPhase = false, _isCurrentlyInverted = false, _isLiquid = false, _isEndStage = false;
        private float _waveOffset = 0, _scrollOffset = 0;
        private Random _rng = new Random();

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

            _timer = new System.Windows.Forms.Timer { Interval = 15 };
            _timer.Tick += (s, e) => {
                double elapsed = (DateTime.Now - _startTime).TotalSeconds;

                
                _isCurrentlyInverted = (int)(elapsed * 5) % 2 == 0;

                if (elapsed > 22) 
                {
                    _isEndStage = true;
                    _isLiquid = false;
                    _scrollOffset += 25; 
                    if (_scrollOffset >= this.Height) _scrollOffset = 0;
                }
                else if (elapsed > 15)
                {
                    _isLiquid = true;
                    _isGlitchPhase = false;
                    _angle = 0;
                    _waveOffset += 0.12f;
                }
                else 
                {
                    _angle += _speed;
                    _speed += _acceleration;
                    if (_speed > 60f) _speed = 60f;
                    if (elapsed > 5) _isGlitchPhase = true;
                }
                this.Invalidate();
            };
            _timer.Start();
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Application.Exit(); };
        }

        private void HideAllWindows()
        {
            keybd_event(0x5B, 0, 0, 0); keybd_event(0x44, 0, 0, 0);
            keybd_event(0x44, 0, 2, 0); keybd_event(0x5B, 0, 2, 0);
        }

        private Bitmap CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Bitmap b = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, bounds.Size);
            return b;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_screenSnapshot == null) return;
            Graphics g = e.Graphics;

            if (_isEndStage)
            {
                ImageAttributes chaosAttr = new ImageAttributes();
                float t = (float)(DateTime.Now - _startTime).TotalSeconds * 4.0f; 
                float inv = _isCurrentlyInverted ? -1f : 1f;
                float off = _isCurrentlyInverted ? 1f : 0f;

              
                ColorMatrix cm = new ColorMatrix(new float[][] {
                    new float[] { (float)Math.Sin(t) * inv, (float)Math.Cos(t*1.5f), (float)Math.Sin(t*0.5f), 0, 0 },
                    new float[] { (float)Math.Cos(t*0.8f), (float)Math.Sin(t*1.2f) * inv, (float)Math.Cos(t*2.0f), 0, 0 },
                    new float[] { (float)Math.Sin(t*2.2f), (float)Math.Cos(t*0.3f), (float)Math.Sin(t*1.7f) * inv, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { off, off, off, 0, 1 }
                });
                chaosAttr.SetColorMatrix(cm);

               
                int shakeX = _rng.Next(-10, 11);

                
                g.DrawImage(_screenSnapshot, new Rectangle(shakeX, (int)_scrollOffset, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, chaosAttr);
                g.DrawImage(_screenSnapshot, new Rectangle(shakeX, (int)_scrollOffset - Height, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, chaosAttr);
            }
            else if (_isLiquid) 
            {
                ImageAttributes attr = new ImageAttributes();
                float inv = _isCurrentlyInverted ? -1f : 1f;
                float off = _isCurrentlyInverted ? 1f : 0f;
                attr.SetColorMatrix(new ColorMatrix(new float[][] {
                    new float[] { inv,0,0,0,0 }, new float[] { 0,0,0,0,0 }, new float[] { 0,0,0,0,0 },
                    new float[] { 0,0,0,1,0 }, new float[] { off,0,0,0,1 }
                }));

                for (int y = 0; y < Height; y += 3)
                {
                    float xOff = (float)Math.Sin((y / 50.0) + _waveOffset) * 60;
                    g.DrawImage(_screenSnapshot, new Rectangle((int)xOff, y, Width, 3), 0, y, Width, 3, GraphicsUnit.Pixel, attr);
                }
            }
            else 
            {
                g.TranslateTransform(Width / 2f, Height / 2f);
                g.RotateTransform(_angle);
                ImageAttributes attr = null;
                if (_isGlitchPhase && _isCurrentlyInverted) {
                    attr = new ImageAttributes();
                    attr.SetColorMatrix(new ColorMatrix(new float[][] {
                        new float[] {-1,0,0,0,0}, new float[] {0,-1,0,0,0}, new float[] {0,0,-1,0,0},
                        new float[] {0,0,0,1,0}, new float[] {1,1,1,0,1}
                    }));
                }
                g.DrawImage(_screenSnapshot, new Rectangle(-Width/2, -Height/2, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, attr);
            }
        }

        [STAThread] static void Main() { Application.Run(new SpinnerForm()); }
    }
}
