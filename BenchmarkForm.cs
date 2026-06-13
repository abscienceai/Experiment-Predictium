using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    public class BenchmarkForm : Form
    {
        private DataGridView grid;
        private Label lblInfo;
        private Label lblBest;

        public BenchmarkForm(string jsonResult, string targetName, int sampleCount)
        {
            InitUI(jsonResult, targetName, sampleCount);
        }

        // Constructor for viewing a previously saved benchmark CSV
        public BenchmarkForm(string csvFilePath)
        {
            // Parse CSV file and reconstruct display
            string targetName = "—";
            int sampleCount = 0;
            var rows = new List<string[]>();
            string bestLine = "";

            try
            {
                foreach (string line in File.ReadAllLines(csvFilePath))
                {
                    if (line.StartsWith("# Target:"))
                        targetName = line.Replace("# Target:", "").Trim();
                    else if (line.StartsWith("# Samples:"))
                    {
                        var part = line.Replace("# Samples:", "").Split('|')[0].Trim();
                        int.TryParse(part, out sampleCount);
                    }
                    else if (line.StartsWith("# Best model"))
                        bestLine = line.Replace("# Best model label:", "").Trim();
                    else if (line.StartsWith("#") || line.StartsWith("Model,") ||
                             string.IsNullOrWhiteSpace(line))
                        continue;
                    else
                        rows.Add(line.Split(','));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not read file: " + ex.Message);
                return;
            }

            InitFromCSV(rows, targetName, sampleCount, bestLine,
                        Path.GetFileName(csvFilePath));
        }

        // Build UI directly from parsed CSV rows
        private void InitFromCSV(List<string[]> rows, string targetName,
                                  int sampleCount, string bestLine, string fileName)
        {
            this.Text = "Benchmark Results — " + fileName;
            this.Size = new Size(820, 420);
            this.MinimumSize = new Size(700, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Font = new Font("Calibri", 9.5f);

            lblInfo = new Label()
            {
                Text = "Target: " + targetName + "   |   Samples: " + sampleCount +
                             "   |   Split: 80% train / 20% test   |   File: " + fileName,
                Left = 16,
                Top = 10,
                Width = 780,
                Height = 20,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Calibri", 8.5f, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblBest = new Label()
            {
                Text = string.IsNullOrWhiteSpace(bestLine) ? "" : "🏆  " + bestLine,
                Left = 16,
                Top = 32,
                Width = 780,
                Height = 22,
                ForeColor = Color.FromArgb(20, 120, 20),
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grid = new DataGridView()
            {
                Left = 16,
                Top = 58,
                Width = this.ClientSize.Width - 32,
                Height = this.ClientSize.Height - 100,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(220, 220, 220),
                BackgroundColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                                AnchorStyles.Left | AnchorStyles.Right,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(31, 92, 153),
                    ForeColor = Color.White,
                    Font = new Font("Calibri", 9.5f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                ColumnHeadersHeightSizeMode =
                    DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Calibri", 9.5f),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(247, 249, 252)
                }
            };

            string[] colNames = { "Model", "RMSE ↓", "MAE ↓", "R² ↑", "Time (s)", "Status" };
            int[] colWidths = { 110, 130, 130, 110, 90, 155 };
            for (int i = 0; i < colNames.Length; i++)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = colNames[i],
                    Width = colWidths[i],
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            // Find best R² to highlight
            double bestR2 = double.MinValue;
            int bestRowIdx = -1;

            foreach (var r in rows)
            {
                if (r.Length < 6) continue;
                grid.Rows.Add(r[0], r[1], r[2], r[3], r[4], r[5]);
                int idx = grid.Rows.Count - 1;
                if (double.TryParse(r[3], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double rv)
                    && rv > bestR2)
                { bestR2 = rv; bestRowIdx = idx; }
            }

            if (bestRowIdx >= 0)
            {
                grid.CellFormatting += (s, e) =>
                {
                    if (e.RowIndex == bestRowIdx)
                    {
                        e.CellStyle.BackColor = Color.FromArgb(210, 245, 210);
                        e.CellStyle.ForeColor = Color.FromArgb(10, 80, 10);
                        e.CellStyle.Font = new Font("Calibri", 9.5f, FontStyle.Bold);
                    }
                };
                grid.Rows[bestRowIdx].Cells[0].Value =
                    "🏆 " + grid.Rows[bestRowIdx].Cells[0].Value;
            }

            var btnClose = new Button()
            {
                Text = "Close",
                Left = this.ClientSize.Width - 110,
                Top = this.ClientSize.Height - 36,
                Width = 90,
                Height = 26,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 92, 153),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblInfo);
            this.Controls.Add(lblBest);
            this.Controls.Add(grid);
            this.Controls.Add(btnClose);
        }

        private void InitUI(string jsonResult, string targetName, int sampleCount)
        {
            this.Text = "Benchmark Results — Experiment-Predictium";
            this.Size = new Size(820, 420);
            this.MinimumSize = new Size(700, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Font = new Font("Calibri", 9.5f);

            // ── Header info label ─────────────────────────────────
            lblInfo = new Label()
            {
                Text = "Target: " + targetName + "   |   Samples: " + sampleCount +
                             "   |   Split: 80% train / 20% test   |   Seed: 42",
                Left = 16,
                Top = 10,
                Width = 780,
                Height = 20,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Calibri", 8.5f, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // ── Best model label ──────────────────────────────────
            lblBest = new Label()
            {
                Text = "",
                Left = 16,
                Top = 32,
                Width = 780,
                Height = 22,
                ForeColor = Color.FromArgb(20, 120, 20),
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // ── Results DataGridView ──────────────────────────────
            grid = new DataGridView()
            {
                Left = 16,
                Top = 58,
                Width = this.ClientSize.Width - 32,
                Height = this.ClientSize.Height - 100,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(220, 220, 220),
                BackgroundColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                               AnchorStyles.Left | AnchorStyles.Right,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(31, 92, 153),
                    ForeColor = Color.White,
                    Font = new Font("Calibri", 9.5f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                ColumnHeadersHeightSizeMode =
                    DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Calibri", 9.5f),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };

            // Define columns
            string[] colNames = { "Model", "RMSE ↓", "MAE ↓", "R² ↑", "Time (s)", "Status" };
            int[] colWidths = { 110, 130, 130, 110, 90, 155 };

            for (int i = 0; i < colNames.Length; i++)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = colNames[i],
                    Width = colWidths[i],
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                grid.Columns.Add(col);
            }

            // ── Parse JSON and fill rows ──────────────────────────
            double bestR2 = double.MinValue;
            int bestRowIdx = -1;

            try
            {
                var list = JsonSerializer.Deserialize<List<BenchmarkResult>>(jsonResult);
                if (list == null) return;

                foreach (var r in list)
                {
                    bool hasError = !string.IsNullOrWhiteSpace(r.error);
                    string status = hasError ? "⚠ " + r.error.Split('\n')[0] : "✔ OK";

                    grid.Rows.Add(
                        r.model,
                        hasError ? "—" : r.rmse.ToString("F5"),
                        hasError ? "—" : r.mae.ToString("F5"),
                        hasError ? "—" : r.r2.ToString("F4"),
                        r.time.ToString("F1") + " s",
                        status
                    );

                    int rowIdx = grid.Rows.Count - 1;

                    // Style error rows
                    if (hasError)
                    {
                        grid.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(160, 40, 40);
                        grid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 245);
                    }

                    // Track best R²
                    if (!hasError && r.r2 > bestR2)
                    {
                        bestR2 = r.r2;
                        bestRowIdx = rowIdx;
                    }
                }

                // Highlight best model row
                if (bestRowIdx >= 0)
                {
                    var bestStyle = grid.Rows[bestRowIdx].DefaultCellStyle;
                    bestStyle.BackColor = Color.FromArgb(210, 245, 210);
                    bestStyle.ForeColor = Color.FromArgb(10, 80, 10);
                    bestStyle.Font = new Font("Calibri", 9.5f, FontStyle.Bold);

                    // Add trophy to model name
                    grid.Rows[bestRowIdx].Cells[0].Value =
                        "🏆 " + grid.Rows[bestRowIdx].Cells[0].Value;

                    lblBest.Text = "🏆  Best model: " +
                        list[bestRowIdx].model +
                        "   R² = " + bestR2.ToString("F4") +
                        "   RMSE = " + list[bestRowIdx].rmse.ToString("F5") +
                        "   MAE = " + list[bestRowIdx].mae.ToString("F5");
                }
            }
            catch (Exception ex)
            {
                lblBest.Text = "Parse error: " + ex.Message;
            }

            // ── Alternating row colours ───────────────────────────
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(247, 249, 252)
            };

            // Re-apply best row highlight (alternating rows override it otherwise)
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex == bestRowIdx)
                {
                    e.CellStyle.BackColor = Color.FromArgb(210, 245, 210);
                    e.CellStyle.ForeColor = Color.FromArgb(10, 80, 10);
                    e.CellStyle.Font = new Font("Calibri", 9.5f, FontStyle.Bold);
                }
            };

            // ── Save Results button ───────────────────────────────
            var btnSave = new Button()
            {
                Text = "💾  Save Results",
                Left = 16,
                Top = this.ClientSize.Height - 36,
                Width = 140,
                Height = 26,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 139, 34),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 110, 20);
            btnSave.Click += (s, e) => SaveResults(targetName, sampleCount);

            // ── Close button ──────────────────────────────────────
            var btnClose = new Button()
            {
                Text = "Close",
                Left = this.ClientSize.Width - 110,
                Top = this.ClientSize.Height - 36,
                Width = 90,
                Height = 26,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 92, 153),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblInfo);
            this.Controls.Add(lblBest);
            this.Controls.Add(grid);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnClose);
        }

        // Save benchmark results as CSV
        private void SaveResults(string targetName, int sampleCount)
        {
            using var sfd = new System.Windows.Forms.SaveFileDialog();
            sfd.Title = "Save Benchmark Results";
            sfd.Filter = "CSV File (*.csv)|*.csv|Text File (*.txt)|*.txt";
            sfd.FileName = "benchmark_" + targetName + "_" +
                           DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";

            if (sfd.ShowDialog() != DialogResult.OK) return;

            var sb = new System.Text.StringBuilder();

            // Header info
            sb.AppendLine("# Experiment-Predictium — Benchmark Results");
            sb.AppendLine("# Target: " + targetName);
            sb.AppendLine("# Samples: " + sampleCount + " | Split: 80/20 | Seed: 42");
            sb.AppendLine("# Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("# Best model label: " + lblBest.Text.Replace("#", "").Trim());
            sb.AppendLine();

            // Column headers
            sb.AppendLine("Model,RMSE,MAE,R2,Time_s,Status");

            // Data rows
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var cells = new string[row.Cells.Count];
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    // Remove trophy emoji for clean CSV
                    string val = row.Cells[i].Value?.ToString() ?? "";
                    val = val.Replace("🏆 ", "").Replace(",", ";");
                    cells[i] = val;
                }
                sb.AppendLine(string.Join(",", cells));
            }

            System.IO.File.WriteAllText(sfd.FileName, sb.ToString(),
                System.Text.Encoding.UTF8);

            MessageBox.Show("Results saved to: " + System.IO.Path.GetFileName(sfd.FileName),
                "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // JSON deserialization helper
        private class BenchmarkResult
        {
            public string model { get; set; }
            public double rmse { get; set; }
            public double mae { get; set; }
            public double r2 { get; set; }
            public double time { get; set; }
            public string error { get; set; }
        }
    }
}