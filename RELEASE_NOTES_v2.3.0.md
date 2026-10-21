# Experiment-Predictium v2.3.0-VM-RR

Release date: 19 October 2026

## Overview

This release consolidates the MOF-specific development carried out during the 2026 Virtual Mobility activity "MOF-Predictium: A No-Code Machine Learning Platform for MOF Property Prediction, Benchmarking, and High-Throughput Screening Using CoRE MOF DB" within COST Action CA22147 - EU4MOFs.

## Main additions

- MOF-Predictium as an eighth model option, combining 1D convolution, multi-head self-attention, and gradient-boosted residual correction.
- Benchmarking with 80/20 holdout evaluation, 5-fold cross-validation, RMSE, MAE, MAPE, and R2.
- Advanced MOF VM Tools for multi-target analysis, Isolation Forest anomaly screening, data augmentation, ensemble uncertainty, learning curves, OOD assessment, and chemistry-aware stratified evaluation.
- High-throughput candidate screening, PCA/UMAP property-space visualization, Pareto-front analysis, and inverse-design support.
- MOF-NP Round-Robin and Inter-Laboratory Reproducibility module for common templates, automated multi-laboratory data merging, provenance tracking, matched-condition analysis, within-lab and between-lab variability, ICC(2,1), protocol metadata review, and discordance flags.
- Updated documentation, tutorial data, reproducibility guidance, screenshots, and COST/EU4MOFs acknowledgement.

## Scientific scope

The round-robin functionality is a research-software extension inspired by scientific discussions within EU4MOFs. It is not an officially adopted EU4MOFs toxicology protocol, and this release does not claim that a future experimental round-robin study has already been performed.

Scientific studies using CoRE MOF or other external datasets should document the exact dataset release, accessible structures, property-label availability, curation, provenance, filtering rules, and split design used for the reported analysis.

## Funding acknowledgement

The MOF-specific development of Experiment-Predictium was supported through a Virtual Mobility Grant within COST Action CA22147 - EU4MOFs (grant reference E-COST-GRANT-CA22147-6e4f1131). COST is supported by the European Union.

## Installation

See `README.md` for source installation. A Windows release asset contains `ExperimentPredictorApp.exe` and the required application files. The current Windows build still requires a compatible .NET 9 Desktop Runtime and a Python installation with packages from `python_scripts/requirements.txt`.


## Archiving and citation

- Release DOI: https://doi.org/10.5281/zenodo.23462328
- Concept DOI (all versions): https://doi.org/10.5281/zenodo.18215803
- Repository: https://github.com/abscienceai/Experiment-Predictium
