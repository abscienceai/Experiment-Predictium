using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using ExcelDataReader;
using System.Data;

namespace ExperimentPredictorApp
{
    public partial class MainForm : Form
    {
        ComboBox cmbModel;
        NumericUpDown numParameters;
        DataGridView dataGrid;
        Button btnPredict;
        TextBox txtPrediction;
        Panel inputPanel;
        List<TextBox> paramTextBoxes = new();
        Button btnLoadExcel;
        Button btnSaveModel;
        Button btnLoadModel;
        List<string> columnHeaders = new();
        ProgressBar progressBar;
        Label lblStatus;
        string currentModelPath = null;   // Path of user-loaded model (Load Trained)
        string lastModelName = null;   // Model type used for the last prediction
        string currentTempModelPath = null;  // Temp save path after prediction (for Save Trained)
        TextBox txtResult = null;      // Green result box shown next to input fields
        Label lblResult = null;      // Label above the result box
        Button btnBenchmark = null;
        Button btnViewBenchmark = null;
        Button btnDataQuality = null;
        Button btnInterpret = null;      // Model interpretability
        Button btnReport = null;      // Generate PDF report
        WizardStepPanel wizardPanel = null;
        string lastExplainJson = null;      // Last interpretability result
        string lastChartPath = null;      // Last chart PNG path

        public MainForm()
        {
            InitUI();
        }

        private void InitUI()
        {
            this.Text = "Lab Experiment Forecaster";
            this.WindowState = FormWindowState.Maximized;

            // ── Create required folders on startup ───────────────
            foreach (string folder in new[] {
                "temp_models", "save_trained", "benchmark_results",
                "load_algorithm", "pdf_reports", "csv_wizard", "excel_data" })
            {
                Directory.CreateDirectory(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, folder));
            }

            // ── Wizard step panel (top of form) ───────────────────
            wizardPanel = new WizardStepPanel()
            {
                Left = 0,
                Top = 0,
                Width = this.ClientSize.Width,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(wizardPanel);

            Label lblModel = new Label() { Text = "Algorithm:", Left = 20, Top = 46, AutoSize = true };
            lblModel.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            cmbModel = new ComboBox() { Left = 90, Top = 44, Width = 155 };
            cmbModel.Items.AddRange(new string[] { "LG", "RFR", "ANN", "LSTM", "GRU", "TFT", "CNN_LSTM" });
            cmbModel.SelectedIndex = 0;
            cmbModel.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // Save Model button — appears after a successful prediction
            btnSaveModel = new Button()
            {
                Text = "💾  Save Trained",
                Top = 76,
                Width = 150,
                Height = 28,
                Visible = false,
                BackColor = Color.FromArgb(34, 139, 34),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSaveModel.Left = this.ClientSize.Width - btnSaveModel.Width - 150;
            btnSaveModel.FlatAppearance.BorderSize = 0;
            btnSaveModel.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 110, 20);
            btnSaveModel.Click += SaveModelClicked;

            // Benchmark button — runs all 7 models and shows comparison table
            btnBenchmark = new Button()
            {
                Text = "📊  Run Benchmark",
                Top = 44,
                Width = 150,
                Height = 28,
                BackColor = Color.FromArgb(140, 60, 180),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnBenchmark.Left = 260;
            btnBenchmark.Width = 155;
            btnBenchmark.FlatAppearance.BorderSize = 0;
            btnBenchmark.FlatAppearance.MouseOverBackColor = Color.FromArgb(110, 40, 150);
            btnBenchmark.Click += BenchmarkClicked;

            // View saved benchmark button
            btnViewBenchmark = new Button()
            {
                Text = "📂  View Benchmark",
                Top = 76,
                Width = 155,
                Height = 28,
                Left = 260,
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnViewBenchmark.FlatAppearance.BorderSize = 0;
            btnViewBenchmark.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
            btnViewBenchmark.Click += (s, e) =>
            {
                string benchDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmark_results");
                Directory.CreateDirectory(benchDir);
                using var ofd = new OpenFileDialog();
                ofd.Title = "Open Saved Benchmark";
                ofd.InitialDirectory = benchDir;
                ofd.Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    var form = new BenchmarkForm(ofd.FileName);
                    form.ShowDialog(this);
                }
            };

            // Interpret button (Row 1, after quality pair)
            btnInterpret = new Button()
            {
                Text = "🔬  Interpret",
                Top = 44,
                Width = 120,
                Height = 28,
                Left = 590,
                BackColor = Color.FromArgb(0, 130, 140),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Enabled = false
            };
            btnInterpret.FlatAppearance.BorderSize = 0;
            btnInterpret.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 100, 110);
            btnInterpret.Click += InterpretClicked;

            // PDF Report button (Row 2, below Interpret)
            btnReport = new Button()
            {
                Text = "📄  PDF Report",
                Top = 76,
                Width = 120,
                Height = 28,
                Left = 590,
                BackColor = Color.FromArgb(150, 0, 0),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Enabled = false
            };
            btnReport.FlatAppearance.BorderSize = 0;
            btnReport.FlatAppearance.MouseOverBackColor = Color.FromArgb(110, 0, 0);
            btnReport.Click += GenerateReportClicked;

            // Data Quality button
            btnDataQuality = new Button()
            {
                Text = "🔍  Data Quality",
                Top = 44,
                Width = 155,
                Height = 28,
                Left = 425,
                BackColor = Color.FromArgb(180, 90, 0),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Enabled = false   // Enabled after data is loaded
            };
            btnDataQuality.FlatAppearance.BorderSize = 0;
            btnDataQuality.FlatAppearance.MouseOverBackColor = Color.FromArgb(140, 60, 0);
            btnDataQuality.Click += DataQualityClicked;

            // Load Saved Model button
            btnLoadModel = new Button()
            {
                Text = "📂  Load Trained",
                Top = 44,
                Width = 150,
                Height = 28,
                BackColor = Color.FromArgb(180, 100, 0),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnLoadModel.Left = this.ClientSize.Width - btnLoadModel.Width - 280;
            btnLoadModel.FlatAppearance.BorderSize = 0;
            btnLoadModel.FlatAppearance.MouseOverBackColor = Color.FromArgb(150, 80, 0);
            btnLoadModel.Click += LoadSavedModelClicked;

            // Load Custom Algorithm button — loads .pkl/.h5/.pt/.py into the combo
            var btnLoadCustomAlgo = new Button()
            {
                Text = "📦  Load Algorithm",
                Top = 76,
                Width = 150,
                Height = 28,
                BackColor = Color.FromArgb(60, 60, 140),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnLoadCustomAlgo.Left = 425;
            btnLoadCustomAlgo.Width = 155;
            btnLoadCustomAlgo.FlatAppearance.BorderSize = 0;
            btnLoadCustomAlgo.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 110);
            btnLoadCustomAlgo.Click += LoadCustomAlgorithmClicked;
            this.Controls.Add(btnLoadCustomAlgo);

            Label lblParamCount = new Label() { Text = "Number of Parameters:", Left = 20, Top = 78, AutoSize = true };
            lblParamCount.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            numParameters = new NumericUpDown()
            {
                Left = 155,
                Top = 76,
                Width = 50,
                Minimum = 1,
                Maximum = 20,
                Value = 3
            };
            numParameters.ValueChanged += OnParamCountChanged;
            numParameters.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            btnLoadExcel = new Button()
            {
                Text = "📊  Load Excel",
                Top = 44,
                Width = 130,
                Height = 28,
                BackColor = Color.FromArgb(31, 92, 153),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnLoadExcel.Left = this.ClientSize.Width - btnLoadExcel.Width - 20;
            btnLoadExcel.FlatAppearance.BorderSize = 0;
            btnLoadExcel.FlatAppearance.MouseOverBackColor = Color.FromArgb(21, 70, 120);
            btnLoadExcel.Click += LoadExcelClicked;

            // CSV Wizard button — below Load Excel
            var btnCsvWizard = new Button()
            {
                Text = "🧙  CSV Wizard",
                Top = 76,
                Width = 130,
                Height = 28,
                BackColor = Color.FromArgb(80, 40, 120),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCsvWizard.Left = this.ClientSize.Width - btnCsvWizard.Width - 20;
            btnCsvWizard.FlatAppearance.BorderSize = 0;
            btnCsvWizard.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 20, 100);
            btnCsvWizard.Click += (s, e) =>
            {
                var wizard = new CsvWizardForm();
                wizard.ShowDialog(this);
            };
            this.Controls.Add(btnCsvWizard);

            this.Controls.Add(lblModel);
            this.Controls.Add(cmbModel);
            this.Controls.Add(lblParamCount);
            this.Controls.Add(numParameters);
            this.Controls.Add(btnLoadExcel);
            this.Controls.Add(btnSaveModel);
            this.Controls.Add(btnLoadModel);
            this.Controls.Add(btnBenchmark);
            this.Controls.Add(btnViewBenchmark);
            this.Controls.Add(btnInterpret);
            this.Controls.Add(btnReport);
            this.Controls.Add(btnDataQuality);

            inputPanel = new Panel()
            {
                Left = 20,
                Top = 116,
                Width = this.ClientSize.Width - 40,
                Height = 72,
                AutoScroll = false,
                BackColor = Color.FromArgb(248, 250, 253),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            dataGrid = new DataGridView()
            {
                Left = 20,
                Top = 194,
                Width = this.ClientSize.Width - 40,
                Height = this.ClientSize.Height - 340,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                RowHeadersVisible = true,          // Sol tarafta satır numaraları
                RowHeadersWidth = 60,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 48,          // İki satır başlık için yükseklik
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(220, 220, 220),
                BackgroundColor = Color.White,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(245, 247, 250)  // Hafif gri
                },
                RowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(31, 92, 153),   // COST mavisi
                    ForeColor = Color.White,
                    Font = new Font("Calibri", 8.5f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.True
                },
                RowHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(240, 244, 248),
                    ForeColor = Color.FromArgb(80, 80, 80),
                    Font = new Font("Calibri", 8f),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };

            // Sütun genişliği değişince input'ları hizala
            dataGrid.ColumnWidthChanged += (s, e) => AlignInputsToColumns();
            dataGrid.Scroll += (s, e) => AlignInputsToColumns();
            dataGrid.Layout += (s, e) => AlignInputsToColumns();

            // Satır numaralarını göster
            dataGrid.RowPostPaint += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var grid = (DataGridView)s;
                    string rowNum = (e.RowIndex + 1).ToString();
                    var rect = grid.GetRowDisplayRectangle(e.RowIndex, true);
                    var headerRect = new Rectangle(0, rect.Top, grid.RowHeadersWidth, rect.Height);
                    using var brush = new SolidBrush(Color.FromArgb(100, 100, 100));
                    e.Graphics.DrawString(rowNum,
                        new Font("Calibri", 8f),
                        brush,
                        headerRect,
                        new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        });
                }
            };

            // ── ALT PANEL: BUTON + SONUÇ + İLERLEME ─────────────────
            int btnH = 28;          // Button height
            int txtH = 70;          // Prediction textbox height (2.5x button)
            int bottomY = this.ClientSize.Height - txtH - 14;  // Textbox bottom margin

            progressBar = new ProgressBar()
            {
                Left = 20,
                Top = this.ClientSize.Height - 8,
                Width = this.ClientSize.Width - 40,
                Height = 6,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // lblStatus — txtPrediction içine gömülü, ayrı kontrol kullanmıyoruz
            lblStatus = new Label()
            {
                Left = 0,
                Top = 0,
                Width = 0,
                Height = 0,
                Visible = false
            };

            // Add label above the prediction result textbox
            var lblPredTitle = new Label()
            {
                Text = "Prediction Result:",
                Left = 178,
                Top = bottomY - 18,
                Width = 200,
                Height = 16,
                Font = new Font("Calibri", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 92, 153),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            this.Controls.Add(lblPredTitle);

            btnPredict = new Button()
            {
                Text = "▶  Run Prediction",
                Left = 20,
                Width = 150,
                Height = btnH,
                Top = bottomY + (txtH - btnH) / 2,  // Vertically centered beside textbox
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.FromArgb(31, 92, 153),
                ForeColor = Color.White,
                Font = new Font("Calibri", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPredict.FlatAppearance.BorderSize = 0;
            btnPredict.FlatAppearance.MouseOverBackColor = Color.FromArgb(21, 70, 120);
            btnPredict.FlatAppearance.MouseDownBackColor = Color.FromArgb(15, 50, 90);
            btnPredict.Click += PredictClicked;

            txtPrediction = new TextBox()
            {
                Left = 178,
                Top = bottomY,
                Width = this.ClientSize.Width - 198,
                Height = txtH,
                Multiline = true,
                ReadOnly = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Calibri", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 100, 20),
                BackColor = Color.FromArgb(240, 248, 240),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.None
            };

            this.Controls.Add(inputPanel);
            this.Controls.Add(dataGrid);
            this.Controls.Add(btnPredict);
            this.Controls.Add(txtPrediction);
            this.Controls.Add(lblStatus);
            this.Controls.Add(progressBar);

            this.Resize += (s, e) =>
            {
                // Guard against resize firing before all controls are initialized
                if (wizardPanel == null || btnLoadExcel == null ||
                    btnSaveModel == null || btnLoadModel == null ||
                    inputPanel == null || dataGrid == null ||
                    txtPrediction == null || btnPredict == null ||
                    progressBar == null) return;

                wizardPanel.Width = this.ClientSize.Width;
                btnLoadExcel.Left = this.ClientSize.Width - btnLoadExcel.Width - 20;
                btnSaveModel.Left = this.ClientSize.Width - btnSaveModel.Width - 160;
                btnLoadModel.Left = this.ClientSize.Width - btnLoadModel.Width - 150;
                inputPanel.Width = this.ClientSize.Width - 40;
                dataGrid.Width = this.ClientSize.Width - 40;
                dataGrid.Height = this.ClientSize.Height - 340;
                int tH = txtPrediction.Height;
                int tY = this.ClientSize.Height - tH - 14;
                txtPrediction.Top = tY;
                txtPrediction.Width = this.ClientSize.Width - 198;
                btnPredict.Top = tY + (tH - btnPredict.Height) / 2;
                foreach (Control c in this.Controls)
                    if (c is Label lbl && lbl.Text == "Prediction Result:")
                    { lbl.Top = tY - 18; lbl.Left = 178; break; }
                progressBar.Top = this.ClientSize.Height - 8;
                progressBar.Width = this.ClientSize.Width - 40;
                AlignInputsToColumns();
            };

            GenerateDataGrid();
            GenerateInputFields();
        }

        private void LoadCustomModelClicked(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Model Files|*.zip;*.h5;*.pt;*.pkl";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string customModelPath = ofd.FileName;
                cmbModel.Items.Add(customModelPath);
                cmbModel.SelectedItem = customModelPath;
            }
        }

        // Load a custom algorithm file (.pkl, .h5, .pt, or .py script)
        private void LoadCustomAlgorithmClicked(object sender, EventArgs e)
        {
            string loadAlgoDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "load_algorithm");
            Directory.CreateDirectory(loadAlgoDir);

            using var ofd = new OpenFileDialog();
            ofd.Title = "Load Custom Algorithm";
            ofd.InitialDirectory = loadAlgoDir;
            ofd.Filter = "Algorithm Files (*.pkl;*.h5;*.pt;*.py)|*.pkl;*.h5;*.pt;*.py" +
                         "|Python Script (*.py)|*.py" +
                         "|Trained Model (*.pkl;*.h5;*.pt)|*.pkl;*.h5;*.pt";

            if (ofd.ShowDialog() != DialogResult.OK) return;

            string filePath = ofd.FileName;
            string ext = Path.GetExtension(filePath).ToLower();
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            if (ext == ".py")
            {
                // Copy .py script to python_scripts (executable) and custom_algorithms (user visible)
                string scriptsDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "python_scripts");
                string customAlgoDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "load_algorithm");
                Directory.CreateDirectory(customAlgoDir);
                string destFile = Path.Combine(scriptsDir, "predict_" + fileName + ".py");
                string algoFile = Path.Combine(customAlgoDir, fileName + ".py");

                try
                {
                    File.Copy(filePath, destFile, overwrite: true);
                    File.Copy(filePath, algoFile, overwrite: true);

                    // Add to combo if not already there
                    string comboName = "py:" + fileName;
                    if (!cmbModel.Items.Contains(comboName))
                        cmbModel.Items.Add(comboName);
                    cmbModel.SelectedItem = comboName;

                    txtPrediction.ForeColor = Color.FromArgb(31, 92, 153);
                    txtPrediction.BackColor = Color.FromArgb(240, 244, 255);
                    txtPrediction.Text = "📦  Python script loaded: " + fileName +
                        ".py  —  copied to python_scripts/ and ready to use";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not copy script: " + ex.Message,
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // Trained model file (.pkl / .h5 / .pt) — set as currentModelPath
                currentModelPath = filePath;

                string comboName = Path.GetFileName(filePath);
                if (!cmbModel.Items.Contains(comboName))
                    cmbModel.Items.Add(comboName);
                cmbModel.SelectedItem = comboName;

                txtPrediction.ForeColor = Color.FromArgb(31, 92, 153);
                txtPrediction.BackColor = Color.FromArgb(240, 244, 255);
                txtPrediction.Text = "📦  Algorithm loaded: " + comboName +
                    "  —  ready to predict (no retraining needed)";
            }
        }

        // Named handler so we can detach/reattach it during Excel loading
        private void OnParamCountChanged(object sender, EventArgs e)
        {
            GenerateDataGrid();
            GenerateInputFields();
        }

        private void LoadExcelClicked(object sender, EventArgs e)
        {
            try
            {
                string excelDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "excel_data");
                Directory.CreateDirectory(excelDataDir);

                using OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Load Excel Data";
                ofd.InitialDirectory = excelDataDir;
                ofd.Filter = "Excel Files|*.xlsx;*.xls";

                if (ofd.ShowDialog() != DialogResult.OK) return;

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using FileStream stream = File.Open(ofd.FileName, FileMode.Open, FileAccess.Read);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();

                if (result.Tables.Count == 0)
                    throw new Exception("No data found in Excel file.");

                DataTable table = result.Tables[0];
                if (table.Rows.Count == 0)
                    throw new Exception("Excel file is empty.");

                // ── BAŞLIK SATIRINI ALGILAMA ──────────────────────────────────
                // İlk satırı kontrol et: eğer sayısal değilse başlık satırıdır
                columnHeaders.Clear();
                int dataStartRow = 0;
                bool hasHeader = false;

                var firstRow = table.Rows[0].ItemArray;
                bool firstRowIsText = false;
                foreach (var cell in firstRow)
                {
                    string val = cell?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(val) && !double.TryParse(val, out _))
                    {
                        firstRowIsText = true;
                        break;
                    }
                }

                if (firstRowIsText)
                {
                    hasHeader = true;
                    dataStartRow = 1;
                    foreach (var cell in firstRow)
                        columnHeaders.Add(cell?.ToString() ?? "");
                }

                // ── PARAMETRE SAYISINI OTOMATİK AYARLA ───────────────────────
                int totalCols = table.Columns.Count;
                int paramCount = totalCols - 1;

                if (paramCount < 1)
                    throw new Exception("Excel file must have at least 2 columns.");

                // Detach ValueChanged to prevent grid reset during assignment
                numParameters.ValueChanged -= OnParamCountChanged;
                numParameters.Value = Math.Min(Math.Max(paramCount, 1), 20);
                numParameters.ValueChanged += OnParamCountChanged;

                // ── SÜTUN BAŞLIKLARIYLA TABLOYA GENERAİLE ─────────────────────
                dataGrid.Rows.Clear();
                dataGrid.Columns.Clear();

                if (hasHeader && columnHeaders.Count >= totalCols)
                {
                    for (int i = 0; i < paramCount; i++)
                    {
                        string name = i < columnHeaders.Count ? columnHeaders[i] : $"Parameter {i + 1}";
                        string header = $"[PARAM {i + 1}]\n{name}";

                        var col = new DataGridViewTextBoxColumn
                        {
                            Name = $"Param{i + 1}",
                            HeaderText = header,
                            MinimumWidth = 100,
                            DefaultCellStyle = new DataGridViewCellStyle
                            {
                                Alignment = DataGridViewContentAlignment.MiddleCenter,
                                Font = new Font("Calibri", 8.5f)
                            }
                        };
                        col.HeaderCell.Style.BackColor = Color.FromArgb(31, 92, 153);
                        col.HeaderCell.Style.ForeColor = Color.White;
                        dataGrid.Columns.Add(col);
                    }

                    string resultName = (paramCount < columnHeaders.Count)
                        ? columnHeaders[paramCount] : "Result";
                    string resultHeader = $"[RESULT]\n{resultName}";

                    var resultCol = new DataGridViewTextBoxColumn
                    {
                        Name = "Result",
                        HeaderText = resultHeader,
                        MinimumWidth = 120,
                        DefaultCellStyle = new DataGridViewCellStyle
                        {
                            Alignment = DataGridViewContentAlignment.MiddleCenter,
                            Font = new Font("Calibri", 8.5f, FontStyle.Bold),
                            BackColor = Color.FromArgb(235, 245, 235),
                            ForeColor = Color.FromArgb(20, 100, 20)
                        }
                    };
                    resultCol.HeaderCell.Style.BackColor = Color.FromArgb(34, 139, 34);
                    resultCol.HeaderCell.Style.ForeColor = Color.White;
                    resultCol.HeaderCell.Style.Font = new Font("Calibri", 8.5f, FontStyle.Bold);
                    dataGrid.Columns.Add(resultCol);
                }
                else
                {
                    GenerateDataGrid();
                }

                // ── VERİ SATIRLARINI YÜKLE (başlık atlanır) ───────────────────
                for (int r = dataStartRow; r < table.Rows.Count; r++)
                {
                    object[] cells = table.Rows[r].ItemArray;
                    if (cells.Length < totalCols) continue;

                    // Boş veya null satırları atla
                    bool rowEmpty = true;
                    foreach (var c in cells)
                        if (!string.IsNullOrWhiteSpace(c?.ToString())) { rowEmpty = false; break; }
                    if (rowEmpty) continue;

                    dataGrid.Rows.Add();
                    int rowIdx = dataGrid.Rows.Count - 2;
                    for (int i = 0; i <= paramCount && i < cells.Length; i++)
                        dataGrid.Rows[rowIdx].Cells[i].Value = cells[i]?.ToString();
                }

                // Giriş kutularını başlık isimleriyle güncelle
                GenerateInputFields();

                // Advance wizard to step 3 (Data Quality) and enable quality button
                if (wizardPanel != null) wizardPanel.CurrentStep = 3;
                if (btnDataQuality != null) btnDataQuality.Enabled = true;

                // Clear only the user-loaded model path — auto-saved models stay on disk
                // Python scripts will verify feature count before loading
                currentModelPath = null;

                string targetName = hasHeader && paramCount < columnHeaders.Count
                    ? columnHeaders[paramCount] : "Last column";
                string loadMsg = "Loaded: " + (dataGrid.Rows.Count - 1) + " rows" +
                    "  |  Parameters: " + paramCount +
                    "  |  Target: " + targetName;
                MessageBox.Show(loadMsg, "Excel Loaded",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading Excel: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GenerateInputFields()
        {
            inputPanel.Controls.Clear();
            paramTextBoxes.Clear();
            int paramCount = (int)numParameters.Value;

            for (int i = 0; i < paramCount; i++)
            {
                string labelText = (columnHeaders.Count > i)
                    ? columnHeaders[i] : $"P{i + 1}";

                // Label (sütun adı)
                Label label = new Label()
                {
                    Text = labelText,
                    Left = 10 + (i * 120),
                    Top = 6,
                    Width = 100,
                    Height = 18,
                    AutoSize = false,
                    Font = new Font("Calibri", 7.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(31, 92, 153),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                // TextBox
                TextBox textBox = new TextBox()
                {
                    Left = 10 + (i * 120),
                    Top = 28,
                    Width = 100,
                    Height = 26,
                    Font = new Font("Calibri", 9f),
                    TextAlign = System.Windows.Forms.HorizontalAlignment.Center,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                inputPanel.Controls.Add(label);
                inputPanel.Controls.Add(textBox);
                paramTextBoxes.Add(textBox);
            }

            // Result display box — green box at the end of the input panel
            lblResult = new Label();
            // Use the actual target column name if available, otherwise generic label
            int targetIdx = (int)numParameters.Value;
            lblResult.Text = columnHeaders.Count > targetIdx
                ? columnHeaders[targetIdx]
                : "▼ Result";
            lblResult.Top = 6;
            lblResult.Width = 110;
            lblResult.Height = 18;
            lblResult.AutoSize = false;
            lblResult.Font = new Font("Calibri", 7.5f, FontStyle.Bold);
            lblResult.ForeColor = Color.FromArgb(20, 100, 20);
            lblResult.TextAlign = ContentAlignment.MiddleCenter;
            lblResult.Left = inputPanel.Width - 120;

            txtResult = new TextBox();
            txtResult.Top = 28;
            txtResult.Width = 110;
            txtResult.Height = 26;
            txtResult.Font = new Font("Calibri", 9f, FontStyle.Bold);
            txtResult.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            txtResult.ReadOnly = true;
            txtResult.BackColor = Color.FromArgb(220, 245, 220);
            txtResult.ForeColor = Color.FromArgb(20, 100, 20);
            txtResult.BorderStyle = BorderStyle.FixedSingle;
            txtResult.Left = inputPanel.Width - 120;
            txtResult.Text = "—";

            inputPanel.Controls.Add(lblResult);
            inputPanel.Controls.Add(txtResult);

            // DataGrid render olduktan sonra hizala
            if (dataGrid.IsHandleCreated)
                dataGrid.BeginInvoke((Action)(() => AlignInputsToColumns()));
            else
                dataGrid.HandleCreated += (s, e) => AlignInputsToColumns();
        }

        private void AlignInputsToColumns()
        {
            if (dataGrid.Columns.Count == 0 || paramTextBoxes.Count == 0) return;

            // Keep result box anchored to right edge of input panel
            if (txtResult != null)
            {
                txtResult.Left = inputPanel.Width - txtResult.Width - 8;
                lblResult.Left = txtResult.Left;
            }

            int paramCount = paramTextBoxes.Count;
            int gridLeft = dataGrid.Left - inputPanel.Left;
            int rowHeaderW = dataGrid.RowHeadersVisible ? dataGrid.RowHeadersWidth : 0;

            int x = gridLeft + rowHeaderW;

            for (int i = 0; i < paramCount && i < dataGrid.Columns.Count; i++)
            {
                var col = dataGrid.Columns[i];
                int colWidth = col.Width;
                int centerX = x + colWidth / 2;

                // Label ve TextBox'ı bu sütunun ortasına hizala
                int controlWidth = Math.Max(colWidth - 8, 60);
                int controlLeft = centerX - controlWidth / 2;

                // Label bul (her çift: label, textbox)
                if (i * 2 + 1 < inputPanel.Controls.Count)
                {
                    var lbl = inputPanel.Controls[i * 2] as Label;
                    var txt = inputPanel.Controls[i * 2 + 1] as TextBox;

                    if (lbl != null)
                    {
                        lbl.Left = controlLeft;
                        lbl.Width = controlWidth;
                    }
                    if (txt != null)
                    {
                        txt.Left = controlLeft;
                        txt.Width = controlWidth;
                    }
                }

                x += colWidth;
            }
        }

        private void GenerateDataGrid()
        {
            dataGrid.Columns.Clear();
            int paramCount = (int)numParameters.Value;

            for (int i = 0; i < paramCount; i++)
            {
                string name = (columnHeaders.Count > i) ? columnHeaders[i] : $"Parameter {i + 1}";
                string header = $"[PARAM {i + 1}]\n{name}";

                var col = new DataGridViewTextBoxColumn
                {
                    Name = $"Param{i + 1}",
                    HeaderText = header,
                    MinimumWidth = 100,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        Font = new Font("Calibri", 8.5f)
                    }
                };
                col.HeaderCell.Style.BackColor = Color.FromArgb(31, 92, 153);
                col.HeaderCell.Style.ForeColor = Color.White;
                dataGrid.Columns.Add(col);
            }

            // RESULT sütunu — farklı renk
            string resultName = (columnHeaders.Count > paramCount) ? columnHeaders[paramCount] : "Result";
            string resultHeader2 = $"[RESULT]\n{resultName}";

            var resultCol = new DataGridViewTextBoxColumn
            {
                Name = "Result",
                HeaderText = resultHeader2,
                MinimumWidth = 120,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Calibri", 8.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(235, 245, 235),   // Hafif yeşil
                    ForeColor = Color.FromArgb(20, 100, 20)
                }
            };
            resultCol.HeaderCell.Style.BackColor = Color.FromArgb(34, 139, 34);  // Yeşil
            resultCol.HeaderCell.Style.ForeColor = Color.White;
            resultCol.HeaderCell.Style.Font = new Font("Calibri", 8.5f, FontStyle.Bold);
            dataGrid.Columns.Add(resultCol);
        }

        private async void PredictClicked(object sender, EventArgs e)
        {
            try
            {
                int paramCount = (int)numParameters.Value;
                List<string> trainingDataLines = new List<string>();

                // Eğitim verisi: her satırdan girdi + çıktı al
                // İlk satır başlık ise atla (sayısal olmayan ilk hücre kontrolü)
                var culture = CultureInfo.InvariantCulture;

                foreach (DataGridViewRow row in dataGrid.Rows)
                {
                    if (row.IsNewRow) continue;

                    // İlk hücre sayısal değilse başlık satırı — atla
                    var firstCell = row.Cells[0].Value?.ToString()?.Trim() ?? "";
                    if (!double.TryParse(firstCell, NumberStyles.Any, culture, out _))
                        continue;

                    List<string> values = new();
                    bool rowValid = true;

                    for (int i = 0; i <= paramCount; i++)
                    {
                        var cellVal = row.Cells[i].Value?.ToString()?.Trim() ?? "";
                        // Noktayı virgüle veya tersi normalize et
                        cellVal = cellVal.Replace(",", ".");
                        if (!double.TryParse(cellVal, NumberStyles.Any, culture, out double parsed))
                        {
                            rowValid = false;
                            break;
                        }
                        // Python için her zaman noktalı format
                        values.Add(parsed.ToString("G", culture));
                    }

                    if (rowValid)
                        trainingDataLines.Add(string.Join(",", values));
                }

                // When a saved model is loaded, training data is not required
                bool modelAlreadySaved = !string.IsNullOrEmpty(currentModelPath) &&
                                          File.Exists(currentModelPath);

                if (trainingDataLines.Count == 0 && !modelAlreadySaved)
                    throw new Exception("No valid numeric training data found. Make sure your data rows contain numbers.");

                // If model is loaded but no grid data, create a dummy row so Python can read input_size
                if (trainingDataLines.Count == 0 && modelAlreadySaved)
                {
                    int dummyCols = (int)numParameters.Value + 1;
                    trainingDataLines.Add(string.Join(",", new string[dummyCols].Select(_ => "0")));
                }

                // Tahmin için girdi değerlerini al
                List<string> predictValues = new();
                foreach (var txt in paramTextBoxes)
                {
                    if (string.IsNullOrWhiteSpace(txt.Text))
                        throw new Exception("Please fill in all parameter fields.");

                    string txtVal = txt.Text.Trim().Replace(",", ".");
                    if (!double.TryParse(txtVal, NumberStyles.Any, culture, out double val))
                        throw new Exception($"Invalid value: '{txt.Text}'. All parameters must be numeric.");

                    predictValues.Add(val.ToString("G", culture));
                }

                string trainingCsv = string.Join("\n", trainingDataLines);
                string predictInput = string.Join(",", predictValues);
                string modelName = cmbModel.SelectedItem.ToString();

                // Strip "py:" prefix for custom Python scripts
                if (modelName.StartsWith("py:"))
                    modelName = modelName.Substring(3);

                // Use user-loaded model if set, otherwise save to temp path for Save Trained
                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_models");
                Directory.CreateDirectory(tempDir);
                string modelExt = ModelHandler.GetModelExtension(modelName);
                string tempModelPath = Path.Combine(tempDir, modelName + "_temp" + modelExt);

                string effectiveModelPath = currentModelPath ?? tempModelPath;

                // UI update — show progress
                btnPredict.Enabled = false;
                btnSaveModel.Visible = false;
                btnPredict.Text = "⏳  Running...";
                txtPrediction.ForeColor = Color.FromArgb(31, 92, 153);
                txtPrediction.BackColor = Color.FromArgb(240, 244, 255);
                txtPrediction.Text = currentModelPath != null
                    ? "⏳  Loading saved " + modelName + " model..."
                    : "⏳  Training " + modelName + " on " + trainingDataLines.Count + " samples...";
                progressBar.Visible = true;
                this.Refresh();

                // Run in background — keep UI responsive
                string prediction = await Task.Run(() =>
                    ModelHandler.RunModel(modelName, trainingCsv, predictInput, effectiveModelPath));

                // Sonucu göster
                progressBar.Visible = false;
                btnPredict.Enabled = true;
                btnPredict.Text = "▶  Run Prediction";

                if (prediction.StartsWith("Error"))
                {
                    txtPrediction.ForeColor = Color.FromArgb(180, 0, 0);
                    txtPrediction.BackColor = Color.FromArgb(255, 240, 240);
                    txtPrediction.Text = prediction;
                }
                else
                {
                    txtPrediction.ForeColor = Color.FromArgb(20, 100, 20);
                    txtPrediction.BackColor = Color.FromArgb(240, 248, 240);

                    bool modelWasLoaded = currentModelPath != null && File.Exists(currentModelPath);
                    string tag = modelWasLoaded ? "loaded" : "trained";
                    txtPrediction.Text = "✔  Result (" + modelName + ", " + tag + "): " + prediction;

                    // Store temp path so Save Trained can find the model
                    if (!modelWasLoaded)
                        currentTempModelPath = tempModelPath;

                    // Update inline result box
                    if (txtResult != null)
                    {
                        txtResult.Text = prediction.Length > 12
                            ? prediction.Substring(0, 12) : prediction;
                        txtResult.BackColor = Color.FromArgb(200, 240, 200);
                    }

                    // Advance wizard to final step
                    if (wizardPanel != null) wizardPanel.CurrentStep = 4;

                    // Enable interpret and report buttons
                    lastModelName = modelName;
                    btnSaveModel.Visible = true;
                    if (btnInterpret != null) btnInterpret.Enabled = true;
                    if (btnReport != null) btnReport.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                btnPredict.Enabled = true;
                btnPredict.Text = "▶  Run Prediction";
                btnSaveModel.Visible = false;
                MessageBox.Show("Error: " + ex.Message, "Prediction Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Save the trained model to a user-chosen location
        private void SaveModelClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(lastModelName)) return;

            // Use temp model from last prediction, or user-loaded model
            string sourcePath = currentTempModelPath ?? currentModelPath;

            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                MessageBox.Show("No trained model found. Run a prediction first.",
                    "Save Trained", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string modelExt = ModelHandler.GetModelExtension(lastModelName);

            string savedModelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save_trained");
            Directory.CreateDirectory(savedModelsDir);

            using var sfd = new SaveFileDialog();
            sfd.Title = "Save Trained Model";
            sfd.InitialDirectory = savedModelsDir;
            sfd.Filter = modelExt == ".pkl"
                ? "Pickle Model (*.pkl)|*.pkl"
                : "PyTorch Model (*.pt)|*.pt";
            sfd.FileName = lastModelName + "_model" + modelExt;

            if (sfd.ShowDialog() != DialogResult.OK) return;

            // Ask user for an optional description/notes for this model
            string description = Microsoft.VisualBasic.Interaction.InputBox(
                "Add a description for this model (optional):" + Environment.NewLine +
                "e.g. MOF Heat Capacity — CoRE MOF DB 2024 — 1372 samples — R²=0.94",
                "Model Description", "");

            // Copy model file to user-chosen location
            File.Copy(sourcePath, sfd.FileName, overwrite: true);

            // Save metadata JSON alongside the model (same name, .json extension)
            string metaPath = Path.ChangeExtension(sfd.FileName, ".json");
            var metadata = new
            {
                modelName = lastModelName,
                paramCount = (int)numParameters.Value,
                columnHeaders = columnHeaders,
                description = description,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            File.WriteAllText(metaPath, JsonSerializer.Serialize(metadata,
                new JsonSerializerOptions { WriteIndented = true }));

            string savedMsg = "Model saved: " + Path.GetFileName(sfd.FileName);
            if (!string.IsNullOrWhiteSpace(description))
                savedMsg += Environment.NewLine + "Note: " + description;
            MessageBox.Show(savedMsg, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Run model interpretability analysis
        private async void InterpretClicked(object sender, EventArgs e)
        {
            var culture = CultureInfo.InvariantCulture;
            int paramCount = (int)numParameters.Value;
            var lines = new System.Collections.Generic.List<string>();

            foreach (DataGridViewRow row in dataGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var first = row.Cells[0].Value?.ToString()?.Trim() ?? "";
                if (!double.TryParse(first, NumberStyles.Any, culture, out _)) continue;
                var vals = new System.Collections.Generic.List<string>();
                bool ok = true;
                for (int i = 0; i <= paramCount; i++)
                {
                    var cv = row.Cells[i].Value?.ToString()?.Trim().Replace(",", ".") ?? "";
                    if (!double.TryParse(cv, NumberStyles.Any, culture, out double pv))
                    { ok = false; break; }
                    vals.Add(pv.ToString("G", culture));
                }
                if (ok) lines.Add(string.Join(",", vals));
            }

            if (lines.Count < 10)
            {
                MessageBox.Show("At least 10 data rows required for interpretability.",
                    "Interpret", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scriptsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "python_scripts");
            string tempFile = Path.Combine(scriptsDir, "temp_explain.csv");
            string chartPath = Path.Combine(scriptsDir, "temp_chart.png");
            File.WriteAllText(tempFile, string.Join(Environment.NewLine, lines));

            string modelName = (lastModelName ?? cmbModel.SelectedItem?.ToString() ?? "RFR");
            if (modelName.StartsWith("py:")) modelName = modelName.Substring(3);
            string headers = string.Join("|", columnHeaders);

            btnInterpret.Enabled = false;
            btnInterpret.Text = "⏳  Analyzing...";
            progressBar.Visible = true;
            this.Refresh();

            string jsonResult = await Task.Run(() =>
                ModelHandler.RunScript("explain.py",
                    "\"" + tempFile + "\" \"" + modelName + "\" \"" + chartPath + "\" \"" + headers + "\""));

            progressBar.Visible = false;
            btnInterpret.Enabled = true;
            btnInterpret.Text = "🔬  Interpret";

            if (jsonResult.StartsWith("Error"))
            {
                MessageBox.Show(jsonResult, "Interpret Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            lastExplainJson = jsonResult;
            lastChartPath = chartPath;
            var form = new ExplainForm(jsonResult);
            form.ShowDialog(this);
        }

        // Generate PDF report
        private async void GenerateReportClicked(object sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog();
            sfd.Title = "Save Analysis Report";
            sfd.Filter = "PDF File (*.pdf)|*.pdf";
            sfd.InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdf_reports");
            Directory.CreateDirectory(sfd.InitialDirectory);
            sfd.FileName = "ExperimentPredictium_Report_" +
                           DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf";
            if (sfd.ShowDialog() != DialogResult.OK) return;

            string scriptsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "python_scripts");
            string dataFile = Path.Combine(scriptsDir, "temp_report_data.json");

            // Collect all available data
            int paramCount = (int)numParameters.Value;
            int tidx = paramCount;
            string target = columnHeaders.Count > tidx ? columnHeaders[tidx] : "Target";

            // Build report data JSON
            var reportData = new System.Collections.Generic.Dictionary<string, object>
            {
                ["target_name"] = target,
                ["n_samples"] = dataGrid.Rows.Count - 1,
                ["n_features"] = paramCount,
                ["model_name"] = lastModelName ?? "—",
                ["prediction"] = txtPrediction.Text.Replace("✔  Result", "").Trim(),
                ["explanation"] = lastExplainJson != null
                    ? System.Text.Json.JsonDocument.Parse(lastExplainJson)
                        .RootElement.GetProperty("explanation").GetString()
                    : "",
                ["chart_path"] = lastChartPath ?? ""
            };

            System.IO.File.WriteAllText(dataFile,
                System.Text.Json.JsonSerializer.Serialize(reportData));

            btnReport.Enabled = false;
            btnReport.Text = "⏳  Generating...";
            progressBar.Visible = true;
            this.Refresh();

            string result = await Task.Run(() =>
                ModelHandler.RunScript("report.py",
                    "\"" + dataFile + "\" \"" + sfd.FileName + "\""));

            progressBar.Visible = false;
            btnReport.Enabled = true;
            btnReport.Text = "📄  PDF Report";

            if (result.StartsWith("Error"))
                MessageBox.Show(result, "Report Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                MessageBox.Show("Report saved to: " + sfd.FileName,
                    "PDF Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // Open PDF
                try
                {
                    System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(sfd.FileName)
                    { UseShellExecute = true });
                }
                catch { }
            }
        }

        // Open data quality panel for the current grid data
        private void DataQualityClicked(object sender, EventArgs e)
        {
            var culture = CultureInfo.InvariantCulture;
            int paramCount = (int)numParameters.Value;

            // Extract all rows from grid
            var data = new List<List<string>>();
            foreach (DataGridViewRow row in dataGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var first = row.Cells[0].Value?.ToString()?.Trim() ?? "";
                if (!double.TryParse(first, NumberStyles.Any, culture, out _)) continue;

                var rowData = new List<string>();
                for (int i = 0; i <= paramCount; i++)
                    rowData.Add(row.Cells[i].Value?.ToString()?.Trim() ?? "");
                data.Add(rowData);
            }

            if (data.Count == 0)
            {
                MessageBox.Show("No data loaded. Please load an Excel file first.",
                    "Data Quality", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var form = new DataQualityForm(data, columnHeaders);
            if (form.ShowDialog(this) == DialogResult.OK && form.Applied)
            {
                // Apply cleaned data back to grid
                var cleaned = form.CleanedData;
                dataGrid.Rows.Clear();
                foreach (var row in cleaned)
                {
                    dataGrid.Rows.Add();
                    int ri = dataGrid.Rows.Count - 2;
                    for (int i = 0; i < row.Count && i <= paramCount; i++)
                        dataGrid.Rows[ri].Cells[i].Value = row[i];
                }

                // Advance wizard — data is clean, ready to model
                if (wizardPanel != null) wizardPanel.CurrentStep = 4;

                MessageBox.Show("Data updated. " + cleaned.Count + " rows ready for training.",
                    "Data Quality Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Run all 7 models on current data and show comparison table
        private async void BenchmarkClicked(object sender, EventArgs e)
        {
            // Build training CSV from grid data
            int paramCount = (int)numParameters.Value;
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new System.Collections.Generic.List<string>();

            foreach (DataGridViewRow row in dataGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var first = row.Cells[0].Value?.ToString()?.Trim() ?? "";
                if (!double.TryParse(first, System.Globalization.NumberStyles.Any, culture, out _))
                    continue;

                var vals = new System.Collections.Generic.List<string>();
                bool ok = true;
                for (int i = 0; i <= paramCount; i++)
                {
                    var cv = row.Cells[i].Value?.ToString()?.Trim().Replace(",", ".") ?? "";
                    if (!double.TryParse(cv, System.Globalization.NumberStyles.Any,
                        culture, out double pv)) { ok = false; break; }
                    vals.Add(pv.ToString("G", culture));
                }
                if (ok) lines.Add(string.Join(",", vals));
            }

            if (lines.Count < 10)
            {
                MessageBox.Show("At least 10 data rows are needed for benchmarking.",
                    "Benchmark", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Write temp CSV
            string scriptsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "python_scripts");
            string tempFile = Path.Combine(scriptsDir, "temp_benchmark.csv");
            File.WriteAllText(tempFile, string.Join(Environment.NewLine, lines));

            // Run benchmark script in background
            btnBenchmark.Enabled = false;
            btnBenchmark.Text = "⏳  Running...";
            progressBar.Visible = true;
            this.Refresh();

            string jsonResult = await Task.Run(() =>
                ModelHandler.RunModel("benchmark",
                    string.Join(Environment.NewLine, lines), "0", null));

            progressBar.Visible = false;
            btnBenchmark.Enabled = true;
            btnBenchmark.Text = "📊  Run Benchmark";

            if (jsonResult.StartsWith("Error"))
            {
                MessageBox.Show(jsonResult, "Benchmark Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Determine target column name and sample count
            int tidx = (int)numParameters.Value;
            string targetName = columnHeaders.Count > tidx
                ? columnHeaders[tidx] : "Target";

            // Show results in BenchmarkForm
            var form = new BenchmarkForm(jsonResult, targetName, lines.Count);
            form.ShowDialog(this);
        }

        // Load a previously saved model — next prediction will skip training
        private void LoadSavedModelClicked(object sender, EventArgs e)
        {
            string saveTrainedDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save_trained");
            Directory.CreateDirectory(saveTrainedDir);

            using var ofd = new OpenFileDialog();
            ofd.Title = "Load Saved Model";
            ofd.InitialDirectory = saveTrainedDir;
            ofd.Filter = "Model Files (*.pkl;*.pt)|*.pkl;*.pt|Pickle (*.pkl)|*.pkl|PyTorch (*.pt)|*.pt";

            if (ofd.ShowDialog() != DialogResult.OK) return;

            currentModelPath = ofd.FileName;
            string modelFile = Path.GetFileName(ofd.FileName);
            string metaPath = Path.ChangeExtension(ofd.FileName, ".json");
            string restoredInfo = "";

            // Restore UI state from metadata if available
            if (File.Exists(metaPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                    var root = doc.RootElement;

                    // Restore model selection
                    if (root.TryGetProperty("modelName", out var mnProp))
                    {
                        string mn = mnProp.GetString();
                        foreach (string item in cmbModel.Items)
                            if (item.Equals(mn, StringComparison.OrdinalIgnoreCase))
                            { cmbModel.SelectedItem = item; break; }
                    }

                    // Restore column headers and parameter count
                    if (root.TryGetProperty("columnHeaders", out var hdrProp) &&
                        root.TryGetProperty("paramCount", out var pcProp))
                    {
                        columnHeaders.Clear();
                        foreach (var h in hdrProp.EnumerateArray())
                            columnHeaders.Add(h.GetString());

                        int pc = pcProp.GetInt32();
                        numParameters.Value = Math.Min(Math.Max(pc, 1), 20);
                        GenerateDataGrid();
                        GenerateInputFields();
                        AlignInputsToColumns();
                    }

                    // Read description and savedAt if present
                    string desc = "";
                    string savedAt = "";
                    if (root.TryGetProperty("description", out var descProp))
                        desc = descProp.GetString();
                    if (root.TryGetProperty("savedAt", out var saProp))
                        savedAt = saProp.GetString();

                    restoredInfo = " — UI restored";
                    if (!string.IsNullOrWhiteSpace(desc))
                        restoredInfo += " | Note: " + desc;
                    if (!string.IsNullOrWhiteSpace(savedAt))
                        restoredInfo += " | Saved: " + savedAt;
                }
                catch { restoredInfo = "  (metadata read error)"; }
            }
            else
            {
                // No metadata — try to detect model type from filename
                foreach (string item in cmbModel.Items)
                    if (modelFile.ToUpper().Contains(item.ToUpper()))
                    { cmbModel.SelectedItem = item; break; }

                restoredInfo = "  (no metadata found — fill parameters manually)";
            }

            txtPrediction.ForeColor = Color.FromArgb(31, 92, 153);
            txtPrediction.BackColor = Color.FromArgb(240, 244, 255);
            txtPrediction.Text = "📂  Model loaded: " + modelFile + restoredInfo;
        }
    }
}