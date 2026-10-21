# Experiment-Predictium v2.3.0-VM-RR

CA22147 EU4MOFs Virtual Mobility software release with MOF-NP inter-laboratory round-robin support.

## Added

- Dedicated **MOF-NP Round-Robin & Inter-Laboratory Reproducibility** no-code window.
- Shared CSV/XLSX laboratory template generator and data dictionary.
- Multi-file CSV/XLSX/TSV merge without manual copy/paste.
- `source_file` and `source_row` provenance retained for every record.
- Conservative column-alias handling and dose-unit harmonisation while retaining originals.
- Required/recommended metadata completeness reporting.
- Matched-condition construction using study/material/batch/cell/assay/endpoint/dose/exposure/unit context.
- Per-lab replicate mean, SD and within-lab CV.
- Per-condition between-laboratory mean, SD, CV, median and IQR.
- Standardised and median/MAD robust deviation scores with review flags.
- Protocol metadata difference checks.
- ICC(2,1) absolute-agreement calculation within endpoint/unit strata when data support it.
- Pairwise laboratory correlations across overlapping matched conditions.
- Reproducibility plots and Markdown/JSON/CSV result summaries.
- Deterministic five-laboratory synthetic self-test.
- `docs/MOF_NP_ROUND_ROBIN_GUIDE.md` with detailed instructions for distributing the template, combining incoming files and interpreting outputs.

## Scientific-scope guardrail

The round-robin module is a software/data-coordination extension, not a completed experimental 2027 round-robin study, an official EU4MOFs protocol, or a validated general-purpose toxicity classifier. Review flags are prompts for investigation, not proof of laboratory error.

## Inherited from v2.2.0-VM

All v2.2.0 VM functions remain: eight-model benchmark including MOF-Predictium, 5-fold CV, MAPE, multi-target, anomaly/augmentation/uncertainty/OOD/learning curves, chemistry-aware evaluation, HT screening, PCA/UMAP, Pareto and inverse design.
