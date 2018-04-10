using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetProxy.Client.Controls
{
    public partial class SpinningProgress : UserControl
    {
        private Color m_InactiveColour = Color.FromArgb(218, 218, 218);
        private Color m_ActiveColour = Color.FromArgb(35, 146, 33);
        private Color m_TransistionColour = Color.FromArgb(129, 242, 121);

        private Region innerBackgroundRegion;
        private GraphicsPath[] segmentPaths = new GraphicsPath[12];

        private bool m_AutoIncrement = true;
        private double m_IncrementFrequency = 100;
        private bool m_BehindIsActive = true;
        private Int32 m_TransitionSegment = 0;

        private System.Timers.Timer m_AutoRotateTimer = null;

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
            get{
                return m_InactiveColour;
            }
            set {
                m_InactiveColour = value;
                Invalidate();
            }
        }

        Color ActiveSegmentColour
        {
            get{
                return m_ActiveColour;
            }
            set {
                m_ActiveColour = value;
                Invalidate();
            }
        }

        Color TransistionSegmentColour
        {
            get{
                return m_TransistionColour;
            }
            set{
                m_TransistionColour = value;
                Invalidate();
            }
        }

        bool BehindTransistionSegmentIsActive
        {
            get{
                return m_BehindIsActive;
            }
            set{
                m_BehindIsActive = value;
                Invalidate();
            }
        }

        Int32 TransistionSegment
        {
            get{
                return m_TransitionSegment;
            }
            set{
                if(value > 12 || value < -1)
                {
                    throw new ArgumentException("TransistionSegment must be between -1 and 12");
                }
                m_TransitionSegment = value;
                Invalidate();
            }
        }

        bool AutoIncrement
        {
            get{
                return m_AutoIncrement;
            }
            set{
                m_AutoIncrement = value;

                if(value == false && m_AutoRotateTimer != null)
                {
                    m_AutoRotateTimer.Dispose();
                    m_AutoRotateTimer = null;
                }

                if(value == true && m_AutoRotateTimer == null)
                {
                    m_AutoRotateTimer = new System.Timers.Timer(m_IncrementFrequency);

                    m_AutoRotateTimer.Elapsed += this.IncrementTransisionSegment;
                    m_AutoRotateTimer.Start();
                }
            }
        }

        public double AutoIncrementFrequency
        {
            get{
                return m_IncrementFrequency;
            }
            set{
                m_IncrementFrequency = value;

                if(m_AutoRotateTimer != null)
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
            for(int intCount = 0; intCount < 12; intCount++)
            {
                segmentPaths[intCount] = new GraphicsPath();

                //We subtract 90 so that the starting segment is at 12 o'clock
                segmentPaths[intCount].AddPie(rctFull, (intCount * 30) - 90, 25);
            }

            //Create the center circle cut-out
            pthInnerBackground = new GraphicsPath();
            pthInnerBackground.AddPie(rctInner, 0, 360);
            innerBackgroundRegion = new Region(pthInnerBackground);
        }

        private void SpinningProgress_EnabledChanged(object sender, System.EventArgs e)
        {
            if(this.Enabled)
            {
                if(m_AutoRotateTimer != null)
                {
                    m_AutoRotateTimer.Start();
                }
            }
            else {
                if(m_AutoRotateTimer != null)
                {
                    m_AutoRotateTimer.Stop();
                }
            }
        }

        private void IncrementTransisionSegment(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (m_TransitionSegment == 12)
            {
                m_TransitionSegment = 0;
                m_BehindIsActive = !m_BehindIsActive;
            }
            else if (m_TransitionSegment == -1)
            {
                m_TransitionSegment = 0;
            }
            else
            {
                m_TransitionSegment += 1;
            }
            Invalidate();
        }

        private void ProgressDisk_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.ExcludeClip(innerBackgroundRegion);

            for(int intCount = 0; intCount < 12; intCount++)
            {
                if(this.Enabled)
                {
                    if(intCount == m_TransitionSegment)
                    {
                        //If this segment is the transistion segment, colour it differently
                        e.Graphics.FillPath(new SolidBrush(m_TransistionColour), segmentPaths[intCount]);
                    }
                    else if(intCount < m_TransitionSegment)
                    {
                        //This segment is behind the transistion segment
                        if(m_BehindIsActive)
                        {
                            //If behind the transistion should be active, 
                            //colour it with the active colour
                            e.Graphics.FillPath(new SolidBrush(m_ActiveColour), segmentPaths[intCount]);
                        }
                        else{
                            //If behind the transistion should be in-active, 
                            //colour it with the in-active colour
                            e.Graphics.FillPath(new SolidBrush(m_InactiveColour), segmentPaths[intCount]);
                        }
                    }
                    else {
                        //This segment is ahead of the transistion segment
                        if(m_BehindIsActive)
                        {
                            //If behind the the transistion should be active, 
                            //colour it with the in-active colour
                            e.Graphics.FillPath(new SolidBrush(m_InactiveColour), segmentPaths[intCount]);
                        }
                        else {
                            //If behind the the transistion should be in-active, 
                            //colour it with the active colour
                            e.Graphics.FillPath(new SolidBrush(m_ActiveColour), segmentPaths[intCount]);
                        }
                    }
                }
                else {
                    //Draw all segments in in-active colour if not enabled
                    e.Graphics.FillPath(new SolidBrush(m_InactiveColour), segmentPaths[intCount]);
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
