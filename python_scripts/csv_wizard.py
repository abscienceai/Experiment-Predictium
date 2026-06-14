"""
CSV Wizard Helper — Experiment-Predictium
Handles CSV inspection, cleaning, and Excel export.
Called by CsvWizardForm.cs with different commands.
"""
import sys, json, os, warnings
warnings.filterwarnings("ignore")
import pandas as pd
import numpy as np

command = sys.argv[1]

# ── COMMAND: inspect ─────────────────────────────────────────────
# Returns column names, dtypes, sample rows, and stats
if command == "inspect":
    csv_path = sys.argv[2]
    df = pd.read_csv(csv_path, nrows=2000, low_memory=False)

    cols = []
    for col in df.columns:
        series  = df[col]
        n_total = len(series)
        n_miss  = int(series.isna().sum() + (series == "unknown").sum()
                      + (series == "").sum())
        # Detect type
        try:
            pd.to_numeric(series.replace("unknown", pd.NA), errors="raise")
            dtype = "numeric"
        except Exception:
            uniq = series.dropna().unique()
            if set(str(v).lower() for v in uniq[:20]).issubset({"yes","no","true","false","1","0"}):
                dtype = "boolean"
            else:
                dtype = "text"

        cols.append({
            "name":        col,
            "dtype":       dtype,
            "n_total":     n_total,
            "n_missing":   n_miss,
            "miss_pct":    round(n_miss / n_total * 100, 1) if n_total > 0 else 0,
            "sample":      str(series.dropna().iloc[0]) if len(series.dropna()) > 0 else ""
        })

    # First 5 rows as preview
    preview = df.head(5).fillna("").astype(str).values.tolist()

    print(json.dumps({
        "n_rows":   len(df),
        "n_cols":   len(df.columns),
        "columns":  cols,
        "preview":  preview
    }))

# ── COMMAND: process ─────────────────────────────────────────────
# Cleans data and exports to Excel
elif command == "process":
    csv_path     = sys.argv[2]
    config_path  = sys.argv[3]   # JSON config file

    with open(config_path, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    feature_cols  = cfg["feature_cols"]    # list of input column names
    target_cols   = cfg["target_cols"]     # list of target column names (one Excel per target)
    rename_map    = cfg.get("rename_map", {})   # {old_name: new_name}
    bool_cols     = cfg.get("bool_cols", [])    # columns to convert Yes/No → 1/0
    miss_strategy = cfg.get("miss_strategy", "drop")  # drop / mean / median
    output_dir    = cfg["output_dir"]

    df = pd.read_csv(csv_path, low_memory=False)

    # Replace "unknown" with NaN
    df.replace("unknown", np.nan, inplace=True)
    df.replace("", np.nan, inplace=True)

    # Convert boolean columns
    for col in bool_cols:
        if col in df.columns:
            df[col] = df[col].map(
                lambda v: 1 if str(v).strip().lower() in ("yes","true","1") else
                          0 if str(v).strip().lower() in ("no","false","0") else np.nan)

    # Convert feature + target cols to numeric
    all_cols = feature_cols + target_cols
    for col in all_cols:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")

    results = []
    for target in target_cols:
        use_cols = feature_cols + [target]
        sub = df[use_cols].copy()

        # Missing value strategy
        if miss_strategy == "drop":
            sub.dropna(inplace=True)
        elif miss_strategy == "mean":
            for c in sub.columns:
                sub[c].fillna(sub[c].mean(), inplace=True)
        elif miss_strategy == "median":
            for c in sub.columns:
                sub[c].fillna(sub[c].median(), inplace=True)

        # Rename columns
        col_rename = {}
        for c in use_cols:
            if c in rename_map:
                col_rename[c] = rename_map[c]
        if col_rename:
            sub.rename(columns=col_rename, inplace=True)

        # Export
        safe_target = rename_map.get(target, target).replace("/","_").replace(" ","_")
        out_path = os.path.join(output_dir, f"MOF_{safe_target}_ExperimentPredictium.xlsx")
        sub.to_excel(out_path, index=False)

        results.append({
            "target":   target,
            "out_path": out_path,
            "n_rows":   len(sub),
            "n_cols":   len(sub.columns)
        })

    print(json.dumps({"status": "ok", "files": results}))

else:
    print(json.dumps({"error": f"Unknown command: {command}"}))
