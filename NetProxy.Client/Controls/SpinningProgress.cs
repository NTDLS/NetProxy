using System.Drawing.Drawing2D;

namespace NetProxy.Client.Controls
{
    public partial class SpinningProgress : UserControl
    {
        private Color _mInactiveColour = Color.FromArgb(218, 218, 218);
        private Color _mActiveColour = Color.FromArgb(35, 146, 33);
        private Color _mTransistionColour = Color.FromArgb(129, 242, 121);

        private Region _innerBackgroundRegion;
        private GraphicsPath[] _segmentPaths = new GraphicsPath[12];

        private bool _mAutoIncrement = true;
        private double _mIncrementFrequency = 100;
        private bool _mBehindIsActive = true;
        private Int32 _mTransitionSegment = 0;

        private System.Timers.Timer _mAutoRotateTimer = null;

        public SpinningProgress()
        {
            InitializeComponent();

            // Add any initialization after the InitializeComponent() call.
            this.CalculateSegments();
            this.AutoIncrementFrequency = 100;
            this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.AutoIncrement = true;
        }

        Color InactiveSegmentColour
        {
            get
            {
                return _mInactiveColour;
            }
            set
            {
                _mInactiveColour = value;
                Invalidate();
            }
        }

        Color ActiveSegmentColour
        {
            get
            {
                return _mActiveColour;
            }
            set
            {
                _mActiveColour = value;
                Invalidate();
            }
        }

        Color TransistionSegmentColour
        {
            get
            {
                return _mTransistionColour;
            }
            set
            {
                _mTransistionColour = value;
                Invalidate();
            }
        }

        bool BehindTransistionSegmentIsActive
        {
            get
            {
                return _mBehindIsActive;
            }
            set
            {
                _mBehindIsActive = value;
                Invalidate();
            }
        }

        Int32 TransistionSegment
        {
            get
            {
                return _mTransitionSegment;
            }
            set
            {
                if (value > 12 || value < -1)
                {
                    throw new ArgumentException("TransistionSegment must be between -1 and 12");
                }
                _mTransitionSegment = value;
                Invalidate();
            }
        }

        bool AutoIncrement
        {
            get
            {
                return _mAutoIncrement;
            }
            set
            {
                _mAutoIncrement = value;

                if (value == false && _mAutoRotateTimer != null)
                {
                    _mAutoRotateTimer.Dispose();
                    _mAutoRotateTimer = null;
                }

                if (value == true && _mAutoRotateTimer == null)
                {
                    _mAutoRotateTimer = new System.Timers.Timer(_mIncrementFrequency);

                    _mAutoRotateTimer.Elapsed += this.IncrementTransisionSegment;
                    _mAutoRotateTimer.Start();
                }
            }
        }

        public double AutoIncrementFrequency
        {
            get
            {
                return _mIncrementFrequency;
            }
            set
            {
                _mIncrementFrequency = value;

                if (_mAutoRotateTimer != null)
                {
                    AutoIncrement = false;
                    AutoIncrement = true;
                }
            }
        }

        private void CalculateSegments()
        {
            Rectangle rctFull = new Rectangle(0, 0, this.Width, this.Height);
            Rectangle rctInner = new Rectangle((int)(this.Width * (7.0 / 30.0)),
                                                (int)(this.Height * (7.0 / 30.0)),
                                                (int)(this.Width - (this.Width * (7.0 / 30.0) * 2.0)),
                                                (int)(this.Height - (this.Height * (7.0 / 30.0) * 2.0)));
            GraphicsPath pthInnerBackground;

            //Create 12 segment pieces
            for (int intCount = 0; intCount < 12; intCount++)
            {
                _segmentPaths[intCount] = new GraphicsPath();

                //We subtract 90 so that the starting segment is at 12 o'clock
                _segmentPaths[intCount].AddPie(rctFull, (intCount * 30) - 90, 25);
            }

            //Create the center circle cut-out
            pthInnerBackground = new GraphicsPath();
            pthInnerBackground.AddPie(rctInner, 0, 360);
            _innerBackgroundRegion = new Region(pthInnerBackground);
        }

        private void SpinningProgress_EnabledChanged(object sender, System.EventArgs e)
        {
            if (this.Enabled)
            {
                if (_mAutoRotateTimer != null)
                {
                    _mAutoRotateTimer.Start();
                }
            }
            else
            {
                if (_mAutoRotateTimer != null)
                {
                    _mAutoRotateTimer.Stop();
                }
            }
        }

        private void IncrementTransisionSegment(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_mTransitionSegment == 12)
            {
                _mTransitionSegment = 0;
                _mBehindIsActive = !_mBehindIsActive;
            }
            else if (_mTransitionSegment == -1)
            {
                _mTransitionSegment = 0;
            }
            else
            {
                _mTransitionSegment += 1;
            }
            Invalidate();
        }

        private void ProgressDisk_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.ExcludeClip(_innerBackgroundRegion);

            for (int intCount = 0; intCount < 12; intCount++)
            {
                if (this.Enabled)
                {
                    if (intCount == _mTransitionSegment)
                    {
                        //If this segment is the transistion segment, colour it differently
                        e.Graphics.FillPath(new SolidBrush(_mTransistionColour), _segmentPaths[intCount]);
                    }
                    else if (intCount < _mTransitionSegment)
                    {
                        //This segment is behind the transistion segment
                        if (_mBehindIsActive)
                        {
                            //If behind the transistion should be active, 
                            //colour it with the active colour
                            e.Graphics.FillPath(new SolidBrush(_mActiveColour), _segmentPaths[intCount]);
                        }
                        else
                        {
                            //If behind the transistion should be in-active, 
                            //colour it with the in-active colour
                            e.Graphics.FillPath(new SolidBrush(_mInactiveColour), _segmentPaths[intCount]);
                        }
                    }
                    else
                    {
                        //This segment is ahead of the transistion segment
                        if (_mBehindIsActive)
                        {
                            //If behind the the transistion should be active, 
                            //colour it with the in-active colour
                            e.Graphics.FillPath(new SolidBrush(_mInactiveColour), _segmentPaths[intCount]);
                        }
                        else
                        {
                            //If behind the the transistion should be in-active, 
                            //colour it with the active colour
                            e.Graphics.FillPath(new SolidBrush(_mActiveColour), _segmentPaths[intCount]);
                        }
                    }
                }
                else
                {
                    //Draw all segments in in-active colour if not enabled
                    e.Graphics.FillPath(new SolidBrush(_mInactiveColour), _segmentPaths[intCount]);
                }
            }
        }

        private void ProgressDisk_Resize(object sender, System.EventArgs e)
        {
            CalculateSegments();
        }

        private void ProgressDisk_SizeChanged(object sender, System.EventArgs e)
        {
            CalculateSegments();
        }
    }
}
