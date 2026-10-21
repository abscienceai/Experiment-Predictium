#!/usr/bin/env python3
"""MOF-NP round-robin data harmonisation and inter-laboratory reproducibility tools.

This module is intentionally conservative: it standardises incoming laboratory files,
keeps provenance, reports metadata completeness, and computes descriptive/agreement
statistics only for matched experimental conditions. It does not decide whether a
material is "safe" or replace toxicology expertise or a pre-agreed experimental protocol.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from itertools import combinations
from pathlib import Path
from typing import Dict, Iterable, List, Sequence, Tuple

import numpy as np
import pandas as pd

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


TEMPLATE_COLUMNS: List[str] = [
    "study_id",
    "lab_id",
    "material_id",
    "batch_id",
    "mof_name",
    "metal_node",
    "linker",
    "surface_modification",
    "particle_size_nm",
    "hydrodynamic_size_nm",
    "pdi",
    "zeta_potential_mv",
    "morphology",
    "medium",
    "serum_percent",
    "cell_line",
    "biological_model_type",
    "assay",
    "endpoint",
    "dose_value",
    "dose_unit",
    "exposure_h",
    "replicate_id",
    "result_value",
    "result_unit",
    "control_type",
    "control_value",
    "normalized_result_percent",
    "protocol_version",
    "instrument",
    "notes",
]

REQUIRED_COLUMNS: List[str] = [
    "study_id", "lab_id", "material_id", "batch_id", "cell_line", "assay",
    "endpoint", "dose_value", "dose_unit", "exposure_h", "replicate_id",
    "result_value", "result_unit", "protocol_version",
]

RECOMMENDED_COLUMNS: List[str] = [
    "mof_name", "metal_node", "linker", "particle_size_nm", "morphology",
    "medium", "serum_percent", "control_type", "control_value",
]

DATA_DICTIONARY: List[Tuple[str, str, str, str]] = [
    ("study_id", "Shared round-robin study identifier", "text", "required"),
    ("lab_id", "Pseudonymous laboratory identifier, e.g. Lab_A", "text", "required"),
    ("material_id", "Shared material identifier used by every laboratory", "text", "required"),
    ("batch_id", "Shared physical batch identifier", "text", "required"),
    ("mof_name", "MOF name/family, e.g. ZIF-8 or UiO-66", "text", "recommended"),
    ("metal_node", "Metal/node descriptor", "text", "recommended"),
    ("linker", "Organic linker", "text", "recommended"),
    ("surface_modification", "Coating or external surface modification", "text", "optional"),
    ("particle_size_nm", "Primary particle size in nm", "number", "recommended"),
    ("hydrodynamic_size_nm", "Hydrodynamic diameter in biological medium", "number", "optional"),
    ("pdi", "Polydispersity index", "number", "optional"),
    ("zeta_potential_mv", "Zeta potential in mV", "number", "optional"),
    ("morphology", "Particle morphology/shape", "text", "recommended"),
    ("medium", "Biological/culture medium", "text", "recommended"),
    ("serum_percent", "Serum percentage in the exposure medium", "number", "recommended"),
    ("cell_line", "Cell line or primary-cell model", "text", "required"),
    ("biological_model_type", "e.g. in vitro, ex vivo", "text", "optional"),
    ("assay", "Assay name, e.g. MTT, CCK-8, LDH, ROS", "text", "required"),
    ("endpoint", "Measured endpoint, e.g. cell_viability, LDH_release, ROS", "text", "required"),
    ("dose_value", "Nominal dose/concentration numeric value", "number", "required"),
    ("dose_unit", "Dose unit; supported automatic conversions include ug/mL, mg/L, ng/mL, mg/mL, g/L", "text", "required"),
    ("exposure_h", "Exposure duration in hours", "number", "required"),
    ("replicate_id", "Biological/technical replicate identifier according to the agreed protocol", "text", "required"),
    ("result_value", "Raw or protocol-defined reported result", "number", "required"),
    ("result_unit", "Result unit, e.g. %, AU, fold_change", "text", "required"),
    ("control_type", "Control definition", "text", "recommended"),
    ("control_value", "Control measurement where applicable", "number", "recommended"),
    ("normalized_result_percent", "Optional harmonised percentage result; use only when scientifically valid", "number", "optional"),
    ("protocol_version", "Common protocol/SOP version", "text", "required"),
    ("instrument", "Instrument/model used for readout", "text", "optional"),
    ("notes", "Free-text deviations or observations", "text", "optional"),
]

ALIASES: Dict[str, str] = {
    "study": "study_id", "studyid": "study_id", "round_robin_id": "study_id",
    "lab": "lab_id", "laboratory": "lab_id", "laboratory_id": "lab_id", "labid": "lab_id",
    "material": "material_id", "sample_id": "material_id", "sample": "material_id",
    "batch": "batch_id", "batchid": "batch_id", "lot_id": "batch_id",
    "mof": "mof_name", "mof_id": "mof_name", "mofname": "mof_name",
    "metal": "metal_node", "metalnode": "metal_node",
    "surface_coating": "surface_modification", "coating": "surface_modification",
    "particle_size": "particle_size_nm", "size_nm": "particle_size_nm",
    "hydrodynamic_size": "hydrodynamic_size_nm", "hydrodynamic_diameter_nm": "hydrodynamic_size_nm",
    "zeta_potential": "zeta_potential_mv", "zeta_mv": "zeta_potential_mv",
    "cell": "cell_line", "cellline": "cell_line",
    "test": "assay", "assay_type": "assay",
    "concentration": "dose_value", "dose": "dose_value", "concentration_value": "dose_value",
    "concentration_unit": "dose_unit", "dose_units": "dose_unit",
    "exposure_time_h": "exposure_h", "exposure_hours": "exposure_h", "time_h": "exposure_h",
    "replicate": "replicate_id", "rep": "replicate_id",
    "result": "result_value", "value": "result_value", "measurement": "result_value",
    "units": "result_unit", "unit": "result_unit",
    "viability_percent": "normalized_result_percent", "normalized_percent": "normalized_result_percent",
    "sop": "protocol_version", "protocol": "protocol_version", "sop_version": "protocol_version",
}

NUMERIC_COLUMNS = [
    "particle_size_nm", "hydrodynamic_size_nm", "pdi", "zeta_potential_mv",
    "serum_percent", "dose_value", "exposure_h", "result_value", "control_value",
    "normalized_result_percent",
]


def _norm_name(name: object) -> str:
    s = str(name).strip().lower()
    s = s.replace("µ", "u").replace("μ", "u")
    s = re.sub(r"[^a-z0-9]+", "_", s).strip("_")
    return ALIASES.get(s, s)


def _read_table(path: Path) -> pd.DataFrame:
    suffix = path.suffix.lower()
    if suffix in {".xlsx", ".xls"}:
        return pd.read_excel(path, sheet_name=0)
    if suffix == ".tsv":
        return pd.read_csv(path, sep="\t")
    try:
        return pd.read_csv(path, sep=None, engine="python")
    except Exception:
        return pd.read_csv(path)


def _write_json(path: Path, obj: object) -> None:
    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False, default=_json_default), encoding="utf-8")


def _json_default(obj: object):
    if isinstance(obj, (np.integer,)):
        return int(obj)
    if isinstance(obj, (np.floating,)):
        return float(obj)
    if isinstance(obj, (np.ndarray,)):
        return obj.tolist()
    if isinstance(obj, (pd.Timestamp,)):
        return obj.isoformat()
    raise TypeError(type(obj).__name__)


def _nonblank(series: pd.Series) -> pd.Series:
    return series.notna() & series.astype(str).str.strip().ne("") & series.astype(str).str.lower().ne("nan")


def _coerce_numeric(series: pd.Series) -> pd.Series:
    if series.dtype.kind in "biufc":
        return pd.to_numeric(series, errors="coerce")
    s = series.astype(str).str.strip().str.replace(" ", "", regex=False)
    # Decimal comma is accepted when there is no decimal point already.
    s = s.str.replace(r"^(\-?\d+),(\d+)$", r"\1.\2", regex=True)
    return pd.to_numeric(s, errors="coerce")


def canonicalise_frame(df: pd.DataFrame, source_file: str) -> Tuple[pd.DataFrame, Dict[str, object]]:
    original_cols = [str(c) for c in df.columns]
    renamed = [_norm_name(c) for c in df.columns]

    # Avoid silent collisions after alias normalisation.
    seen: Dict[str, int] = {}
    safe_cols: List[str] = []
    collisions: List[str] = []
    for c in renamed:
        if c not in seen:
            seen[c] = 1
            safe_cols.append(c)
        else:
            seen[c] += 1
            new_c = f"{c}__duplicate{seen[c]}"
            safe_cols.append(new_c)
            collisions.append(new_c)
    df = df.copy()
    df.columns = safe_cols

    missing_before = [c for c in REQUIRED_COLUMNS if c not in df.columns]
    lab_id_missing_or_blank = "lab_id" not in df.columns or not _nonblank(df["lab_id"]).any()
    for c in TEMPLATE_COLUMNS:
        if c not in df.columns:
            df[c] = np.nan

    # If no lab ID was supplied, file stem becomes a pseudonymous default.
    stem = re.sub(r"[^A-Za-z0-9_\-]+", "_", Path(source_file).stem).strip("_") or "Lab_unknown"
    if "lab_id" in df.columns:
        mask = ~_nonblank(df["lab_id"])
        df.loc[mask, "lab_id"] = stem

    for c in NUMERIC_COLUMNS:
        if c in df.columns:
            df[c] = _coerce_numeric(df[c])

    df["source_file"] = Path(source_file).name
    df["source_row"] = np.arange(2, len(df) + 2)  # spreadsheet-like row numbering

    extras = [c for c in df.columns if c not in TEMPLATE_COLUMNS + ["source_file", "source_row"]]
    ordered = TEMPLATE_COLUMNS + extras + ["source_file", "source_row"]
    df = df[ordered]

    report = {
        "source_file": Path(source_file).name,
        "rows": int(len(df)),
        "original_columns": original_cols,
        "canonical_columns": safe_cols,
        "missing_required_columns_before_fill": missing_before,
        "duplicate_alias_columns": collisions,
        "derived_lab_id_from_filename": bool(lab_id_missing_or_blank),
    }
    return df, report


def dose_to_ug_ml(value: object, unit: object) -> float:
    try:
        v = float(value)
    except Exception:
        return float("nan")
    u = str(unit).strip().lower().replace("µ", "u").replace("μ", "u").replace(" ", "")
    mapping = {
        "ug/ml": 1.0, "mcg/ml": 1.0, "mg/l": 1.0,
        "ng/ml": 0.001, "ug/l": 0.001,
        "mg/ml": 1000.0, "g/l": 1000.0,
    }
    return v * mapping[u] if u in mapping else float("nan")


def enrich_harmonised_fields(df: pd.DataFrame) -> pd.DataFrame:
    out = df.copy()
    out["dose_ug_ml"] = [dose_to_ug_ml(v, u) for v, u in zip(out["dose_value"], out["dose_unit"])]

    norm_pct = _coerce_numeric(out["normalized_result_percent"])
    raw = _coerce_numeric(out["result_value"])
    unit = out["result_unit"].fillna("").astype(str).str.strip()
    unit_norm = unit.str.lower().str.replace(" ", "", regex=False)

    percent_from_raw = unit_norm.isin(["%", "percent", "percentage", "pct"])
    fraction_from_raw = unit_norm.isin(["fraction", "proportion"])

    analysis_value = raw.copy()
    analysis_unit = unit.replace("", "unspecified").copy()

    use_norm = norm_pct.notna()
    analysis_value.loc[use_norm] = norm_pct.loc[use_norm]
    analysis_unit.loc[use_norm] = "percent"

    use_raw_pct = ~use_norm & percent_from_raw
    analysis_unit.loc[use_raw_pct] = "percent"

    use_fraction = ~use_norm & fraction_from_raw & raw.notna()
    analysis_value.loc[use_fraction] = raw.loc[use_fraction] * 100.0
    analysis_unit.loc[use_fraction] = "percent"

    out["analysis_value"] = analysis_value
    out["analysis_unit"] = analysis_unit

    # Stable dose key: standard concentration where conversion is safe; otherwise preserve raw value+unit.
    def dose_key(row: pd.Series) -> str:
        if pd.notna(row.get("dose_ug_ml")):
            return f"{float(row['dose_ug_ml']):.12g} ug/mL"
        v, u = row.get("dose_value"), row.get("dose_unit")
        return f"{v} {u}" if pd.notna(v) else "<MISSING_DOSE>"

    out["dose_harmonised"] = out.apply(dose_key, axis=1)
    return out


def completeness_table(df: pd.DataFrame) -> pd.DataFrame:
    required_present = pd.DataFrame({c: _nonblank(df[c]) if c in df else False for c in REQUIRED_COLUMNS})
    recommended_present = pd.DataFrame({c: _nonblank(df[c]) if c in df else False for c in RECOMMENDED_COLUMNS})
    out = df[[c for c in ["source_file", "source_row", "study_id", "lab_id", "material_id", "batch_id", "endpoint"] if c in df]].copy()
    out["required_present_n"] = required_present.sum(axis=1)
    out["required_total_n"] = len(REQUIRED_COLUMNS)
    out["required_completeness_percent"] = 100.0 * out["required_present_n"] / len(REQUIRED_COLUMNS)
    out["recommended_present_n"] = recommended_present.sum(axis=1)
    out["recommended_total_n"] = len(RECOMMENDED_COLUMNS)
    out["recommended_completeness_percent"] = 100.0 * out["recommended_present_n"] / len(RECOMMENDED_COLUMNS)
    out["missing_required"] = required_present.apply(lambda r: ",".join([c for c, ok in r.items() if not bool(ok)]), axis=1)
    return out


def create_template(outdir: Path) -> None:
    outdir.mkdir(parents=True, exist_ok=True)
    empty = pd.DataFrame(columns=TEMPLATE_COLUMNS)
    empty.to_csv(outdir / "round_robin_template.csv", index=False)
    dictionary = pd.DataFrame(DATA_DICTIONARY, columns=["column", "meaning", "type", "status"])
    dictionary.to_csv(outdir / "round_robin_data_dictionary.csv", index=False)
    with pd.ExcelWriter(outdir / "round_robin_template.xlsx", engine="openpyxl") as writer:
        empty.to_excel(writer, sheet_name="Data", index=False)
        dictionary.to_excel(writer, sheet_name="Data_Dictionary", index=False)
    (outdir / "HOW_TO_FILL_TEMPLATE.txt").write_text(
        "One row = one replicate measurement under one defined experimental condition.\n"
        "Use the same study_id, material_id, batch_id and protocol_version agreed by the coordinator.\n"
        "Each laboratory should use a pseudonymous lab_id (Lab_A, Lab_B, ...).\n"
        "Do not average replicates before submission unless the common protocol explicitly requires it.\n"
        "Keep result_value/result_unit exactly as measured; normalized_result_percent is optional and should only be filled when scientifically valid.\n",
        encoding="utf-8",
    )


def merge_files(inputs: Sequence[Path], outdir: Path) -> Path:
    outdir.mkdir(parents=True, exist_ok=True)
    frames: List[pd.DataFrame] = []
    reports: List[Dict[str, object]] = []
    for path in inputs:
        raw = _read_table(path)
        canonical, report = canonicalise_frame(raw, str(path))
        canonical = enrich_harmonised_fields(canonical)
        frames.append(canonical)
        reports.append(report)

    if not frames:
        raise ValueError("No input laboratory files were supplied.")

    merged = pd.concat(frames, ignore_index=True, sort=False)
    merged.insert(0, "record_id", [f"RR{idx:07d}" for idx in range(1, len(merged) + 1)])

    comp = completeness_table(merged)
    merged_path = outdir / "merged_round_robin.csv"
    merged.to_csv(merged_path, index=False)
    comp.to_csv(outdir / "metadata_completeness.csv", index=False)

    summary = {
        "n_source_files": len(inputs),
        "n_rows": int(len(merged)),
        "n_labs": int(merged["lab_id"].astype(str).nunique()),
        "source_reports": reports,
        "dose_auto_conversion": "dose_ug_ml is created only for safely recognised mass/volume units; original dose_value/dose_unit are always retained.",
        "result_harmonisation": "analysis_value uses normalized_result_percent when supplied; otherwise result_value. Results with different analysis_unit are never placed in the same matched condition.",
        "mean_required_completeness_percent": float(comp["required_completeness_percent"].mean()) if len(comp) else None,
        "rows_with_all_required_metadata": int((comp["required_completeness_percent"] == 100.0).sum()),
    }
    _write_json(outdir / "merge_report.json", summary)

    readme = [
        "# Round-robin merge report",
        "",
        f"Source files: {len(inputs)}",
        f"Rows: {len(merged)}",
        f"Laboratories: {summary['n_labs']}",
        f"Mean required-metadata completeness: {summary['mean_required_completeness_percent']:.1f}%" if summary["mean_required_completeness_percent"] is not None else "",
        "",
        "The original source file and row number are retained in source_file/source_row.",
        "Automatic unit conversion is intentionally limited. Unsupported units remain unchanged and must be reviewed before inter-laboratory interpretation.",
    ]
    (outdir / "MERGE_SUMMARY.md").write_text("\n".join(readme), encoding="utf-8")
    return merged_path


def _safe_cv(mean: float, sd: float) -> float:
    if not np.isfinite(mean) or abs(mean) < 1e-12 or not np.isfinite(sd):
        return float("nan")
    return 100.0 * sd / abs(mean)


def _icc2_1(matrix: np.ndarray) -> float:
    """Two-way random-effects, absolute-agreement, single-measure ICC(2,1)."""
    x = np.asarray(matrix, dtype=float)
    if x.ndim != 2 or x.shape[0] < 2 or x.shape[1] < 2 or np.isnan(x).any():
        return float("nan")
    n, k = x.shape
    grand = x.mean()
    row_means = x.mean(axis=1)
    col_means = x.mean(axis=0)
    msr = k * np.sum((row_means - grand) ** 2) / (n - 1)
    msc = n * np.sum((col_means - grand) ** 2) / (k - 1)
    residual = x - row_means[:, None] - col_means[None, :] + grand
    mse = np.sum(residual ** 2) / ((n - 1) * (k - 1))
    denom = msr + (k - 1) * mse + (k * (msc - mse) / n)
    if abs(denom) < 1e-12:
        return float("nan")
    return float((msr - mse) / denom)


def _robust_z(values: pd.Series) -> pd.Series:
    x = pd.to_numeric(values, errors="coerce")
    med = x.median()
    mad = (x - med).abs().median()
    if not np.isfinite(mad) or mad < 1e-12:
        return pd.Series(np.nan, index=x.index)
    return 0.67448975 * (x - med) / mad


def _condition_string(row: pd.Series) -> str:
    parts = [
        str(row.get("study_id", "")), str(row.get("material_id", "")), str(row.get("batch_id", "")),
        str(row.get("cell_line", "")), str(row.get("assay", "")), str(row.get("endpoint", "")),
        str(row.get("dose_harmonised", "")), str(row.get("exposure_h", "")), str(row.get("analysis_unit", "")),
    ]
    return " | ".join(parts)


def analyse_round_robin(input_path: Path, outdir: Path) -> None:
    outdir.mkdir(parents=True, exist_ok=True)
    raw = _read_table(input_path)
    # Files produced by merge already have canonical names, but accepting a single lab-style file is useful.
    if "analysis_value" not in raw.columns or "dose_harmonised" not in raw.columns:
        canonical, _ = canonicalise_frame(raw, str(input_path))
        df = enrich_harmonised_fields(canonical)
    else:
        df = raw.copy()
        for c in TEMPLATE_COLUMNS:
            if c not in df.columns:
                df[c] = np.nan
        for c in NUMERIC_COLUMNS + ["dose_ug_ml", "analysis_value"]:
            if c in df.columns:
                df[c] = _coerce_numeric(df[c])

    df["lab_id"] = df["lab_id"].fillna("").astype(str).str.strip()
    df["analysis_value"] = _coerce_numeric(df["analysis_value"])
    valid = df["lab_id"].ne("") & df["analysis_value"].notna()
    excluded = df.loc[~valid].copy()
    df = df.loc[valid].copy()
    if df.empty:
        raise ValueError("No analysable rows: lab_id and numeric analysis_value are required.")

    # Missing key values are explicit rather than silently dropped by groupby.
    key_cols = ["study_id", "material_id", "batch_id", "cell_line", "assay", "endpoint", "dose_harmonised", "exposure_h", "analysis_unit"]
    for c in key_cols:
        if c not in df.columns:
            df[c] = "<MISSING>"
        df[c] = df[c].where(_nonblank(df[c]), "<MISSING>")

    # Stable condition IDs.
    unique_keys = df[key_cols].drop_duplicates().reset_index(drop=True)
    unique_keys["condition_id"] = [f"C{i:04d}" for i in range(1, len(unique_keys) + 1)]
    df = df.merge(unique_keys, on=key_cols, how="left")

    lab_condition = (
        df.groupby(["condition_id", "lab_id"], dropna=False)["analysis_value"]
        .agg(n="count", mean="mean", sd="std")
        .reset_index()
    )
    lab_condition["within_lab_cv_percent"] = [
        _safe_cv(m, s) for m, s in zip(lab_condition["mean"], lab_condition["sd"])
    ]

    condition_rows: List[Dict[str, object]] = []
    z_rows: List[pd.DataFrame] = []
    for condition_id, g in lab_condition.groupby("condition_id", sort=True):
        vals = g["mean"].astype(float)
        n_labs = int(g["lab_id"].nunique())
        mean = float(vals.mean())
        sd = float(vals.std(ddof=1)) if n_labs > 1 else float("nan")
        median = float(vals.median())
        q1 = float(vals.quantile(0.25))
        q3 = float(vals.quantile(0.75))
        condition_rows.append({
            "condition_id": condition_id,
            "n_labs": n_labs,
            "n_measurements": int(g["n"].sum()),
            "interlab_mean": mean,
            "interlab_sd": sd,
            "interlab_cv_percent": _safe_cv(mean, sd),
            "median_lab_mean": median,
            "iqr_lab_mean": q3 - q1,
        })
        gz = g.copy()
        gz["between_lab_z"] = (vals - mean) / sd if np.isfinite(sd) and sd > 0 else np.nan
        gz["robust_z"] = _robust_z(vals)
        z_rows.append(gz)

    condition_summary = pd.DataFrame(condition_rows).merge(unique_keys, on="condition_id", how="left")
    lab_condition_z = pd.concat(z_rows, ignore_index=True)
    lab_condition_z = lab_condition_z.merge(unique_keys, on="condition_id", how="left")

    # Review flags are intentionally labelled as screening flags, not definitive outliers.
    flag_mask = (
        lab_condition_z["robust_z"].abs().ge(3.5)
        | lab_condition_z["between_lab_z"].abs().ge(2.0)
        | lab_condition_z["within_lab_cv_percent"].ge(20.0)
    )
    flags = lab_condition_z.loc[flag_mask].copy()
    def reason(row: pd.Series) -> str:
        reasons = []
        if pd.notna(row["robust_z"]) and abs(float(row["robust_z"])) >= 3.5:
            reasons.append("robust inter-lab deviation")
        if pd.notna(row["between_lab_z"]) and abs(float(row["between_lab_z"])) >= 2.0:
            reasons.append("standardised inter-lab deviation")
        if pd.notna(row["within_lab_cv_percent"]) and float(row["within_lab_cv_percent"]) >= 20.0:
            reasons.append("high within-lab CV (screening threshold 20%)")
        return "; ".join(reasons)
    if not flags.empty:
        flags["review_reason"] = flags.apply(reason, axis=1)

    # Protocol/metadata differences under otherwise matched conditions.
    protocol_fields = ["protocol_version", "medium", "serum_percent", "control_type"]
    protocol_rows: List[Dict[str, object]] = []
    for condition_id, cg in df.groupby("condition_id", sort=True):
        for field in protocol_fields:
            if field not in cg.columns:
                continue
            by_lab = cg.groupby("lab_id", dropna=False)[field].agg(lambda s: next((str(v).strip() for v in s if pd.notna(v) and str(v).strip()), "<MISSING>"))
            nonmissing = by_lab[by_lab != "<MISSING>"]
            mode = nonmissing.mode().iloc[0] if not nonmissing.empty else "<MISSING>"
            for lab, value in by_lab.items():
                if value == "<MISSING>" or (mode != "<MISSING>" and value != mode):
                    protocol_rows.append({
                        "condition_id": condition_id,
                        "lab_id": lab,
                        "field": field,
                        "observed": value,
                        "modal_value": mode,
                        "status": "missing" if value == "<MISSING>" else "differs_from_modal",
                    })
    protocol_deviations = pd.DataFrame(protocol_rows)

    # ICC(2,1) and pairwise Pearson agreement are calculated only within endpoint+unit strata.
    icc_rows: List[Dict[str, object]] = []
    pair_rows: List[Dict[str, object]] = []
    strata = lab_condition_z[["condition_id", "endpoint", "analysis_unit"]].drop_duplicates()
    means_for_agreement = lab_condition_z.merge(strata, on=["condition_id", "endpoint", "analysis_unit"], how="left")
    for (endpoint, unit), sg in means_for_agreement.groupby(["endpoint", "analysis_unit"], dropna=False):
        pivot = sg.pivot_table(index="condition_id", columns="lab_id", values="mean", aggfunc="mean")
        complete = pivot.dropna(axis=0, how="any")
        icc = _icc2_1(complete.to_numpy()) if complete.shape[0] >= 2 and complete.shape[1] >= 2 else float("nan")
        icc_rows.append({
            "endpoint": endpoint,
            "analysis_unit": unit,
            "n_labs": int(pivot.shape[1]),
            "n_conditions_total": int(pivot.shape[0]),
            "n_complete_conditions_for_icc": int(complete.shape[0]),
            "icc_2_1_absolute_agreement": icc,
        })
        for a, b in combinations(pivot.columns, 2):
            pair = pivot[[a, b]].dropna()
            r = float(pair[a].corr(pair[b])) if len(pair) >= 3 and pair[a].std() > 0 and pair[b].std() > 0 else float("nan")
            pair_rows.append({
                "endpoint": endpoint, "analysis_unit": unit,
                "lab_a": a, "lab_b": b, "n_matched_conditions": int(len(pair)),
                "pearson_r": r,
            })

    icc_df = pd.DataFrame(icc_rows)
    pair_df = pd.DataFrame(pair_rows)

    # Per-lab summary uses standardised within-condition deviations to avoid pooling raw endpoints/units.
    lab_summary = (
        lab_condition_z.groupby("lab_id", dropna=False)
        .agg(
            matched_conditions=("condition_id", "nunique"),
            total_replicates=("n", "sum"),
            median_within_lab_cv_percent=("within_lab_cv_percent", "median"),
            mean_between_lab_z=("between_lab_z", "mean"),
            median_robust_z=("robust_z", "median"),
        )
        .reset_index()
    )

    # Write tables.
    df.to_csv(outdir / "analysis_input_harmonised.csv", index=False)
    if not excluded.empty:
        excluded.to_csv(outdir / "excluded_rows_missing_lab_or_numeric_result.csv", index=False)
    lab_condition_z.to_csv(outdir / "lab_condition_summary.csv", index=False)
    condition_summary.to_csv(outdir / "condition_summary.csv", index=False)
    lab_summary.to_csv(outdir / "lab_summary.csv", index=False)
    flags.to_csv(outdir / "discordance_review_flags.csv", index=False)
    protocol_deviations.to_csv(outdir / "protocol_metadata_deviations.csv", index=False)
    icc_df.to_csv(outdir / "icc_by_endpoint_unit.csv", index=False)
    pair_df.to_csv(outdir / "pairwise_lab_correlations.csv", index=False)

    # Plot 1: distribution of within-condition robust z scores by lab.
    plot_df = lab_condition_z.dropna(subset=["robust_z"]).copy()
    if not plot_df.empty:
        labs = sorted(plot_df["lab_id"].unique().tolist())
        arrays = [plot_df.loc[plot_df["lab_id"] == lab, "robust_z"].to_numpy() for lab in labs]
        fig, ax = plt.subplots(figsize=(max(7, len(labs) * 1.1), 5))
        ax.boxplot(arrays, labels=labs, showmeans=True)
        ax.axhline(0.0, linewidth=1)
        ax.axhline(3.5, linestyle="--", linewidth=1)
        ax.axhline(-3.5, linestyle="--", linewidth=1)
        ax.set_ylabel("Robust within-condition z-score")
        ax.set_title("Inter-laboratory deviation distribution")
        ax.tick_params(axis="x", rotation=45)
        fig.tight_layout()
        fig.savefig(outdir / "interlab_robust_z_by_lab.png", dpi=160)
        plt.close(fig)

    # Plot 2: between-lab CV by condition (only where >=2 labs and finite CV).
    cvplot = condition_summary[(condition_summary["n_labs"] >= 2) & condition_summary["interlab_cv_percent"].notna()].copy()
    if not cvplot.empty:
        cvplot = cvplot.sort_values("interlab_cv_percent", ascending=False).head(30)
        fig, ax = plt.subplots(figsize=(10, max(5, 0.28 * len(cvplot) + 2)))
        ax.barh(cvplot["condition_id"].astype(str), cvplot["interlab_cv_percent"].astype(float))
        ax.invert_yaxis()
        ax.set_xlabel("Between-laboratory CV (%)")
        ax.set_ylabel("Matched condition")
        ax.set_title("Largest between-laboratory variability among matched conditions")
        fig.tight_layout()
        fig.savefig(outdir / "between_lab_cv_by_condition.png", dpi=160)
        plt.close(fig)

    # Plot 3: one representative condition with the largest number of labs, then measurements.
    if not condition_summary.empty:
        representative = condition_summary.sort_values(["n_labs", "n_measurements"], ascending=False).iloc[0]
        rid = representative["condition_id"]
        rg = lab_condition_z[lab_condition_z["condition_id"] == rid].sort_values("lab_id")
        if len(rg) >= 2:
            fig, ax = plt.subplots(figsize=(8, 5))
            yerr = rg["sd"].fillna(0).to_numpy(dtype=float)
            ax.errorbar(rg["lab_id"].astype(str), rg["mean"].astype(float), yerr=yerr, fmt="o", capsize=4)
            ax.set_ylabel(f"Mean result ({representative['analysis_unit']})")
            ax.set_title(f"Representative matched condition: {rid}")
            ax.tick_params(axis="x", rotation=45)
            fig.tight_layout()
            fig.savefig(outdir / "representative_condition_lab_means.png", dpi=160)
            plt.close(fig)

    agreement = {
        "rows_analysed": int(len(df)),
        "rows_excluded_missing_lab_or_numeric_result": int(len(excluded)),
        "labs": sorted(df["lab_id"].astype(str).unique().tolist()),
        "n_labs": int(df["lab_id"].nunique()),
        "n_matched_conditions": int(df["condition_id"].nunique()),
        "conditions_with_2plus_labs": int((condition_summary["n_labs"] >= 2).sum()),
        "discordance_review_flags": int(len(flags)),
        "protocol_metadata_deviations": int(len(protocol_deviations)),
        "icc_note": "ICC(2,1) is reported only within endpoint+unit strata using conditions complete across all participating labs in that stratum.",
        "flag_note": "Flags are screening signals for review, not proof that a laboratory or measurement is erroneous.",
    }
    _write_json(outdir / "agreement_summary.json", agreement)

    lines = [
        "# MOF-NP Round-Robin Inter-Laboratory Analysis",
        "",
        f"- Rows analysed: {agreement['rows_analysed']}",
        f"- Laboratories: {agreement['n_labs']}",
        f"- Matched conditions: {agreement['n_matched_conditions']}",
        f"- Conditions represented by at least two laboratories: {agreement['conditions_with_2plus_labs']}",
        f"- Discordance review flags: {agreement['discordance_review_flags']}",
        f"- Protocol/metadata deviations: {agreement['protocol_metadata_deviations']}",
        "",
        "## Interpretation safeguards",
        "",
        "- The program compares laboratories only inside matched material/batch/cell/assay/endpoint/dose/exposure/unit conditions.",
        "- CV, z-score, robust-z, ICC and correlations describe agreement/reproducibility; they do not establish biological causality or safety.",
        "- A flagged laboratory-condition pair should be checked against raw data, controls, instrument logs, dispersion preparation and protocol deviations before any conclusion.",
        "- ICC is not reported when there are too few complete matched conditions.",
        "- Experimental protocol and toxicological interpretation remain the responsibility of the round-robin study team.",
        "",
        "## Main output files",
        "",
        "- `lab_condition_summary.csv`: replicate-level data reduced to laboratory means/SD/CV within each matched condition.",
        "- `condition_summary.csv`: between-laboratory mean, SD, CV and IQR for each matched condition.",
        "- `discordance_review_flags.csv`: heuristic review signals only.",
        "- `protocol_metadata_deviations.csv`: missing or differing protocol metadata under matched conditions.",
        "- `icc_by_endpoint_unit.csv`: ICC(2,1) where mathematically supportable.",
        "- `pairwise_lab_correlations.csv`: pairwise Pearson agreement across matched conditions.",
    ]
    (outdir / "ROUND_ROBIN_ANALYSIS_SUMMARY.md").write_text("\n".join(lines), encoding="utf-8")


def create_demo(outdir: Path) -> Path:
    outdir.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(42)
    labs = ["Lab_A", "Lab_B", "Lab_C", "Lab_D", "Lab_E"]
    lab_bias = {"Lab_A": 0.0, "Lab_B": 1.0, "Lab_C": -1.5, "Lab_D": 0.5, "Lab_E": -16.0}
    rows = []
    for lab in labs:
        for dose in [10.0, 50.0, 100.0]:
            for exposure in [24.0, 48.0]:
                base = 100.0 - 0.22 * dose - (6.0 if exposure == 48 else 0.0)
                for rep in range(1, 4):
                    rows.append({
                        "study_id": "DEMO_RR_2027",
                        "lab_id": lab,
                        "material_id": "MAT_ZIF8_001",
                        "batch_id": "BATCH_SHARED_001",
                        "mof_name": "ZIF-8",
                        "metal_node": "Zn",
                        "linker": "2-methylimidazolate",
                        "surface_modification": "none",
                        "particle_size_nm": 90 + rng.normal(0, 3),
                        "hydrodynamic_size_nm": 115 + rng.normal(0, 5),
                        "pdi": 0.18 + rng.normal(0, 0.01),
                        "zeta_potential_mv": -12 + rng.normal(0, 1),
                        "morphology": "rhombic dodecahedral",
                        "medium": "DMEM" if lab != "Lab_C" else "RPMI-1640",
                        "serum_percent": 10,
                        "cell_line": "A549",
                        "biological_model_type": "in vitro",
                        "assay": "CCK-8",
                        "endpoint": "cell_viability",
                        "dose_value": dose,
                        "dose_unit": "ug/mL",
                        "exposure_h": exposure,
                        "replicate_id": f"R{rep}",
                        "result_value": base + lab_bias[lab] + rng.normal(0, 2.0),
                        "result_unit": "%",
                        "control_type": "untreated",
                        "control_value": 100.0,
                        "normalized_result_percent": np.nan,
                        "protocol_version": "RR-SOP-v0.1",
                        "instrument": "DemoReader",
                        "notes": "synthetic demonstration only",
                    })
    df = pd.DataFrame(rows)
    path = outdir / "round_robin_demo_synthetic.csv"
    df.to_csv(path, index=False)
    analyse_round_robin(path, outdir / "analysis")
    return path


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="MOF-NP round-robin data harmonisation and reproducibility analysis")
    sub = p.add_subparsers(dest="command", required=True)

    t = sub.add_parser("template", help="Create standard round-robin data template and dictionary")
    t.add_argument("--outdir", required=True)

    m = sub.add_parser("merge", help="Merge and harmonise multiple laboratory files")
    m.add_argument("--inputs", nargs="+", required=True)
    m.add_argument("--outdir", required=True)

    a = sub.add_parser("analyze", help="Analyse a merged round-robin dataset")
    a.add_argument("--input", required=True)
    a.add_argument("--outdir", required=True)

    d = sub.add_parser("demo", help="Generate and analyse a deterministic synthetic demo")
    d.add_argument("--outdir", required=True)
    return p


def main() -> int:
    args = build_parser().parse_args()
    if args.command == "template":
        create_template(Path(args.outdir))
        print(f"Template created: {args.outdir}")
    elif args.command == "merge":
        path = merge_files([Path(p) for p in args.inputs], Path(args.outdir))
        print(f"Merged dataset: {path}")
    elif args.command == "analyze":
        analyse_round_robin(Path(args.input), Path(args.outdir))
        print(f"Round-robin analysis completed: {args.outdir}")
    elif args.command == "demo":
        path = create_demo(Path(args.outdir))
        print(f"Synthetic demo created and analysed: {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
