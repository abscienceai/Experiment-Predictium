using System;
using System.Collections.Generic;
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
        Button btnLoadCustomModel; // New button to load custom model

        public MainForm()
        {
            InitUI();
        }

        private void InitUI()
        {
            

            this.Text = "Lab Experiment Forecaster";
            this.WindowState = FormWindowState.Maximized;

            Label lblModel = new Label() { Text = "Select Model:", Left = 20, Top = 20, AutoSize = true };
            lblModel.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            cmbModel = new ComboBox() { Left = 120, Top = 18, Width = 200 };
            cmbModel.Items.AddRange(new string[] { "LG", "RFR", "ANN", "LSTM", "GRU", "TFT", "CNN_LSTM" });
            cmbModel.SelectedIndex = 0;
            cmbModel.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            btnLoadCustomModel = new Button() { Text = "Load Model", Top = 18, Width = 120 };
            btnLoadCustomModel.Left = this.ClientSize.Width - btnLoadCustomModel.Width - 150;
            btnLoadCustomModel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoadCustomModel.Click += LoadCustomModelClicked;

            Label lblParamCount = new Label() { Text = "Number of Parameters:", Left = 20, Top = 60, AutoSize = true };
            lblParamCount.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            numParameters = new NumericUpDown()
            {
                Left = 150,
                Top = 58,
                Width = 50,
                Minimum = 1,
                Maximum = 20,
                Value = 3
            };
            numParameters.ValueChanged += (s, e) =>
            {
                GenerateDataGrid();
                GenerateInputFields();
            };
            numParameters.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            btnLoadExcel = new Button() { Text = "Load Excel Data", Top = 18, Width = 120 };
            btnLoadExcel.Left = this.ClientSize.Width - btnLoadExcel.Width - 20;
            btnLoadExcel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoadExcel.Click += LoadExcelClicked;

            this.Controls.Add(lblModel);
            this.Controls.Add(cmbModel);
            this.Controls.Add(lblParamCount);
            this.Controls.Add(numParameters);
            this.Controls.Add(btnLoadExcel);
            this.Controls.Add(btnLoadCustomModel); // Add custom model button

            inputPanel = new Panel()
            {
                Left = 20,
                Top = 100,
                Width = this.ClientSize.Width - 40,
                Height = 60,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            dataGrid = new DataGridView()
            {
                Left = 20,
                Top = 170,
                Width = this.ClientSize.Width - 40,
                Height = this.ClientSize.Height - 300,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnPredict = new Button()
            {
                Text = "Run Prediction",
                Left = 20,
                Width = 100,
                Top = this.ClientSize.Height - 60,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnPredict.Click += PredictClicked;


            txtPrediction = new TextBox()
            {
                Left = 140,
                Top = this.ClientSize.Height - 60,
                Width = this.ClientSize.Width - 160,
                ReadOnly = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            this.Controls.Add(inputPanel);
            this.Controls.Add(dataGrid);
            this.Controls.Add(btnPredict);
            this.Controls.Add(txtPrediction);

            this.Resize += (s, e) =>
            {
                btnLoadExcel.Left = this.ClientSize.Width - btnLoadExcel.Width - 20;
                btnLoadCustomModel.Left = this.ClientSize.Width - btnLoadCustomModel.Width - 150;
                inputPanel.Width = this.ClientSize.Width - 40;
                dataGrid.Width = this.ClientSize.Width - 40;
                dataGrid.Height = this.ClientSize.Height - 300;
                txtPrediction.Width = this.ClientSize.Width - 160;
                txtPrediction.Top = btnPredict.Top = this.ClientSize.Height - 60;
            };

            GenerateDataGrid();
            GenerateInputFields();
        }

        private void LoadCustomModelClicked(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Model Files|*.zip;*.h5;*.pt;*.pkl"; // Define the model file types you accept

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string customModelPath = ofd.FileName;
                cmbModel.Items.Add(customModelPath); // Add the custom model to the combo box
                cmbModel.SelectedItem = customModelPath; // Select the custom model
            }
        }

        private void LoadExcelClicked(object sender, EventArgs e)
        {
            try
            {
                using OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Excel Files|*.xlsx;*.xls";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    using FileStream stream = File.Open(ofd.FileName, FileMode.Open, FileAccess.Read);
                    using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                    DataSet result = reader.AsDataSet();

                    if (result.Tables.Count == 0)
                        throw new Exception("No data found in Excel file.");

                    DataTable table = result.Tables[0];

                    int paramCount = (int)numParameters.Value;
                    if (table.Columns.Count < paramCount + 1)
                        throw new Exception("Not enough columns in Excel file.");

                    dataGrid.Rows.Clear();
                    dataGrid.Columns.Clear();
                    GenerateDataGrid(); // Recreate columns

                    foreach (DataRow row in table.Rows)
                    {
                        object[] cells = row.ItemArray;
                        if (cells.Length >= paramCount + 1)
                        {
                            dataGrid.Rows.Add();
                            for (int i = 0; i <= paramCount; i++)
                            {
                                dataGrid.Rows[dataGrid.Rows.Count - 2].Cells[i].Value = cells[i]?.ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while loading Excel: " + ex.Message);
            }
        }

        private void GenerateInputFields()
        {
            inputPanel.Controls.Clear();
            paramTextBoxes.Clear();
            int paramCount = (int)numParameters.Value;

            for (int i = 0; i < paramCount; i++)
            {
                Label label = new Label()
                {
                    Text = $"P{i + 1}:",
                    Left = 10 + (i * 120),
                    Top = 10,
                    Width = 40
                };
                TextBox textBox = new TextBox()
                {
                    Left = 50 + (i * 120),
                    Top = 8,
                    Width = 60
                };

                inputPanel.Controls.Add(label);
                inputPanel.Controls.Add(textBox);
                paramTextBoxes.Add(textBox);
            }
        }

        private void GenerateDataGrid()
        {
            dataGrid.Columns.Clear();
            int paramCount = (int)numParameters.Value;

            for (int i = 0; i < paramCount; i++)
            {
                dataGrid.Columns.Add($"Param{i + 1}", $"Parameter {i + 1}");
            }

            dataGrid.Columns.Add("Result", "Result");
        }

        private void PredictClicked(object sender, EventArgs e)
        {
            try
            {
                int paramCount = (int)numParameters.Value;
                List<string> trainingDataLines = new List<string>();

                // 1. Training data: take input + output from each row
                foreach (DataGridViewRow row in dataGrid.Rows)
                {
                    if (row.IsNewRow) continue;

                    List<string> values = new();
                    for (int i = 0; i <= paramCount; i++)
                    {
                        if (row.Cells[i].Value == null)
                            throw new Exception("Fill all cells.");
                        values.Add(row.Cells[i].Value.ToString());
                    }
                    trainingDataLines.Add(string.Join(",", values));
                }

                // 2. Get the parameters to estimate
                List<string> predictValues = new();
                foreach (var txt in paramTextBoxes)
                {
                    if (!double.TryParse(txt.Text, out double val))
                        throw new Exception("Fill in all parameters numerically.");
                    predictValues.Add(val.ToString());
                }

                // 3. Prepare data to be sent to Python
                string trainingCsv = string.Join("\n", trainingDataLines);
                string predictInput = string.Join(",", predictValues);
                string model = cmbModel.SelectedItem.ToString();

                string prediction = ModelHandler.RunModel(model, trainingCsv, predictInput);
                txtPrediction.Text = "Prediction Result: " + prediction;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }
    }
}
