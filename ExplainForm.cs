using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    public class ExplainForm : Form
    {
        private PictureBox chartBox;
        private RichTextBox txtExplanation;
        private DataGridView importanceGrid;

        public ExplainForm(string jsonResult)
        {
            InitUI(jsonResult);
        }

        private void InitUI(string jsonResult)
        {
            this.Text          = "Model Interpretability — Experiment-Predictium";
            this.Size          = new Size(1000, 680);
            this.MinimumSize   = new Size(800, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor     = Color.White;

            try
            {
                using var doc  = JsonDocument.Parse(jsonResult);
                var root = doc.RootElement;

                string model      = root.GetProperty("model").GetString();
                string target     = root.GetProperty("target").GetString();
                string method     = root.GetProperty("method").GetString();
                string explain    = root.GetProperty("explanation").GetString();
                string chartPath  = root.GetProperty("chart_path").GetString();

                // ── Title label ───────────────────────────────────
                var lblTitle = new Label()
                {
                    Text      = $"Interpretability — {model} | Target: {target}",
                    Left = 12, Top = 8, Width = 960, Height = 22,
                    Font      = new Font("Calibri", 10.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(31, 92, 153),
                    Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var lblMethod = new Label()
                {
                    Text      = "Method: " + method,
                    Left = 12, Top = 30, Width = 960, Height = 16,
                    Font      = new Font("Calibri", 8.5f, FontStyle.Italic),
                    ForeColor = Color.FromArgb(120, 120, 120),
                    Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                // ── Chart (left side) ─────────────────────────────
                chartBox = new PictureBox()
                {
                    Left      = 12, Top = 52,
                    Width     = 620, Height = 420,
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.FromArgb(250, 251, 253),
                    Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
                };

                if (File.Exists(chartPath))
                    chartBox.Image = Image.FromFile(chartPath);

                // ── Importance table (right side) ─────────────────
                importanceGrid = new DataGridView()
                {
                    Left  = 642, Top = 52,
                    Width = 340, Height = 200,
                    ReadOnly = true,
                    AllowUserToAddRows    = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible     = false,
                    EnableHeadersVisualStyles = false,
                    BorderStyle   = BorderStyle.FixedSingle,
                    BackgroundColor = Color.White,
                    GridColor     = Color.FromArgb(220, 220, 220),
                    Anchor        = AnchorStyles.Top | AnchorStyles.Right,
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

                importanceGrid.Columns.Add(new DataGridViewTextBoxColumn
                    { HeaderText = "Feature", Width = 180, SortMode = DataGridViewColumnSortMode.NotSortable });
                importanceGrid.Columns.Add(new DataGridViewTextBoxColumn
                    { HeaderText = "Importance", Width = 100, SortMode = DataGridViewColumnSortMode.NotSortable });
                importanceGrid.Columns.Add(new DataGridViewTextBoxColumn
                    { HeaderText = "Bar", Width = 56, SortMode = DataGridViewColumnSortMode.NotSortable });

                if (root.TryGetProperty("importance", out var impArr))
                {
                    int rowIdx = 0;
                    foreach (var item in impArr.EnumerateArray())
                    {
                        string name = item.GetProperty("name").GetString();
                        double val  = item.GetProperty("value").GetDouble();
                        int    bars = (int)(val * 20);
                        string bar  = new string('█', bars) + new string('░', 20 - bars);
                        importanceGrid.Rows.Add(name, val.ToString("F4"), bar);

                        // Top feature highlight
                        if (rowIdx == 0)
                        {
                            importanceGrid.Rows[rowIdx].DefaultCellStyle.BackColor
                                = Color.FromArgb(210, 245, 210);
                            importanceGrid.Rows[rowIdx].DefaultCellStyle.ForeColor
                                = Color.FromArgb(10, 80, 10);
                        }
                        rowIdx++;
                    }
                }

                // ── Scientific explanation ────────────────────────
                var lblExp = new Label()
                {
                    Text      = "Scientific Interpretation:",
                    Left = 642, Top = 260, Width = 340, Height = 18,
                    Font      = new Font("Calibri", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(31, 92, 153),
                    Anchor    = AnchorStyles.Top | AnchorStyles.Right
                };

                txtExplanation = new RichTextBox()
                {
                    Left      = 642, Top = 280,
                    Width     = 340, Height = 192,
                    ReadOnly  = true,
                    BackColor = Color.FromArgb(245, 248, 253),
                    BorderStyle = BorderStyle.FixedSingle,
                    Font      = new Font("Calibri", 9f),
                    ForeColor = Color.FromArgb(30, 30, 30),
                    Text      = explain,
                    Anchor    = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
                };

                // ── Buttons ───────────────────────────────────────
                var btnClose = new Button()
                {
                    Text      = "Close",
                    Left      = this.ClientSize.Width - 100,
                    Top       = this.ClientSize.Height - 36,
                    Width     = 80, Height = 26,
                    Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(31, 92, 153),
                    ForeColor = Color.White,
                    Font      = new Font("Calibri", 9f, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => this.Close();

                // Save chart PNG button
                var btnSaveChart = new Button()
                {
                    Text      = "💾  Save Chart",
                    Left      = 12,
                    Top       = this.ClientSize.Height - 36,
                    Width     = 120, Height = 26,
                    Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(34, 139, 34),
                    ForeColor = Color.White,
                    Font      = new Font("Calibri", 9f, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                btnSaveChart.FlatAppearance.BorderSize = 0;
                btnSaveChart.Click += (s, e) =>
                {
                    if (chartBox.Image == null) return;
                    using var sfd = new SaveFileDialog();
                    sfd.Filter   = "PNG Image (*.png)|*.png";
                    sfd.FileName = $"feature_importance_{model}.png";
                    if (sfd.ShowDialog() == DialogResult.OK)
                        chartBox.Image.Save(sfd.FileName);
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblMethod);
                this.Controls.Add(chartBox);
                this.Controls.Add(importanceGrid);
                this.Controls.Add(lblExp);
                this.Controls.Add(txtExplanation);
                this.Controls.Add(btnSaveChart);
                this.Controls.Add(btnClose);

                // Resize handler
                this.Resize += (s, e) =>
                {
                    if (chartBox == null) return;
                    btnClose.Left      = this.ClientSize.Width - 100;
                    btnClose.Top       = btnSaveChart.Top = this.ClientSize.Height - 36;
                    chartBox.Height    = this.ClientSize.Height - 150;
                    txtExplanation.Height = this.ClientSize.Height - 490;
                    importanceGrid.Left = txtExplanation.Left = lblExp.Left =
                        this.ClientSize.Width - 350;
                };
            }
            catch (Exception ex)
            {
                var lbl = new Label()
                {
                    Text = "Parse error: " + ex.Message,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
                };
                this.Controls.Add(lbl);
            }
        }
    }
}
