using System;
using System.Drawing;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    /// <summary>
    /// Horizontal step indicator showing current wizard position.
    /// Steps: 1-Load  2-Columns  3-Quality  4-Model & Predict
    /// </summary>
    public class WizardStepPanel : Panel
    {
        private int _currentStep = 1;
        private readonly string[] _labels =
        {
            "① Load Data",
            "② Select Columns",
            "③ Data Quality",
            "④ Model & Predict"
        };

        public int CurrentStep
        {
            get => _currentStep;
            set { _currentStep = Math.Clamp(value, 1, _labels.Length); Invalidate(); }
        }

        public WizardStepPanel()
        {
            this.Height          = 36;
            this.BackColor       = Color.FromArgb(245, 247, 252);
            this.BorderStyle     = BorderStyle.None;
            this.DoubleBuffered  = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g      = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int n       = _labels.Length;
            int stepW   = this.Width / n;
            int h       = this.Height;

            for (int i = 0; i < n; i++)
            {
                int   step    = i + 1;
                bool  done    = step < _currentStep;
                bool  active  = step == _currentStep;
                float x       = i * stepW;
                float w       = stepW;

                // Background
                Color bg = done    ? Color.FromArgb(210, 245, 210) :
                           active  ? Color.FromArgb(31, 92, 153) :
                                     Color.FromArgb(235, 238, 245);
                using var brush = new SolidBrush(bg);
                g.FillRectangle(brush, x, 0, w, h);

                // Separator
                if (i < n - 1)
                {
                    using var pen = new Pen(Color.FromArgb(200, 210, 225), 1f);
                    g.DrawLine(pen, x + w, 4, x + w, h - 4);
                }

                // Text
                Color fg = active ? Color.White :
                           done   ? Color.FromArgb(20, 100, 20) :
                                    Color.FromArgb(120, 130, 145);
                var  font = new Font("Calibri", 8.8f,
                    active || done ? FontStyle.Bold : FontStyle.Regular);

                string label = done ? "✔ " + _labels[i].Substring(2) : _labels[i];
                var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(label, font, new SolidBrush(fg),
                    new RectangleF(x, 0, w, h), sf);
            }
        }
    }
}
