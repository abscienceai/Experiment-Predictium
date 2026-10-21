"""Run a deterministic end-to-end smoke test for the VM completion modules.

Outputs are written under reproducibility/demo_outputs and are intentionally ignored
by git. The generated data are synthetic and educational only.
"""
from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

import pandas as pd

ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "python_scripts" / "mof_vm_tools.py"
OUT = ROOT / "reproducibility" / "demo_outputs"


def run(args):
    cmd = [sys.executable, str(TOOL)] + args
    print("RUN:", " ".join(map(str, cmd)), flush=True)
    cp = subprocess.run(cmd, cwd=ROOT, text=True, capture_output=True)
    print(cp.stdout.strip(), flush=True)
    if cp.returncode != 0:
        print(cp.stderr, flush=True)
        raise RuntimeError("Command failed: " + " ".join(args))


def main():
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)

    tutorial_dir = OUT / "tutorial_source"
    run(["tutorial", "--outdir", str(tutorial_dir), "--rows", "80"])
    tutorial = tutorial_dir / "mof_tutorial_dataset.csv"
    df = pd.read_csv(tutorial)

    feature_cols = [
        "pore_diameter_A", "surface_area_m2g", "pore_volume_cm3g",
        "density_gcm3", "metal_electronegativity", "linker_hydrophilicity"
    ]
    train = df[feature_cols + ["water_stability_score"]]
    train_path = OUT / "demo_training_no_header.csv"
    train.to_csv(train_path, index=False, header=False)
    candidates = df[["mof_id", "topology", "metal_node", "linker_family"] + feature_cols]
    candidate_path = OUT / "demo_candidates.csv"
    candidates.to_csv(candidate_path, index=False)

    run(["robustness", "--input", str(train_path), "--outdir", str(OUT / "robustness")])
    run(["multitarget", "--input", str(tutorial), "--targets", "3", "--outdir", str(OUT / "multitarget")])
    run(["stratified", "--input", str(tutorial), "--target", "water_stability_score",
         "--groups", "topology,metal_node,linker_family",
         "--exclude", "gas_uptake_mmol_g,toxicity_proxy_demo",
         "--outdir", str(OUT / "stratified")])
    run(["screen", "--training", str(train_path), "--candidates", str(candidate_path),
         "--model", "MOF_PREDICTIUM", "--direction", "max", "--outdir", str(OUT / "screening")])
    run(["map", "--input", str(tutorial), "--color", "water_stability_score", "--outdir", str(OUT / "maps")])
    run(["pareto", "--input", str(tutorial),
         "--objectives", "water_stability_score,gas_uptake_mmol_g,toxicity_proxy_demo",
         "--directions", "max,max,min", "--outdir", str(OUT / "pareto")])
    desired_target = float(df["water_stability_score"].quantile(0.85))
    run(["inverse", "--training", str(train_path), "--candidates", str(candidate_path),
         "--target-value", str(desired_target), "--top", "20", "--outdir", str(OUT / "inverse")])

    summary = {
        "status": "PASS",
        "tutorial_rows": len(df),
        "modules": ["robustness", "multitarget", "stratified", "screening", "maps", "pareto", "inverse"],
        "umap_note": "UMAP is produced when umap-learn is installed; otherwise PCA runs and an installation notice is written."
    }
    (OUT / "demo_validation_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2), flush=True)


if __name__ == "__main__":
    main()
