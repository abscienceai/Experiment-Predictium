using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExperimentPredictorApp
{
    /// <summary>
    /// Multi-step wizard: CSV → column selection → cleaning → export to Excel
    /// </summary>
    public class CsvWizardForm : Form
    {
        // ── State ─────────────────────────────────────────────────
        private string csvPath;
        private List<ColumnInfo> columns = new();
        private int currentStep = 1;

        // ── Controls ──────────────────────────────────────────────
        private Panel stepPanel;          // Current step content
        private Label lblStepTitle;
        private Button btnBack, btnNext, btnFinish;
        private ProgressBar progressBar;
        private Label lblProgress;

        // Step-specific controls
        private Label lblCsvPath;
        private DataGridView previewGrid;
        private CheckedListBox lstFeatures;
        private ListBox lstTarget;
        private List<string> lstTargetActualNames = new(); // actual column names (not display strings)
        private DataGridView boolGrid;
        private ComboBox cmbMissing;
        private DataGridView renameGrid;
        private TextBox txtOutputDir;
        private RichTextBox txtSummary;

        // Collected configuration
        private List<string> featureCols = new();
        private List<string> targetCols = new();
        private List<string> boolCols = new();
        private string missStrategy = "drop";
        private Dictionary<string, string> renameMap = new();
        private string outputDir = "";

        public CsvWizardForm()
        {
            InitShell();
            ShowStep(1);
        }

        // ── Shell layout (wizard chrome) ──────────────────────────
        private void InitShell()
        {
            this.Text = "CSV → Excel Wizard — Experiment-Predictium";
            this.Size = new Size(860, 620);
            this.MinimumSize = new Size(720, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Font = new Font("Calibri", 9.5f);

            // Top title bar
            var titleBar = new Panel()
            {
                Left = 0,
                Top = 0,
                Height = 52,
                Width = this.ClientSize.Width,
                BackColor = Color.FromArgb(31, 92, 153),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            lblStepTitle = new Label()
            {
                Left = 16,
                Top = 8,
                Width = 800,
                Height = 36,
                Font = new Font("Calibri", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            titleBar.Controls.Add(lblStepTitle);

            // Step content panel
            stepPanel = new Panel()
            {
                Left = 0,
                Top = 52,
                Width = this.ClientSize.Width,
                Height = this.ClientSize.Height - 120,
                AutoScroll = true,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                         AnchorStyles.Left | AnchorStyles.Right
            };

            // Progress bar
            progressBar = new ProgressBar()
            {
                Left = 16,
                Top = this.ClientSize.Height - 62,
                Width = this.ClientSize.Width - 32,
                Height = 6,
                Minimum = 1,
                Maximum = 6,
                Value = 1,
                Style = ProgressBarStyle.Continuous,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lblProgress = new Label()
            {
                Left = 16,
                Top = this.ClientSize.Height - 78,
                Width = 300,
                Height = 16,
                Font = new Font("Calibri", 8f, FontStyle.Italic),
                ForeColor = Color.FromArgb(120, 120, 120),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            // Navigation buttons
            btnBack = MakeNavBtn("← Back", Color.FromArgb(120, 120, 120));
            btnNext = MakeNavBtn("Next →", Color.FromArgb(31, 92, 153));
            btnFinish = MakeNavBtn("✔ Export", Color.FromArgb(34, 139, 34));

            btnBack.Left = this.ClientSize.Width - 280;
            btnNext.Left = this.ClientSize.Width - 190;
            btnFinish.Left = this.ClientSize.Width - 100;
            foreach (var b in new[] { btnBack, btnNext, btnFinish })
                b.Top = this.ClientSize.Height - 42;

            btnBack.Anchor = btnNext.Anchor = btnFinish.Anchor =
                AnchorStyles.Bottom | AnchorStyles.Right;

            btnBack.Click += (s, e) => Navigate(-1);
            btnNext.Click += (s, e) => Navigate(+1);
            btnFinish.Click += (s, e) => _ = FinishAsync();

            this.Resize += (s, e) =>
            {
                if (titleBar == null) return;
                titleBar.Width = this.ClientSize.Width;
                stepPanel.Width = this.ClientSize.Width;
                stepPanel.Height = this.ClientSize.Height - 120;
                progressBar.Width = this.ClientSize.Width - 32;
                progressBar.Top = this.ClientSize.Height - 62;
                lblProgress.Top = this.ClientSize.Height - 78;
                btnBack.Left = this.ClientSize.Width - 280;
                btnNext.Left = this.ClientSize.Width - 190;
                btnFinish.Left = this.ClientSize.Width - 100;
                foreach (var b in new[] { btnBack, btnNext, btnFinish })
                    b.Top = this.ClientSize.Height - 42;
            };

            this.Controls.Add(titleBar);
            this.Controls.Add(stepPanel);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblProgress);
            this.Controls.Add(btnBack);
            this.Controls.Add(btnNext);
            this.Controls.Add(btnFinish);
        }

        private Button MakeNavBtn(string text, Color bg)
        {
            var b = new Button()
            {
                Text = text,
                Width = 82,
                Height = 28,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        // ── Step navigation ───────────────────────────────────────
        private void Navigate(int dir)
        {
            if (dir > 0 && !ValidateStep()) return;
            currentStep = Math.Clamp(currentStep + dir, 1, 6);
            ShowStep(currentStep);
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            progressBar.Value = step;
            btnBack.Enabled = step > 1;
            btnNext.Visible = step < 6;
            btnFinish.Visible = step == 6;

            stepPanel.Controls.Clear();

            string[] titles =
            {
                "Step 1 of 6 — Load CSV File",
                "Step 2 of 6 — Select Feature & Target Columns",
                "Step 3 of 6 — Boolean / Categorical Columns",
                "Step 4 of 6 — Missing Value Strategy",
                "Step 5 of 6 — Rename Columns (Optional)",
                "Step 6 of 6 — Preview & Export"
            };
            lblStepTitle.Text = titles[step - 1];
            lblProgress.Text = $"Step {step} of 6";

            switch (step)
            {
                case 1: BuildStep1(); break;
                case 2: BuildStep2(); break;
                case 3: BuildStep3(); break;
                case 4: BuildStep4(); break;
                case 5: BuildStep5(); break;
                case 6: BuildStep6(); break;
            }
        }

        // ── Step 1: Load CSV ──────────────────────────────────────
        private void BuildStep1()
        {
            var lbl = MakeLabel("Select your CSV file. The wizard will inspect column types, " +
                "detect missing values, and guide you through creating Excel files " +
                "ready for Experiment-Predictium.", 16, 12, 800, 36);
            lbl.Font = new Font("Calibri", 9.5f, FontStyle.Italic);

            lblCsvPath = MakeLabel("No file selected.", 16, 60, 600, 22);
            lblCsvPath.ForeColor = Color.FromArgb(120, 120, 120);

            var btnBrowse = new Button()
            {
                Text = "📂  Browse CSV...",
                Left = 16,
                Top = 86,
                Width = 160,
                Height = 28,
                BackColor = Color.FromArgb(31, 92, 153),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += async (s, e) => await LoadCsvAsync();

            previewGrid = MakeGrid(16, 120,
                stepPanel.Width - 32, stepPanel.Height - 130);

            if (!string.IsNullOrEmpty(csvPath))
            {
                lblCsvPath.Text = csvPath;
                lblCsvPath.ForeColor = Color.FromArgb(20, 100, 20);
            }

            stepPanel.Controls.Add(lbl);
            stepPanel.Controls.Add(lblCsvPath);
            stepPanel.Controls.Add(btnBrowse);
            stepPanel.Controls.Add(previewGrid);
        }

        private async Task LoadCsvAsync()
        {
            string csvWizardDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "csv_wizard");
            Directory.CreateDirectory(csvWizardDir);

            using var ofd = new OpenFileDialog();
            ofd.Title = "Select CSV File";
            ofd.InitialDirectory = csvWizardDir;
            ofd.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            csvPath = ofd.FileName;
            if (lblCsvPath != null)
            {
                lblCsvPath.Text = csvPath;
                lblCsvPath.ForeColor = Color.FromArgb(20, 100, 20);
            }

            // Call Python to inspect
            string result = await Task.Run(() =>
                ModelHandler.RunScript("csv_wizard.py",
                    "inspect \"" + csvPath + "\""));

            if (result.StartsWith("Error"))
            { MessageBox.Show(result); return; }

            try
            {
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;

                columns.Clear();
                foreach (var c in root.GetProperty("columns").EnumerateArray())
                    columns.Add(new ColumnInfo
                    {
                        Name = c.GetProperty("name").GetString(),
                        Dtype = c.GetProperty("dtype").GetString(),
                        NMissing = c.GetProperty("n_missing").GetInt32(),
                        MissPct = c.GetProperty("miss_pct").GetDouble(),
                        Sample = c.GetProperty("sample").GetString()
                    });

                // Show preview
                if (previewGrid != null)
                {
                    previewGrid.Columns.Clear();
                    previewGrid.Rows.Clear();
                    foreach (var c in columns)
                        previewGrid.Columns.Add(c.Name, c.Name);

                    foreach (var row in root.GetProperty("preview").EnumerateArray())
                    {
                        var cells = row.EnumerateArray()
                                       .Select(v => (object)v.GetString()).ToArray();
                        previewGrid.Rows.Add(cells);
                    }
                }

                int nRows = root.GetProperty("n_rows").GetInt32();
                if (lblCsvPath != null)
                    lblCsvPath.Text = csvPath +
                        $"   ({nRows:N0} rows, {columns.Count} columns)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Parse error: " + ex.Message);
            }
        }

        // ── Step 2: Select columns ────────────────────────────────
        private void BuildStep2()
        {
            var lbl1 = MakeLabel(
                "Select INPUT features (X) — multiple selection allowed:", 16, 8, 380, 20);
            lbl1.Font = new Font("Calibri", 9.5f, FontStyle.Bold);
            lbl1.ForeColor = Color.FromArgb(31, 92, 153);

            var lbl2 = MakeLabel(
                "Select TARGET variable(s) (y) — one Excel file per target:", 410, 8, 400, 20);
            lbl2.Font = new Font("Calibri", 9.5f, FontStyle.Bold);
            lbl2.ForeColor = Color.FromArgb(34, 139, 34);

            lstFeatures = new CheckedListBox()
            {
                Left = 16,
                Top = 30,
                Width = 380,
                Height = stepPanel.Height - 50,
                Font = new Font("Calibri", 9f),
                CheckOnClick = true
            };
            foreach (var c in columns)
            {
                string tag = c.Dtype == "numeric" ? "" :
                             c.Dtype == "boolean" ? " [bool]" : " [text]";
                string miss = c.MissPct > 0 ? $" ({c.MissPct:F0}% missing)" : "";
                lstFeatures.Items.Add(c.Name + tag + miss,
                    featureCols.Contains(c.Name));
            }

            lstTarget = new ListBox()
            {
                Left = 410,
                Top = 30,
                Width = 400,
                Height = stepPanel.Height - 50,
                Font = new Font("Calibri", 9f),
                SelectionMode = SelectionMode.MultiExtended
            };
            lstTargetActualNames.Clear();
            foreach (var c in columns)
            {
                if (c.Dtype == "numeric")
                {
                    // Use [xx%] suffix to avoid clashing with column names containing "("
                    string miss = c.MissPct > 0 ? $" [{c.MissPct:F0}% missing]" : "";
                    lstTarget.Items.Add(c.Name + miss);
                    lstTargetActualNames.Add(c.Name);  // Store actual name separately
                    if (targetCols.Contains(c.Name))
                        lstTarget.SetSelected(lstTarget.Items.Count - 1, true);
                }
            }

            var note = MakeLabel(
                "Tip: For MOF data, select LCD, PLD, Density, ASA, VF, PV, Has_OMS, " +
                "Avg_Atomic_Mass as features. Select Heat_Capacity, Thermal_Stability etc. as targets.",
                16, stepPanel.Height - 40, 780, 32);
            note.Font = new Font("Calibri", 8.5f, FontStyle.Italic);
            note.ForeColor = Color.FromArgb(100, 100, 100);

            stepPanel.Controls.Add(lbl1);
            stepPanel.Controls.Add(lbl2);
            stepPanel.Controls.Add(lstFeatures);
            stepPanel.Controls.Add(lstTarget);
            stepPanel.Controls.Add(note);
        }

        // ── Step 3: Boolean columns ───────────────────────────────
        private void BuildStep3()
        {
            var lbl = MakeLabel(
                "Tick the columns that contain Yes/No or True/False values — " +
                "they will be converted to 1/0 automatically.", 16, 8, 800, 22);

            boolGrid = MakeGrid(16, 36, stepPanel.Width - 32, stepPanel.Height - 50);
            boolGrid.Columns.Add(new DataGridViewCheckBoxColumn
            { HeaderText = "Convert", Width = 80 });
            boolGrid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Column", Width = 200, ReadOnly = true });
            boolGrid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Type", Width = 80, ReadOnly = true });
            boolGrid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Sample value", Width = 180, ReadOnly = true });
            boolGrid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Missing", Width = 100, ReadOnly = true });

            foreach (var c in columns.Where(c =>
                featureCols.Contains(c.Name) || targetCols.Contains(c.Name)))
            {
                bool isBool = c.Dtype == "boolean";
                int ri = boolGrid.Rows.Add(isBool, c.Name, c.Dtype,
                    c.Sample, c.MissPct > 0 ? c.MissPct + "%" : "✔ Clean");
                if (isBool)
                    boolGrid.Rows[ri].DefaultCellStyle.BackColor =
                        Color.FromArgb(235, 245, 235);
            }

            stepPanel.Controls.Add(lbl);
            stepPanel.Controls.Add(boolGrid);
        }

        // ── Step 4: Missing value strategy ───────────────────────
        private void BuildStep4()
        {
            var lbl = MakeLabel(
                "Choose how to handle rows with missing or 'unknown' values:", 16, 8, 800, 22);

            cmbMissing = new ComboBox()
            {
                Left = 16,
                Top = 36,
                Width = 400,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Calibri", 9.5f)
            };
            cmbMissing.Items.AddRange(new object[]
            {
                "Drop rows with any missing values  (recommended — safest)",
                "Fill missing values with column MEAN",
                "Fill missing values with column MEDIAN"
            });
            cmbMissing.SelectedIndex = missStrategy == "mean" ? 1 :
                                       missStrategy == "median" ? 2 : 0;

            // Stats
            int totalMiss = columns
                .Where(c => featureCols.Contains(c.Name) || targetCols.Contains(c.Name))
                .Sum(c => c.NMissing);

            var lblStats = MakeLabel(
                $"Selected columns have {totalMiss:N0} total missing/unknown values " +
                "across all rows.", 16, 72, 800, 22);
            lblStats.ForeColor = totalMiss == 0
                ? Color.FromArgb(20, 100, 20) : Color.FromArgb(160, 60, 0);
            lblStats.Font = new Font("Calibri", 9.5f, FontStyle.Bold);

            var note = MakeLabel(
                "Note: 'Drop rows' removes entire rows where any selected column is missing. " +
                "'Fill' replaces missing values in place — useful when missing data is rare.",
                16, 106, 800, 40);
            note.Font = new Font("Calibri", 9f, FontStyle.Italic);
            note.ForeColor = Color.FromArgb(100, 100, 100);

            stepPanel.Controls.Add(lbl);
            stepPanel.Controls.Add(cmbMissing);
            stepPanel.Controls.Add(lblStats);
            stepPanel.Controls.Add(note);
        }

        // ── Step 5: Rename columns ────────────────────────────────
        private void BuildStep5()
        {
            var lbl = MakeLabel(
                "Rename columns for cleaner Excel output. Edit the 'New Name' column — " +
                "leave blank to keep original.", 16, 8, 800, 22);

            renameGrid = MakeGrid(16, 36, stepPanel.Width - 32, stepPanel.Height - 50);
            renameGrid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Original Column", Width = 280, ReadOnly = true });
            renameGrid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "New Name (edit here)", Width = 280 });
            renameGrid.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Role", Width = 100, ReadOnly = true });

            var all = featureCols.Select(c => (c, "Feature"))
                .Concat(targetCols.Select(c => (c, "Target")));

            foreach (var (col, role) in all)
            {
                string suggested = renameMap.ContainsKey(col) ? renameMap[col] :
                    col.Replace(" ", "_")
                       .Replace("(", "").Replace(")", "")
                       .Replace("/", "_").Replace("@", "at");
                int ri = renameGrid.Rows.Add(col, suggested, role);
                if (role == "Target")
                    renameGrid.Rows[ri].Cells[2].Style.ForeColor =
                        Color.FromArgb(34, 139, 34);
            }

            stepPanel.Controls.Add(lbl);
            stepPanel.Controls.Add(renameGrid);
        }

        // ── Step 6: Preview & Export ──────────────────────────────
        private void BuildStep6()
        {
            var lbl = MakeLabel(
                "Review your selections below, then click ✔ Export to generate Excel files.",
                16, 8, 800, 22);

            txtSummary = new RichTextBox()
            {
                Left = 16,
                Top = 36,
                Width = stepPanel.Width - 32,
                Height = 220,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 248, 253),
                Font = new Font("Calibri", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };

            txtSummary.AppendText("FEATURES (" + featureCols.Count + " columns):\n");
            foreach (var c in featureCols)
            {
                string renamed = renameMap.ContainsKey(c) ? " → " + renameMap[c] : "";
                txtSummary.AppendText("  • " + c + renamed + "\n");
            }
            txtSummary.AppendText("\nTARGETS (" + targetCols.Count + " — one Excel per target):\n");
            foreach (var c in targetCols)
            {
                string renamed = renameMap.ContainsKey(c) ? " → " + renameMap[c] : "";
                txtSummary.AppendText("  • " + c + renamed + "\n");
            }
            if (boolCols.Count > 0)
            {
                txtSummary.AppendText("\nBOOLEAN CONVERSIONS (Yes/No → 1/0):\n");
                foreach (var c in boolCols)
                    txtSummary.AppendText("  • " + c + "\n");
            }
            txtSummary.AppendText("\nMISSING VALUES: " + missStrategy.ToUpper() + "\n");
            txtSummary.AppendText("OUTPUT FOLDER: " + outputDir + "\n");

            var lblOut = MakeLabel("Output folder:", 16, 268, 120, 22);
            lblOut.Font = new Font("Calibri", 9.5f, FontStyle.Bold);

            txtOutputDir = new TextBox()
            {
                Left = 140,
                Top = 266,
                Width = 540,
                Text = string.IsNullOrEmpty(outputDir)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "csv_wizard")
                    : outputDir,
                Font = new Font("Calibri", 9f)
            };

            var btnBrowseOut = new Button()
            {
                Text = "...",
                Left = 686,
                Top = 266,
                Width = 40,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBrowseOut.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog();
                fbd.SelectedPath = txtOutputDir.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                    txtOutputDir.Text = fbd.SelectedPath;
            };

            stepPanel.Controls.Add(lbl);
            stepPanel.Controls.Add(txtSummary);
            stepPanel.Controls.Add(lblOut);
            stepPanel.Controls.Add(txtOutputDir);
            stepPanel.Controls.Add(btnBrowseOut);
        }

        // ── Validation ────────────────────────────────────────────
        private bool ValidateStep()
        {
            switch (currentStep)
            {
                case 1:
                    if (string.IsNullOrEmpty(csvPath) || columns.Count == 0)
                    { MessageBox.Show("Please load a CSV file first."); return false; }
                    return true;

                case 2:
                    // Collect feature and target selections
                    featureCols.Clear();
                    foreach (int i in lstFeatures.CheckedIndices)
                        featureCols.Add(columns[i].Name);

                    targetCols.Clear();
                    foreach (int i in lstTarget.SelectedIndices)
                    {
                        // Use parallel actual names list — avoids splitting column names
                        if (i < lstTargetActualNames.Count)
                            targetCols.Add(lstTargetActualNames[i]);
                    }

                    if (featureCols.Count == 0)
                    { MessageBox.Show("Please select at least one feature column."); return false; }
                    if (targetCols.Count == 0)
                    { MessageBox.Show("Please select at least one target column."); return false; }
                    return true;

                case 3:
                    boolCols.Clear();
                    foreach (DataGridViewRow row in boolGrid.Rows)
                        if (row.IsNewRow) continue;
                        else if (row.Cells[0].Value is true)
                            boolCols.Add(row.Cells[1].Value?.ToString() ?? "");
                    return true;

                case 4:
                    missStrategy = cmbMissing.SelectedIndex == 1 ? "mean" :
                                   cmbMissing.SelectedIndex == 2 ? "median" : "drop";
                    return true;

                case 5:
                    renameMap.Clear();
                    foreach (DataGridViewRow row in renameGrid.Rows)
                    {
                        if (row.IsNewRow) continue;
                        string orig = row.Cells[0].Value?.ToString() ?? "";
                        string newn = row.Cells[1].Value?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(newn) && newn != orig)
                            renameMap[orig] = newn;
                    }
                    return true;
            }
            return true;
        }

        // ── Export ────────────────────────────────────────────────
        private async Task FinishAsync()
        {
            outputDir = txtOutputDir?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            { MessageBox.Show("Please select a valid output folder."); return; }

            // Build config JSON
            var cfg = new Dictionary<string, object>
            {
                ["feature_cols"] = featureCols,
                ["target_cols"] = targetCols,
                ["bool_cols"] = boolCols,
                ["rename_map"] = renameMap,
                ["miss_strategy"] = missStrategy,
                ["output_dir"] = outputDir
            };

            string scriptsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "python_scripts");
            string cfgPath = Path.Combine(scriptsDir, "temp_csv_wizard_config.json");
            File.WriteAllText(cfgPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));

            btnFinish.Enabled = false;
            btnFinish.Text = "⏳ Exporting...";

            string result = await Task.Run(() =>
                ModelHandler.RunScript("csv_wizard.py",
                    "process \"" + csvPath + "\" \"" + cfgPath + "\""));

            btnFinish.Enabled = true;
            btnFinish.Text = "✔ Export";

            if (result.StartsWith("Error"))
            { MessageBox.Show(result, "Export Error"); return; }

            try
            {
                using var doc = JsonDocument.Parse(result);
                var files = doc.RootElement.GetProperty("files").EnumerateArray().ToList();
                string msg = "Successfully created " + files.Count + " Excel file(s):\n\n";
                foreach (var f in files)
                    msg += "• " + Path.GetFileName(f.GetProperty("out_path").GetString()) +
                           "  (" + f.GetProperty("n_rows").GetInt32() + " rows)\n";

                MessageBox.Show(msg, "Export Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch { MessageBox.Show(result); }
        }

        // ── Helpers ───────────────────────────────────────────────
        private static Label MakeLabel(string text, int l, int t, int w, int h)
        {
            return new Label
            {
                Text = text,
                Left = l,
                Top = t,
                Width = w,
                Height = h,
                AutoSize = false
            };
        }

        private static DataGridView MakeGrid(int l, int t, int w, int h)
        {
            return new DataGridView
            {
                Left = l,
                Top = t,
                Width = w,
                Height = h,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackgroundColor = Color.White,
                GridColor = Color.FromArgb(220, 220, 220),
                ColumnHeadersHeight = 30,
                ColumnHeadersHeightSizeMode =
                    DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(31, 92, 153),
                    ForeColor = Color.White,
                    Font = new Font("Calibri", 9f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Calibri", 9f)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(247, 249, 252)
                }
            };
        }

        // ── Inner data class ──────────────────────────────────────
        private class ColumnInfo
        {
            public string Name { get; set; }
            public string Dtype { get; set; }
            public int NMissing { get; set; }
            public double MissPct { get; set; }
            public string Sample { get; set; }
        }
    }
}