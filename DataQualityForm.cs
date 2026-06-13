using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    public class DataQualityForm : Form
    {
        // Callback: called when user applies a missing-value strategy
        // Returns the cleaned data as list of rows
        public List<List<string>> CleanedData { get; private set; }
        public bool Applied { get; private set; } = false;

        private DataGridView statsGrid;
        private Label lblSummary;
        private ComboBox cmbStrategy;
        private Button btnApply;
        private readonly List<ColumnStats> stats;
        private readonly List<List<string>> rawData;
        private readonly List<string> headers;

        public DataQualityForm(List<List<string>> data, List<string> columnHeaders)
        {
            rawData = data;
            headers = columnHeaders;
            stats   = ComputeStats(data, columnHeaders);
            InitUI();
        }

        // ── Compute statistics for each column ───────────────────
        private static List<ColumnStats> ComputeStats(
            List<List<string>> data, List<string> headers)
        {
            int cols = data.Count > 0 ? data[0].Count : 0;
            var result = new List<ColumnStats>();
            var culture = CultureInfo.InvariantCulture;

            for (int c = 0; c < cols; c++)
            {
                var cs   = new ColumnStats();
                cs.Name  = c < headers.Count ? headers[c] : $"Col {c + 1}";
                cs.Total = data.Count;

                var nums = new List<double>();
                int missing = 0;

                foreach (var row in data)
                {
                    string val = c < row.Count ? row[c]?.Trim() ?? "" : "";
                    if (string.IsNullOrEmpty(val) || val == "NaN" || val == "—")
                        missing++;
                    else if (double.TryParse(val, NumberStyles.Any, culture, out double d))
                        nums.Add(d);
                    else
                        missing++;
                }

                cs.Missing = missing;
                if (nums.Count > 0)
                {
                    nums.Sort();
                    cs.Min    = nums[0];
                    cs.Max    = nums[nums.Count - 1];
                    cs.Mean   = nums.Average();
                    cs.Median = nums[nums.Count / 2];
                    double variance = nums.Average(v => Math.Pow(v - cs.Mean, 2));
                    cs.StdDev = Math.Sqrt(variance);
                }
                result.Add(cs);
            }
            return result;
        }

        private void InitUI()
        {
            this.Text          = "Data Quality Panel — Experiment-Predictium";
            this.Size          = new Size(1000, 520);
            this.MinimumSize   = new Size(800, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor     = Color.White;
            this.Font          = new Font("Calibri", 9.5f);

            // ── Summary label ─────────────────────────────────────
            int totalMissing = stats.Sum(s => s.Missing);
            int totalCells   = stats.Sum(s => s.Total);
            double missPct   = totalCells > 0
                ? (double)totalMissing / totalCells * 100 : 0;

            string quality = missPct == 0 ? "✔ No missing values"
                           : missPct < 5  ? $"⚠ {missPct:F1}% missing (low)"
                           : missPct < 20 ? $"⚠ {missPct:F1}% missing (moderate)"
                           : $"✖ {missPct:F1}% missing (high)";

            lblSummary = new Label()
            {
                Text      = $"Dataset: {rawData.Count} rows × {stats.Count} columns   |   " +
                             $"Total cells: {totalCells}   |   {quality}",
                Left      = 16, Top = 10, Width = 960, Height = 20,
                ForeColor = missPct == 0 ? Color.FromArgb(20,120,20) :
                            missPct < 5  ? Color.FromArgb(140,80,0) :
                                           Color.FromArgb(160,30,30),
                Font      = new Font("Calibri", 9f, FontStyle.Bold),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // ── Stats DataGridView ────────────────────────────────
            statsGrid = new DataGridView()
            {
                Left      = 16, Top = 36,
                Width     = this.ClientSize.Width - 32,
                Height    = this.ClientSize.Height - 110,
                ReadOnly  = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible     = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                BorderStyle   = BorderStyle.FixedSingle,
                GridColor     = Color.FromArgb(220, 220, 220),
                BackgroundColor = Color.White,
                Anchor        = AnchorStyles.Top | AnchorStyles.Bottom |
                                AnchorStyles.Left | AnchorStyles.Right,
                ColumnHeadersHeight = 32,
                ColumnHeadersHeightSizeMode =
                    DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(31, 92, 153),
                    ForeColor = Color.White,
                    Font      = new Font("Calibri", 9f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font      = new Font("Calibri", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(247, 249, 252)
                }
            };

            // Columns
            string[] cols  = { "Column", "Count", "Missing", "Missing %",
                                "Min", "Max", "Mean", "Median", "Std Dev", "Quality" };
            int[]    widths = { 160, 65, 70, 85, 90, 90, 90, 90, 90, 100 };
            for (int i = 0; i < cols.Length; i++)
                statsGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = cols[i], Width = widths[i],
                    SortMode   = DataGridViewColumnSortMode.NotSortable
                });

            // Fill rows
            foreach (var s in stats)
            {
                double pct = s.Total > 0 ? (double)s.Missing / s.Total * 100 : 0;
                string qual = pct == 0  ? "✔ Clean"
                            : pct < 5   ? "⚠ Low"
                            : pct < 20  ? "⚠ Moderate"
                            : "✖ High";

                statsGrid.Rows.Add(
                    s.Name,
                    s.Total - s.Missing,
                    s.Missing,
                    pct.ToString("F1") + "%",
                    s.Min.ToString("F4"),
                    s.Max.ToString("F4"),
                    s.Mean.ToString("F4"),
                    s.Median.ToString("F4"),
                    s.StdDev.ToString("F4"),
                    qual
                );

                int ri = statsGrid.Rows.Count - 1;
                // Colour-code quality cell
                var qualCell = statsGrid.Rows[ri].Cells[9];
                if (pct == 0)
                { qualCell.Style.BackColor = Color.FromArgb(210,245,210);
                  qualCell.Style.ForeColor = Color.FromArgb(10,80,10); }
                else if (pct < 5)
                { qualCell.Style.BackColor = Color.FromArgb(255,245,210);
                  qualCell.Style.ForeColor = Color.FromArgb(130,70,0); }
                else if (pct < 20)
                { qualCell.Style.BackColor = Color.FromArgb(255,235,200);
                  qualCell.Style.ForeColor = Color.FromArgb(160,60,0); }
                else
                { qualCell.Style.BackColor = Color.FromArgb(255,220,220);
                  qualCell.Style.ForeColor = Color.FromArgb(160,20,20); }
            }

            // ── Bottom bar: strategy + buttons ────────────────────
            var lblStrategy = new Label()
            {
                Text      = "Missing value strategy:",
                Left      = 16, Top = this.ClientSize.Height - 36,
                Width     = 160, Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Calibri", 9f, FontStyle.Bold),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left
            };

            cmbStrategy = new ComboBox()
            {
                Left          = 182, Top = this.ClientSize.Height - 36,
                Width         = 200, Height = 26,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Calibri", 9f),
                Anchor        = AnchorStyles.Bottom | AnchorStyles.Left
            };
            cmbStrategy.Items.AddRange(new object[]
            {
                "Keep as is (no change)",
                "Drop rows with missing values",
                "Fill missing with column mean",
                "Fill missing with column median"
            });
            cmbStrategy.SelectedIndex = 0;

            btnApply = new Button()
            {
                Text      = "✔  Apply & Continue",
                Left      = 392, Top = this.ClientSize.Height - 36,
                Width     = 155, Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 92, 153),
                ForeColor = Color.White,
                Font      = new Font("Calibri", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += ApplyClicked;

            var btnCancel = new Button()
            {
                Text      = "Cancel",
                Left      = this.ClientSize.Width - 100,
                Top       = this.ClientSize.Height - 36,
                Width     = 80, Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(150,150,150),
                ForeColor = Color.White,
                Font      = new Font("Calibri", 9f),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.Close();

            // Reposition on resize
            this.Resize += (s, e) =>
            {
                statsGrid.Width  = this.ClientSize.Width - 32;
                statsGrid.Height = this.ClientSize.Height - 110;
                lblStrategy.Top  = cmbStrategy.Top = btnApply.Top =
                btnCancel.Top    = this.ClientSize.Height - 36;
                btnCancel.Left   = this.ClientSize.Width - 100;
            };

            this.Controls.Add(lblSummary);
            this.Controls.Add(statsGrid);
            this.Controls.Add(lblStrategy);
            this.Controls.Add(cmbStrategy);
            this.Controls.Add(btnApply);
            this.Controls.Add(btnCancel);
        }

        private void ApplyClicked(object sender, EventArgs e)
        {
            var culture = CultureInfo.InvariantCulture;
            CleanedData = new List<List<string>>();

            switch (cmbStrategy.SelectedIndex)
            {
                case 0: // Keep as is
                    CleanedData = rawData.Select(r => new List<string>(r)).ToList();
                    break;

                case 1: // Drop missing rows
                    foreach (var row in rawData)
                    {
                        bool hasMissing = row.Any(v =>
                            string.IsNullOrWhiteSpace(v) || v == "NaN" || v == "—" ||
                            !double.TryParse(v.Replace(",","."), NumberStyles.Any, culture, out _));
                        if (!hasMissing) CleanedData.Add(new List<string>(row));
                    }
                    break;

                case 2: // Fill with mean
                case 3: // Fill with median
                {
                    // Compute fill values per column
                    var fills = new double[stats.Count];
                    for (int c = 0; c < stats.Count; c++)
                        fills[c] = cmbStrategy.SelectedIndex == 2
                            ? stats[c].Mean : stats[c].Median;

                    foreach (var row in rawData)
                    {
                        var newRow = new List<string>();
                        for (int c = 0; c < row.Count; c++)
                        {
                            string v = row[c]?.Trim() ?? "";
                            bool bad = string.IsNullOrEmpty(v) || v == "NaN" || v == "—" ||
                                !double.TryParse(v.Replace(",","."), NumberStyles.Any, culture, out _);
                            newRow.Add(bad ? fills[c].ToString("G", culture) : v);
                        }
                        CleanedData.Add(newRow);
                    }
                    break;
                }
            }

            int removed = rawData.Count - CleanedData.Count;
            string msg  = "Strategy applied: " + cmbStrategy.SelectedItem + "\n" +
                          "Rows before: " + rawData.Count + "\n" +
                          "Rows after:  " + CleanedData.Count;
            if (removed > 0) msg += "\nRows removed: " + removed;

            MessageBox.Show(msg, "Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Applied = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private class ColumnStats
        {
            public string Name   { get; set; }
            public int    Total  { get; set; }
            public int    Missing{ get; set; }
            public double Min    { get; set; }
            public double Max    { get; set; }
            public double Mean   { get; set; }
            public double Median { get; set; }
            public double StdDev { get; set; }
        }
    }
}
