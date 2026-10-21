# MOF-NP Round-Robin & Inter-Laboratory Reproducibility Guide

## Purpose and status

This module provides a **local, no-code data coordination and analysis workflow** for a future multi-laboratory MOF nanoparticle (MOF-NP) toxicity round-robin study. It was added as a WG4-aligned extension after scientific discussion around a possible future inter-laboratory toxicity study.

It is **not an official EU4MOFs protocol**, does not decide whether a MOF is safe/toxic, and does not replace toxicology expertise, assay-specific quality control, or a protocol agreed by the participating laboratories.

Scientific background for the data fields is consistent with the need to record MOF nanoparticle physical/synthetic/biological identity and exposure context discussed in:

R. Ettlinger et al., *Toxicity of metal-organic framework nanoparticles: from essential analyses to potential applications*, **Chemical Society Reviews** 51 (2022) 464-484. DOI: 10.1039/D1CS00918D.

## What the module does

The module supports four steps:

1. **Create a common laboratory template.**
2. **Merge files returned by independent laboratories without manual copy/paste.**
3. **Check metadata completeness, unit compatibility and provenance.**
4. **Analyse inter-laboratory reproducibility only within matched experimental conditions.**

The program keeps every source file name and source row so a result can be traced back to the submitted laboratory file.

---

## Opening the module

1. Start Experiment-Predictium.
2. Click **MOF VM Tools**.
3. Click **9. MOF-NP Round Robin**.

The Round-Robin window contains:

- **Create Lab Template**
- **Merge + Analyze Labs**
- **Analyze Existing Merge**
- **Synthetic Self-Test**
- **Open Round-Robin Guide**
- **Open Results Folder**

---

# Recommended study workflow

## Step 1. Freeze the common protocol before data collection

Before distributing the template, the scientific study team should agree the protocol. At minimum, define:

- shared `study_id`;
- shared material identifier and physical `batch_id`;
- MOF identity;
- nanoparticle preparation/dispersion procedure;
- biological model / cell line;
- assay and endpoint;
- dose levels and units;
- exposure times;
- control definition;
- replicate definition;
- common `protocol_version`.

The software records and checks these values. It does not choose them for the study team.

## Step 2. Create the template

Click **Create Lab Template**.

The program creates:

- `round_robin_template.xlsx`
- `round_robin_template.csv`
- `round_robin_data_dictionary.csv`
- `HOW_TO_FILL_TEMPLATE.txt`

The Excel template contains a **Data** sheet and a **Data_Dictionary** sheet.

Send the same blank template to every laboratory.

## Step 3. Rules for each laboratory

Each laboratory should:

- use its assigned pseudonymous `lab_id`, for example `Lab_A`, `Lab_B` or `Lab_C`;
- keep the coordinator-supplied `study_id`, `material_id`, `batch_id` and `protocol_version` unchanged;
- enter **one replicate measurement per row**;
- keep raw `result_value` and `result_unit`;
- avoid averaging replicates before submission unless the common protocol explicitly requires this;
- document deviations in `notes` rather than silently modifying the common identifiers;
- return the completed CSV/XLSX file to the study coordinator.

Suggested file names:

```text
RR2027_Lab_A.xlsx
RR2027_Lab_B.xlsx
RR2027_Lab_C.xlsx
```

Do not encode personal names in `lab_id` when blinded analysis is desired.

---

# Main data fields

## Study/material identity

- `study_id`
- `lab_id`
- `material_id`
- `batch_id`
- `mof_name`
- `metal_node`
- `linker`
- `surface_modification`

## Nanoparticle characterisation

- `particle_size_nm`
- `hydrodynamic_size_nm`
- `pdi`
- `zeta_potential_mv`
- `morphology`

## Biological/exposure context

- `medium`
- `serum_percent`
- `cell_line`
- `biological_model_type`
- `dose_value`
- `dose_unit`
- `exposure_h`

## Assay/result

- `assay`
- `endpoint`
- `replicate_id`
- `result_value`
- `result_unit`
- `control_type`
- `control_value`
- `normalized_result_percent` (optional)

## Protocol/provenance

- `protocol_version`
- `instrument`
- `notes`

The full definitions are automatically exported in `round_robin_data_dictionary.csv`.

---

# How incoming laboratory files are combined

## Recommended method: do not manually copy/paste

When files return from the laboratories:

1. Open **MOF-NP Round Robin**.
2. Click **Merge + Analyze Labs**.
3. In the file picker, select **all laboratory CSV/XLSX files at the same time**.
4. The program creates a study folder containing `01_merge` and `02_analysis`.

### `01_merge` outputs

`merged_round_robin.csv`
: The combined record-level dataset.

`metadata_completeness.csv`
: Required/recommended metadata completeness for each submitted row.

`merge_report.json`
: Number of files, rows, laboratories, missing required columns and column-normalisation information for every source file.

`MERGE_SUMMARY.md`
: Human-readable merge summary.

### Provenance is never discarded

Two columns are added automatically:

- `source_file`
- `source_row`

This allows any merged record to be traced back to the exact incoming file and original row.

## Column-name harmonisation

The standard template should be preferred. The merger also recognises a limited set of common aliases. For example:

```text
Laboratory ID  -> lab_id
Concentration  -> dose_value
Concentration Unit -> dose_unit
Exposure Time h -> exposure_h
Result -> result_value
```

The merger deliberately does **not** attempt unlimited fuzzy matching, because silently guessing scientific column meanings can create data errors.

## Missing lab IDs

If a file contains no usable `lab_id`, the file name is used as a temporary laboratory identifier and this is recorded in the merge report. The coordinator should review/correct this before final analysis.

## Dose-unit harmonisation

Original `dose_value` and `dose_unit` are always retained.

A separate `dose_ug_ml` field is created only when conversion is unambiguous for supported mass/volume units. Examples:

```text
1 ug/mL = 1 mg/L
1 ng/mL = 0.001 ug/mL
1 mg/mL = 1000 ug/mL
1 g/L = 1000 ug/mL
```

Unsupported units are **not guessed**. They remain in their original form and should be harmonised by the study team if scientifically appropriate.

## Result harmonisation

The program creates `analysis_value` and `analysis_unit`:

- if `normalized_result_percent` is supplied, it is used as the analysis value in percent;
- otherwise the original `result_value` and `result_unit` are used;
- a value explicitly supplied as a fraction/proportion is converted to percent;
- different result units are not treated as the same matched condition.

Do not put values into `normalized_result_percent` merely to force comparability. Use it only if the normalisation is scientifically justified by the agreed protocol.

---

# How laboratories are compared

The program does **not** pool all results together. It first creates matched conditions using:

- `study_id`
- `material_id`
- `batch_id`
- `cell_line`
- `assay`
- `endpoint`
- harmonised dose
- `exposure_h`
- analysis unit

Example: a 24 h CCK-8 viability result at 50 ug/mL is not automatically compared with a 48 h MTT result at 100 ug/mL.

For every matched laboratory-condition combination it calculates:

- replicate count (`n`);
- laboratory mean;
- laboratory SD;
- within-laboratory CV (%).

For every matched condition it calculates:

- number of participating laboratories;
- total measurements;
- inter-laboratory mean;
- between-laboratory SD;
- between-laboratory CV (%);
- median and IQR of laboratory means.

---

# Agreement and review metrics

## Standardised deviation

A laboratory mean is compared with the means of the other laboratories inside the same matched condition.

## Robust deviation score

A median/MAD-based robust z-score is also calculated. It is less sensitive to one extreme laboratory mean than the ordinary mean/SD z-score.

## Within-laboratory CV

High variation among a laboratory's replicates can be surfaced for review.

## ICC(2,1)

Where enough complete matched conditions exist, the program reports a two-way random-effects absolute-agreement single-measure ICC, commonly referred to as ICC(2,1).

ICC is calculated **within endpoint + analysis-unit strata**, not by mixing incompatible endpoints or units.

If too few complete matched conditions are available, ICC is left unavailable rather than fabricated.

## Pairwise laboratory correlations

Pearson correlation is reported for laboratory pairs only when at least three overlapping matched conditions exist.

## Protocol/metadata differences

Within otherwise matched conditions, the program checks fields such as:

- `protocol_version`
- `medium`
- `serum_percent`
- `control_type`

Missing values or differences from the modal value are exported for review.

---

# Analysis output files

The `02_analysis` directory includes:

- `analysis_input_harmonised.csv`
- `lab_condition_summary.csv`
- `condition_summary.csv`
- `lab_summary.csv`
- `discordance_review_flags.csv`
- `protocol_metadata_deviations.csv`
- `icc_by_endpoint_unit.csv`
- `pairwise_lab_correlations.csv`
- `agreement_summary.json`
- `ROUND_ROBIN_ANALYSIS_SUMMARY.md`
- `interlab_robust_z_by_lab.png`
- `between_lab_cv_by_condition.png`
- `representative_condition_lab_means.png`

If records are missing a laboratory ID or numeric analysis result, they are excluded from analysis and written to a separate excluded-row file when applicable.

---

# Understanding `discordance_review_flags.csv`

A flag is **not proof that a laboratory made an error**.

The current screening rules surface a laboratory-condition pair when one or more of the following occurs:

- absolute robust z-score >= 3.5;
- absolute standard between-laboratory z-score >= 2;
- within-laboratory CV >= 20%.

These are review triggers only. A flagged result should lead to checks such as:

- raw instrument file;
- control performance;
- plate/layout effects;
- nanoparticle dispersion preparation;
- particle aggregation;
- medium/serum differences;
- timing;
- batch identity;
- assay interference;
- transcription/unit error;
- documented protocol deviation.

Thresholds are not universal biological acceptance criteria and should not be treated as pass/fail limits unless the round-robin team formally adopts them in advance.

---

# Example data flow

```text
                 Common SOP + common template
                           |
          +----------------+----------------+
          |                |                |
        Lab A            Lab B            Lab C
       A.xlsx            B.xlsx            C.xlsx
          |                |                |
          +----------------+----------------+
                           |
                    Merge + provenance
                           |
        metadata_completeness.csv + merge_report.json
                           |
                 merged_round_robin.csv
                           |
             matched-condition construction
                           |
       within-lab + between-lab reproducibility analysis
                           |
        CV / robust-z / ICC / pairwise agreement / plots
                           |
                 Scientific team review
```

---

# Synthetic Self-Test

Use **Synthetic Self-Test** to check that the software path works.

It creates a deterministic five-laboratory demonstration dataset. One laboratory contains an intentionally strong systematic offset and one laboratory contains a deliberate medium difference so that the review outputs can be exercised.

The synthetic dataset is **software validation only**. It is not experimental toxicity evidence and must never be reported as an EU4MOFs round-robin result.

---

# Practical rules for a real 2027 study

For the cleanest multi-laboratory comparison:

1. Send the **same material batch** whenever scientifically/logistically possible.
2. Freeze an SOP version before starting.
3. Define biological versus technical replicates explicitly.
4. Use predefined dose and exposure-time grids.
5. Define the primary endpoint and assay before data collection.
6. Record particle characterisation and dispersion conditions.
7. Use pseudonymous lab IDs if the first analysis should be blinded.
8. Do not edit another laboratory's raw values in the merged file. Corrections should be traceable.
9. Keep the original submitted files unchanged in a read-only archive.
10. Interpret statistical discordance jointly with experimental metadata and expert judgement.

## Privacy/local processing

The round-robin module processes files locally through Experiment-Predictium. The module itself does not upload participating laboratories' data to an external service.
