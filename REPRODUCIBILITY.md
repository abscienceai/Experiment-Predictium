# Reproducibility

## Determinism

The VM modules use random seed **42** by default. Cross-validation splits, model seeds, tutorial data and augmentation are deterministic unless the source library itself introduces nondeterministic hardware behavior.

## Python environment

Install:

```bash
pip install -r python_scripts/requirements.txt
```

Main packages: NumPy, pandas, scikit-learn, PyTorch, matplotlib, openpyxl, joblib, SHAP and umap-learn.

## Data leakage controls

- Cross-validation fitting is performed inside each training fold.
- MOF-Predictium standardization is fitted only to the training data used for that fit.
- OOD scalers and distance thresholds are derived from training descriptors.
- Chemistry/group holdout trains without the held-out group.

## Reproducibility demo

Run:

```bash
python reproducibility/run_vm_demo.py
```

The script creates a deterministic tutorial dataset and executes the implemented VM modules. The tutorial data are synthetic and educational only.

## Data-dependent deliverables

The software can process CoRE MOF-derived labelled datasets, but a claim about a specific 43,000+ structure benchmark must only be made after the exact CoRE release, labels, descriptor sources and filtering rules are frozen and the run is actually completed.


## Round-robin module self-test

From the repository root:

```bash
python python_scripts/round_robin.py template --outdir rr_template_test
python python_scripts/round_robin.py demo --outdir rr_demo_test
```

The demo is deterministic synthetic software-validation data only. Expected analysis outputs include `agreement_summary.json`, `lab_condition_summary.csv`, `condition_summary.csv`, `icc_by_endpoint_unit.csv`, `discordance_review_flags.csv` and PNG plots.
