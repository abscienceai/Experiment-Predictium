# EU4MOFs MOF-Predictium Tutorial

This guide demonstrates the CA22147 Virtual Mobility software outputs implemented in Experiment-Predictium / MOF-Predictium.

## 1. Install requirements

```bash
pip install -r python_scripts/requirements.txt
```

The UMAP module requires `umap-learn`, which is included in `requirements.txt`.

## 2. Start the application

Open `ExperimentPredictorApp.sln` in Visual Studio 2022 and run the project. Load a numeric Excel dataset with descriptor columns followed by one regression target.

## 3. Core eight-model benchmark

Click **Run Benchmark**. The application evaluates:

- LG
- RFR
- ANN
- LSTM
- GRU
- TFT baseline
- CNN_LSTM
- MOF_PREDICTIUM

The table reports holdout RMSE, MAE, MAPE and R2, plus reproducible 5-fold cross-validation mean and standard deviation.

## 4. Advanced VM tools

Click **MOF VM Tools**.

### Robustness Suite

Uses the current numeric training table and exports:

- Isolation Forest anomaly scores/flags
- Gaussian-noise augmented data
- SMOTER-compatible target-tail augmented data
- bootstrap ensemble uncertainty with 95% intervals
- learning-curve CSV/PNG
- descriptor-space OOD flags

Augmentation outputs are optional robustness experiments. They are not automatically used as the default training set.

### Multi-Target Prediction

Load a headered CSV/XLSX. Specify how many final columns are regression targets. The tool generates 5-fold out-of-fold metrics and predictions for each target plus a reusable multi-output Random Forest model.

### Stratified / Group Evaluation

Load a headered dataset containing metadata columns such as `topology`, `metal_node`, and `linker_family`. Enter the exact target column and group columns. The tool reports:

1. ordinary 5-fold out-of-fold performance disaggregated by group;
2. leave-one-group-out performance, which is a stronger chemistry-transfer stress test.

### Batch High-Throughput Screening

Load a numeric training dataset in the main window, open **MOF VM Tools**, then choose a candidate descriptor file. Select `MOF_PREDICTIUM` or `RFR`, and choose whether the predicted target should be maximized or minimized. Results include rank, prediction, ensemble dispersion, descriptor-space distance and OOD flag.

### PCA / UMAP

Load a descriptor/property table. Numeric columns are standardized before 2D mapping. Optionally provide a numeric property column (for example `predicted_target`) to colour the structures. The tool exports coordinates and figures for PCA and UMAP.

### Pareto Front

Load a headered candidate table containing at least two objective columns. Enter objective names and matching `max` / `min` directions. The tool exports the full table with a Pareto flag and the non-dominated subset.

### Inverse Design

Load a labelled numeric training dataset in the main window, then select an existing candidate descriptor table and enter a desired **target-property value**. The module trains a differentiable neural surrogate, performs gradient-based optimisation in standardized descriptor space while constraining descriptors to the observed training ranges, and then retrieves the closest existing candidates. This is constrained inverse-design guidance over candidate space, not unconstrained generation of new crystal structures.

## 5. Tutorial dataset

A deterministic synthetic demonstration dataset is bundled at:

`tutorial_data/mof_tutorial_dataset.csv`

It contains MOF-like descriptors, chemistry metadata and three **synthetic educational targets**. It must not be used or cited as experimental MOF evidence.

## 6. Reproducibility

Random seed: 42. See `REPRODUCIBILITY.md` and `reproducibility/run_vm_demo.py`.

### Leakage control for grouped evaluation

If the input file contains other property labels in addition to the selected target, enter those columns in the optional **exclude** prompt. This prevents using other targets as predictors during chemistry-aware evaluation.
