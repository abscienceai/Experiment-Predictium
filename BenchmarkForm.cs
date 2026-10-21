using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    public class BenchmarkForm : Form
    {
        private DataGridView grid;
        private Label lblInfo;
        private Label lblBest;

        private static readonly string[] ColumnNames =
        {
            "Model", "Holdout RMSE ↓", "Holdout MAE ↓", "Holdout MAPE % ↓", "Holdout R² ↑",
            "CV RMSE (mean ± SD) ↓", "CV MAE (mean ± SD) ↓", "CV MAPE % (mean ± SD) ↓",
            "CV R² (mean ± SD) ↑", "Time (s)", "Status"
        };

        private static readonly int[] ColumnWidths =
        {
            145, 115, 110, 125, 105, 165, 160, 175, 155, 90, 190
        };

        public BenchmarkForm(string jsonResult, string targetName, int sampleCount)
        {
            InitUI(jsonResult, targetName, sampleCount);
        }

        public BenchmarkForm(string csvFilePath)
        {
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
                    else if (line.StartsWith("# Best model label:"))
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

            InitFromCSV(rows, targetName, sampleCount, bestLine, Path.GetFileName(csvFilePath));
        }

        private void ConfigureWindow(string title)
        {
            Text = title;
            Size = new Size(1460, 500);
            MinimumSize = new Size(1050, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Calibri", 9.5f);
        }

        private DataGridView CreateGrid()
        {
            var g = new DataGridView()
            {
                Left = 16,
                Top = 62,
                Width = ClientSize.Width - 32,
                Height = ClientSize.Height - 108,
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
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(31, 92, 153),
                    ForeColor = Color.White,
                    Font = new Font("Calibri", 9f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.True
                },
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 44,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Calibri", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(247, 249, 252)
                },
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };

            for (int i = 0; i < ColumnNames.Length; i++)
            {
                g.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = ColumnNames[i],
                    Width = ColumnWidths[i],
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }
            return g;
        }

        private Label CreateInfoLabel(string text)
        {
            return new Label()
            {
                Text = text,
                Left = 16,
                Top = 10,
                Width = ClientSize.Width - 32,
                Height = 20,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Calibri", 8.5f, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
        }

        private Label CreateBestLabel(string text = "")
        {
            return new Label()
            {
                Text = text,
                Left = 16,
                Top = 33,
                Width = ClientSize.Width - 32,
                Height = 24,
                ForeColor = Color.FromArgb(20, 120, 20),
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
        }

        private Button CreateCloseButton()
        {
            var b = new Button()
            {
                Text = "Close",
                Left = ClientSize.Width - 110,
                Top = ClientSize.Height - 36,
                Width = 90,
                Height = 26,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 92, 153),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => Close();
            return b;
        }

        private void HighlightBestRow(int bestRowIdx)
        {
            if (bestRowIdx < 0) return;
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex == bestRowIdx)
                {
                    e.CellStyle.BackColor = Color.FromArgb(210, 245, 210);
                    e.CellStyle.ForeColor = Color.FromArgb(10, 80, 10);
                    e.CellStyle.Font = new Font("Calibri", 9f, FontStyle.Bold);
                }
            };
            grid.Rows[bestRowIdx].Cells[0].Value = "🏆 " + grid.Rows[bestRowIdx].Cells[0].Value;
        }

        private void InitFromCSV(List<string[]> rows, string targetName,
                                 int sampleCount, string bestLine, string fileName)
        {
            ConfigureWindow("Benchmark Results — " + fileName);
            lblInfo = CreateInfoLabel(
                "Target: " + targetName + "   |   Samples: " + sampleCount +
                "   |   Holdout: 80/20, seed 42   |   Cross-validation: 5-fold   |   File: " + fileName);
            lblBest = CreateBestLabel(string.IsNullOrWhiteSpace(bestLine) ? "" : "🏆  " + bestLine);
            grid = CreateGrid();

            double bestCvR2 = double.MinValue;
            int bestRowIdx = -1;

            foreach (var r in rows)
            {
                // VM benchmark CSV has 11 columns. Old v2.0 CSV had 6 columns.
                if (r.Length >= 11)
                {
                    grid.Rows.Add(r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9], r[10]);
                    int idx = grid.Rows.Count - 1;
                    string cvR2Text = r[8].Split('±')[0].Trim();
                    if (double.TryParse(cvR2Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double cvR2)
                        && cvR2 > bestCvR2)
                    {
                        bestCvR2 = cvR2;
                        bestRowIdx = idx;
                    }
                }
                else if (r.Length >= 6)
                {
                    // Backward-compatible display for v2.0 files.
                    grid.Rows.Add(r[0], r[1], r[2], "—", r[3], "—", "—", "—", "—", r[4], r[5]);
                }
            }

            HighlightBestRow(bestRowIdx);
            Controls.Add(lblInfo);
            Controls.Add(lblBest);
            Controls.Add(grid);
            Controls.Add(CreateCloseButton());
        }

        private void InitUI(string jsonResult, string targetName, int sampleCount)
        {
            ConfigureWindow("Benchmark Results — Experiment-Predictium v2.2.0-VM");
            lblInfo = CreateInfoLabel(
                "Target: " + targetName + "   |   Samples: " + sampleCount +
                "   |   Holdout: 80% train / 20% test, seed 42   |   Cross-validation: 5-fold");
            lblBest = CreateBestLabel();
            grid = CreateGrid();

            double bestCvR2 = double.MinValue;
            int bestRowIdx = -1;
            List<BenchmarkResult> list = null;

            try
            {
                list = JsonSerializer.Deserialize<List<BenchmarkResult>>(jsonResult);
                if (list == null) return;

                foreach (var r in list)
                {
                    bool hasError = !string.IsNullOrWhiteSpace(r.error);
                    string status = hasError ? "⚠ " + r.error.Split('\n')[0] : "✔ OK";

                    grid.Rows.Add(
                        r.model,
                        hasError ? "—" : r.rmse.ToString("F5", CultureInfo.InvariantCulture),
                        hasError ? "—" : r.mae.ToString("F5", CultureInfo.InvariantCulture),
                        hasError ? "—" : r.mape.ToString("F2", CultureInfo.InvariantCulture),
                        hasError ? "—" : r.r2.ToString("F4", CultureInfo.InvariantCulture),
                        hasError ? "—" : FormatMeanSd(r.cv_rmse_mean, r.cv_rmse_std, 5),
                        hasError ? "—" : FormatMeanSd(r.cv_mae_mean, r.cv_mae_std, 5),
                        hasError ? "—" : FormatMeanSd(r.cv_mape_mean, r.cv_mape_std, 2),
                        hasError ? "—" : FormatMeanSd(r.cv_r2_mean, r.cv_r2_std, 4),
                        r.time.ToString("F1", CultureInfo.InvariantCulture),
                        status
                    );

                    int rowIdx = grid.Rows.Count - 1;
                    if (hasError)
                    {
                        grid.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(160, 40, 40);
                        grid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 245);
                    }
                    else if (r.cv_r2_mean > bestCvR2)
                    {
                        bestCvR2 = r.cv_r2_mean;
                        bestRowIdx = rowIdx;
                    }
                }

                if (bestRowIdx >= 0)
                {
                    var best = list[bestRowIdx];
                    lblBest.Text = "🏆  Best by mean 5-fold CV R²: " + best.model +
                        "   CV R² = " + FormatMeanSd(best.cv_r2_mean, best.cv_r2_std, 4) +
                        "   |   Holdout R² = " + best.r2.ToString("F4", CultureInfo.InvariantCulture) +
                        "   |   Holdout RMSE = " + best.rmse.ToString("F5", CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                lblBest.Text = "Parse error: " + ex.Message;
            }

            HighlightBestRow(bestRowIdx);

            var btnSave = new Button()
            {
                Text = "💾  Save Results",
                Left = 16,
                Top = ClientSize.Height - 36,
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
            btnSave.Click += (s, e) => SaveResults(targetName, sampleCount);

            Controls.Add(lblInfo);
            Controls.Add(lblBest);
            Controls.Add(grid);
            Controls.Add(btnSave);
            Controls.Add(CreateCloseButton());
        }

        private static string FormatMeanSd(double mean, double sd, int decimals)
        {
            string f = "F" + decimals;
            return mean.ToString(f, CultureInfo.InvariantCulture) + " ± " +
                   sd.ToString(f, CultureInfo.InvariantCulture);
        }

        private void SaveResults(string targetName, int sampleCount)
        {
            using var sfd = new SaveFileDialog();
            sfd.Title = "Save Benchmark Results";
            sfd.Filter = "CSV File (*.csv)|*.csv|Text File (*.txt)|*.txt";
            sfd.FileName = "benchmark_" + targetName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
            if (sfd.ShowDialog() != DialogResult.OK) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Experiment-Predictium v2.2.0-VM — Benchmark Results");
            sb.AppendLine("# Target: " + targetName);
            sb.AppendLine("# Samples: " + sampleCount + " | Holdout: 80/20 | Seed: 42 | Cross-validation: 5-fold");
            sb.AppendLine("# MAPE note: percentage metric is epsilon-protected for zero/near-zero targets.");
            sb.AppendLine("# Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("# Best model label: " + lblBest.Text.Replace("🏆", "").Trim());
            sb.AppendLine();
            sb.AppendLine("Model,Holdout_RMSE,Holdout_MAE,Holdout_MAPE_pct,Holdout_R2,CV_RMSE_mean_sd,CV_MAE_mean_sd,CV_MAPE_pct_mean_sd,CV_R2_mean_sd,Time_s,Status");

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var cells = new string[row.Cells.Count];
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    string val = row.Cells[i].Value?.ToString() ?? "";
                    val = val.Replace("🏆 ", "").Replace(",", ";");
                    cells[i] = val;
                }
                sb.AppendLine(string.Join(",", cells));
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            MessageBox.Show("Results saved to: " + Path.GetFileName(sfd.FileName),
                "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private class BenchmarkResult
        {
            public string model { get; set; }
            public double rmse { get; set; }
            public double mae { get; set; }
            public double mape { get; set; }
            public double r2 { get; set; }
            public double cv_rmse_mean { get; set; }
            public double cv_rmse_std { get; set; }
            public double cv_mae_mean { get; set; }
            public double cv_mae_std { get; set; }
            public double cv_mape_mean { get; set; }
            public double cv_mape_std { get; set; }
            public double cv_r2_mean { get; set; }
            public double cv_r2_std { get; set; }
            public double time { get; set; }
            public string error { get; set; }
        }
    }
}
