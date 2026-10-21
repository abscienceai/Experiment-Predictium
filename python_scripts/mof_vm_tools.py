"""MOF-Predictium VM completion toolbox.

Implements the approved CA22147 VM work-plan modules that complement the core
8-model benchmark:
- multi-target prediction
- Isolation Forest anomaly detection
- Gaussian-noise and SMOTER-compatible augmentation
- ensemble uncertainty quantification
- learning curves
- OOD detection
- chemistry-aware stratified/group evaluation
- batch high-throughput screening and ranking
- PCA/UMAP maps
- Pareto-front analysis
- inverse-design candidate retrieval in descriptor space
- deterministic tutorial dataset generation

All commands are deterministic (seed=42) and write machine-readable CSV/JSON
outputs plus selected PNG figures into an output directory.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
from pathlib import Path

import joblib
import numpy as np
import pandas as pd

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

from sklearn.ensemble import IsolationForest, RandomForestRegressor, GradientBoostingRegressor
from sklearn.metrics import mean_absolute_error, mean_squared_error, r2_score
from sklearn.model_selection import KFold, LeaveOneGroupOut, learning_curve, train_test_split
from sklearn.multioutput import MultiOutputRegressor
from sklearn.neighbors import NearestNeighbors
from sklearn.preprocessing import StandardScaler
from sklearn.decomposition import PCA

try:
    import umap  # type: ignore
    HAVE_UMAP = True
except Exception:
    HAVE_UMAP = False


SEED = 42
RNG = np.random.default_rng(SEED)


def ensure_dir(path: str | Path) -> Path:
    p = Path(path)
    p.mkdir(parents=True, exist_ok=True)
    return p


def read_table(path: str | Path, header: str | int | None = "infer") -> pd.DataFrame:
    path = str(path)
    ext = Path(path).suffix.lower()
    if ext in {".xlsx", ".xls"}:
        return pd.read_excel(path, header=header)
    if ext in {".tsv", ".txt"}:
        return pd.read_csv(path, sep="\t", header=header)
    return pd.read_csv(path, header=header)


def numeric_matrix(df: pd.DataFrame, exclude: set[str] | None = None):
    exclude = exclude or set()
    cols = [c for c in df.columns if str(c) not in exclude]
    numeric = df[cols].apply(pd.to_numeric, errors="coerce")
    valid_cols = [c for c in numeric.columns if numeric[c].notna().sum() == len(numeric)]
    if not valid_cols:
        raise ValueError("No fully numeric feature columns were found.")
    return numeric[valid_cols].to_numpy(float), [str(c) for c in valid_cols]


def metrics(y_true, y_pred):
    y_true = np.asarray(y_true, dtype=float)
    y_pred = np.asarray(y_pred, dtype=float)
    rmse = float(np.sqrt(mean_squared_error(y_true, y_pred)))
    mae = float(mean_absolute_error(y_true, y_pred))
    r2 = float(r2_score(y_true, y_pred)) if len(y_true) > 1 else float("nan")
    denom = np.maximum(np.abs(y_true), 1e-8)
    mape = float(np.mean(np.abs((y_true - y_pred) / denom)) * 100.0)
    return {"rmse": rmse, "mae": mae, "mape": mape, "r2": r2}


def json_dump(obj, path: Path):
    path.write_text(json.dumps(obj, indent=2, allow_nan=False), encoding="utf-8")


# ---------- MOF-Predictium hybrid batch model ----------
def fit_mof_predictium_batch(X, y, epochs=180):
    """Fit the VM hybrid model. PyTorch is imported lazily so non-neural tools start fast."""
    try:
        import torch
        import torch.nn as nn
    except Exception as exc:
        raise RuntimeError("PyTorch is required for MOF-Predictium screening.") from exc

    class MOFPredictiumNet(nn.Module):
        def __init__(self, n_features: int):
            super().__init__()
            self.conv = nn.Conv1d(1, 32, kernel_size=3, padding=1)
            self.attn = nn.MultiheadAttention(embed_dim=32, num_heads=4, batch_first=True)
            self.norm = nn.LayerNorm(32)
            self.fc1 = nn.Linear(32, 32)
            self.fc2 = nn.Linear(32, 1)

        def forward(self, x):
            z = torch.relu(self.conv(x.unsqueeze(1))).permute(0, 2, 1)
            a, _ = self.attn(z, z, z, need_weights=False)
            z = self.norm(z + a)
            z = z.mean(dim=1)
            z = torch.relu(self.fc1(z))
            return self.fc2(z)

    np.random.seed(SEED)
    torch.manual_seed(SEED)
    scaler = StandardScaler().fit(X)
    Xs = scaler.transform(X)
    xt = torch.tensor(Xs, dtype=torch.float32)
    yt = torch.tensor(y, dtype=torch.float32).view(-1, 1)
    net = MOFPredictiumNet(X.shape[1])
    opt = torch.optim.Adam(net.parameters(), lr=0.005)
    loss_fn = nn.MSELoss()
    net.train()
    for _ in range(epochs):
        pred = net(xt)
        loss = loss_fn(pred, yt)
        opt.zero_grad(); loss.backward(); opt.step()
    net.eval()
    with torch.no_grad():
        base_train = net(xt).squeeze(1).cpu().numpy()
    residual = y - base_train
    booster = GradientBoostingRegressor(
        n_estimators=150, learning_rate=0.03, max_depth=2,
        random_state=SEED, loss="huber"
    ).fit(Xs, residual)
    return scaler, net, booster


def predict_mof_batch(payload, X):
    import torch
    scaler, net, booster = payload
    Xs = scaler.transform(X)
    with torch.no_grad():
        base = net(torch.tensor(Xs, dtype=torch.float32)).squeeze(1).cpu().numpy()
    return base + booster.predict(Xs)


# ---------- Milestone 2 ----------
def cmd_robustness(args):
    out = ensure_dir(args.outdir)
    df = read_table(args.input, header=None)
    if df.shape[1] < 2:
        raise ValueError("Input must have at least one feature and one target column.")
    arr = df.apply(pd.to_numeric, errors="raise").to_numpy(float)
    X, y = arr[:, :-1], arr[:, -1]
    n, p = X.shape
    if n < 10:
        raise ValueError("At least 10 rows are recommended for robustness analysis.")

    # Isolation Forest
    iso = IsolationForest(n_estimators=200, contamination="auto", random_state=SEED)
    iso_flag = iso.fit_predict(X)
    anomaly_score = -iso.score_samples(X)  # larger = more anomalous
    anom = pd.DataFrame(X, columns=[f"feature_{i+1}" for i in range(p)])
    anom["target"] = y
    anom["anomaly_score"] = anomaly_score
    anom["is_anomaly"] = iso_flag == -1
    anom.to_csv(out / "isolation_forest_anomalies.csv", index=False)

    # Gaussian augmentation, one synthetic copy per original sample, 1% feature SD
    scale = np.nanstd(X, axis=0, ddof=1)
    scale[~np.isfinite(scale) | (scale == 0)] = 1.0
    X_noise = X + RNG.normal(0.0, 0.01, size=X.shape) * scale
    gauss = np.vstack([np.column_stack([X, y]), np.column_stack([X_noise, y])])
    pd.DataFrame(gauss).to_csv(out / "gaussian_augmented.csv", index=False, header=False)

    # SMOTER-compatible target-tail interpolation.
    # Synthetic samples are generated only within lower/upper target tails.
    q10, q90 = np.quantile(y, [0.10, 0.90])
    tail_idx = np.where((y <= q10) | (y >= q90))[0]
    synth_rows = []
    if len(tail_idx) >= 2:
        low = tail_idx[y[tail_idx] <= q10]
        high = tail_idx[y[tail_idx] >= q90]
        for group in (low, high):
            if len(group) < 2:
                continue
            target_new = max(len(group), int(math.ceil(0.15 * n)))
            for _ in range(target_new):
                a, b = RNG.choice(group, size=2, replace=True)
                lam = float(RNG.random())
                xs = X[a] + lam * (X[b] - X[a])
                ys = y[a] + lam * (y[b] - y[a])
                synth_rows.append(np.r_[xs, ys])
    smoter = np.vstack([np.column_stack([X, y]), np.asarray(synth_rows)]) if synth_rows else np.column_stack([X, y])
    pd.DataFrame(smoter).to_csv(out / "smoter_compatible_augmented.csv", index=False, header=False)

    # Holdout ensemble uncertainty via independent bootstrap forests.
    idx = np.arange(n)
    tr_idx, te_idx = train_test_split(idx, test_size=0.20, random_state=SEED)
    Xtr, Xte, ytr, yte = X[tr_idx], X[te_idx], y[tr_idx], y[te_idx]
    ensemble_preds = []
    for seed in range(SEED, SEED + 10):
        local_rng = np.random.default_rng(seed)
        boot = local_rng.choice(len(Xtr), len(Xtr), replace=True)
        m = RandomForestRegressor(n_estimators=100, random_state=seed, n_jobs=-1)
        m.fit(Xtr[boot], ytr[boot])
        ensemble_preds.append(m.predict(Xte))
    ep = np.vstack(ensemble_preds)
    pred_mean = ep.mean(axis=0)
    pred_sd = ep.std(axis=0, ddof=1)
    lower = pred_mean - 1.96 * pred_sd
    upper = pred_mean + 1.96 * pred_sd
    coverage = float(np.mean((yte >= lower) & (yte <= upper)))
    unc = pd.DataFrame({
        "row_index": te_idx,
        "observed": yte,
        "prediction_mean": pred_mean,
        "uncertainty_sd": pred_sd,
        "lower_95": lower,
        "upper_95": upper,
        "covered_95": (yte >= lower) & (yte <= upper),
    })
    unc.to_csv(out / "ensemble_uncertainty_holdout.csv", index=False)

    # Learning curve, RF baseline, leakage-safe CV.
    cv = KFold(n_splits=5, shuffle=True, random_state=SEED)
    train_sizes, train_scores, val_scores = learning_curve(
        RandomForestRegressor(n_estimators=100, random_state=SEED, n_jobs=-1),
        X, y, cv=cv, train_sizes=np.linspace(0.2, 1.0, 5),
        scoring="neg_mean_squared_error", n_jobs=-1
    )
    train_rmse = np.sqrt(-train_scores)
    val_rmse = np.sqrt(-val_scores)
    lc = pd.DataFrame({
        "train_size": train_sizes,
        "train_rmse_mean": train_rmse.mean(axis=1),
        "train_rmse_std": train_rmse.std(axis=1, ddof=1),
        "validation_rmse_mean": val_rmse.mean(axis=1),
        "validation_rmse_std": val_rmse.std(axis=1, ddof=1),
    })
    lc.to_csv(out / "learning_curve.csv", index=False)
    fig = plt.figure(figsize=(7, 4.5))
    plt.plot(train_sizes, lc["train_rmse_mean"], marker="o", label="Train RMSE")
    plt.plot(train_sizes, lc["validation_rmse_mean"], marker="o", label="Validation RMSE")
    plt.xlabel("Training samples")
    plt.ylabel("RMSE")
    plt.title("Learning curve")
    plt.legend()
    plt.tight_layout()
    fig.savefig(out / "learning_curve.png", dpi=160)
    plt.close(fig)

    # Distance-based OOD. Threshold is the 95th percentile of train leave-one-out NN distances.
    scaler = StandardScaler().fit(Xtr)
    Ztr, Zte = scaler.transform(Xtr), scaler.transform(Xte)
    nn_train = NearestNeighbors(n_neighbors=min(2, len(Ztr))).fit(Ztr)
    d_train, _ = nn_train.kneighbors(Ztr)
    train_nn = d_train[:, 1] if d_train.shape[1] > 1 else np.zeros(len(Ztr))
    threshold = float(np.quantile(train_nn, 0.95))
    nn = NearestNeighbors(n_neighbors=1).fit(Ztr)
    d_test, _ = nn.kneighbors(Zte)
    d_test = d_test[:, 0]
    ood = pd.DataFrame({"row_index": te_idx, "ood_distance": d_test, "ood_threshold": threshold, "is_ood": d_test > threshold})
    ood.to_csv(out / "ood_holdout.csv", index=False)

    summary = {
        "rows": n,
        "features": p,
        "anomalies": int((iso_flag == -1).sum()),
        "gaussian_rows": int(len(gauss)),
        "smoter_compatible_rows": int(len(smoter)),
        "uncertainty_metrics": metrics(yte, pred_mean),
        "uncertainty_95_coverage": coverage,
        "ood_threshold": threshold,
        "ood_flagged_holdout": int((d_test > threshold).sum()),
        "notes": {
            "smoter": "SMOTER-compatible interpolation is applied only to target tails and is intended as an optional robustness experiment, not a default transformation.",
            "uncertainty": "Uncertainty is ensemble predictive dispersion from bootstrap Random Forest models.",
            "ood": "OOD is distance-based in standardized descriptor space, with a 95th-percentile train nearest-neighbour threshold.",
        },
    }
    json_dump(summary, out / "robustness_summary.json")
    print(json.dumps({"ok": True, "output": str(out), **summary}, allow_nan=False))


def cmd_multitarget(args):
    out = ensure_dir(args.outdir)
    df = read_table(args.input, header="infer")
    if len(df) < 10:
        raise ValueError("At least 10 rows are required.")
    n_targets = int(args.targets)
    if n_targets < 2 or n_targets >= df.shape[1]:
        raise ValueError("Target count must be >=2 and smaller than the total column count.")
    target_cols = list(df.columns[-n_targets:])
    Xdf = df.iloc[:, :-n_targets].apply(pd.to_numeric, errors="coerce")
    valid_features = [c for c in Xdf.columns if Xdf[c].notna().sum() == len(Xdf)]
    if not valid_features:
        raise ValueError("No fully numeric feature columns found before the target columns.")
    Ydf = df[target_cols].apply(pd.to_numeric, errors="raise")
    X, Y = Xdf[valid_features].to_numpy(float), Ydf.to_numpy(float)

    kf = KFold(n_splits=5, shuffle=True, random_state=SEED)
    oof = np.zeros_like(Y, dtype=float)
    fold_rows = []
    for fold, (tr, va) in enumerate(kf.split(X), start=1):
        model = RandomForestRegressor(n_estimators=150, random_state=SEED + fold, n_jobs=-1)
        model.fit(X[tr], Y[tr])
        pred = model.predict(X[va])
        oof[va] = pred
        for j, t in enumerate(target_cols):
            fold_rows.append({"fold": fold, "target": str(t), **metrics(Y[va, j], pred[:, j])})
    fold_df = pd.DataFrame(fold_rows)
    fold_df.to_csv(out / "multitarget_fold_metrics.csv", index=False)
    summary_rows = []
    for j, t in enumerate(target_cols):
        m = metrics(Y[:, j], oof[:, j])
        summary_rows.append({"target": str(t), **m})
    pd.DataFrame(summary_rows).to_csv(out / "multitarget_oof_metrics.csv", index=False)
    pred_df = pd.DataFrame({f"observed_{t}": Y[:, j] for j, t in enumerate(target_cols)})
    for j, t in enumerate(target_cols):
        pred_df[f"oof_predicted_{t}"] = oof[:, j]
    pred_df.to_csv(out / "multitarget_oof_predictions.csv", index=False)

    final = RandomForestRegressor(n_estimators=250, random_state=SEED, n_jobs=-1).fit(X, Y)
    joblib.dump({"model": final, "features": [str(c) for c in valid_features], "targets": [str(c) for c in target_cols]}, out / "multitarget_model.joblib")
    print(json.dumps({"ok": True, "output": str(out), "targets": [str(x) for x in target_cols], "features": len(valid_features)}, allow_nan=False))


def cmd_stratified(args):
    out = ensure_dir(args.outdir)
    df = read_table(args.input, header="infer")
    target = args.target
    groups = [x.strip() for x in args.groups.split(",") if x.strip()]
    extra_exclude = [x.strip() for x in getattr(args, "exclude", "").split(",") if x.strip()]
    if target not in map(str, df.columns):
        raise ValueError(f"Target column '{target}' not found. Available: {list(map(str, df.columns))}")
    for g in groups:
        if g not in map(str, df.columns):
            raise ValueError(f"Group column '{g}' not found.")
    # normalize column names to strings for reliable indexing
    df.columns = [str(c) for c in df.columns]
    y = pd.to_numeric(df[target], errors="raise").to_numpy(float)
    X, feature_cols = numeric_matrix(df, exclude=set(groups + [target] + extra_exclude))
    if len(X) != len(y):
        raise ValueError("Feature and target row counts differ.")

    kf = KFold(n_splits=5, shuffle=True, random_state=SEED)
    oof = np.zeros(len(y), dtype=float)
    for fold, (tr, va) in enumerate(kf.split(X), start=1):
        m = RandomForestRegressor(n_estimators=150, random_state=SEED + fold, n_jobs=-1).fit(X[tr], y[tr])
        oof[va] = m.predict(X[va])
    pd.DataFrame({"observed": y, "oof_predicted": oof}).to_csv(out / "stratified_oof_predictions.csv", index=False)

    rows = []
    for g in groups:
        for value, idx in df.groupby(g, dropna=False).groups.items():
            idx = np.asarray(list(idx), dtype=int)
            if len(idx) < 3:
                continue
            rows.append({"group_column": g, "group_value": str(value), "n": len(idx), **metrics(y[idx], oof[idx])})
    pd.DataFrame(rows).to_csv(out / "stratified_group_metrics.csv", index=False)

    # Chemistry/group holdout: train on all other groups, test one group at a time.
    holdout_rows = []
    for g in groups:
        labels = df[g].astype(str).fillna("<NA>").to_numpy()
        unique = np.unique(labels)
        if len(unique) < 2:
            continue
        logo = LeaveOneGroupOut()
        for tr, va in logo.split(X, y, groups=labels):
            held = labels[va[0]]
            if len(va) < 2:
                continue
            m = RandomForestRegressor(n_estimators=150, random_state=SEED, n_jobs=-1).fit(X[tr], y[tr])
            pred = m.predict(X[va])
            holdout_rows.append({"group_column": g, "held_out_group": str(held), "n": len(va), **metrics(y[va], pred)})
    pd.DataFrame(holdout_rows).to_csv(out / "leave_one_group_out_metrics.csv", index=False)

    summary = {"target": target, "groups": groups, "excluded_columns": extra_exclude, "numeric_features": feature_cols, "global_oof": metrics(y, oof)}
    json_dump(summary, out / "stratified_summary.json")
    print(json.dumps({"ok": True, "output": str(out), **summary}, allow_nan=False))


# ---------- Milestone 3 ----------
def _read_candidate_matrix(path: str | Path, expected_features: int | None = None):
    # Try headered input first. Non-numeric first column can act as an ID.
    df = read_table(path, header="infer")
    num = df.apply(pd.to_numeric, errors="coerce")
    numeric_cols = [c for c in num.columns if num[c].notna().sum() == len(num)]
    X = num[numeric_cols].to_numpy(float)
    if expected_features is not None and X.shape[1] != expected_features:
        # Retry as no-header; useful for pure numeric candidate matrices.
        df2 = read_table(path, header=None)
        num2 = df2.apply(pd.to_numeric, errors="coerce")
        numeric_cols2 = [c for c in num2.columns if num2[c].notna().sum() == len(num2)]
        X2 = num2[numeric_cols2].to_numpy(float)
        if X2.shape[1] == expected_features:
            return df2, X2, [str(c) for c in numeric_cols2]
        raise ValueError(f"Candidate file has {X.shape[1]} numeric columns, expected {expected_features} features.")
    return df, X, [str(c) for c in numeric_cols]


def cmd_screen(args):
    out = ensure_dir(args.outdir)
    tr = read_table(args.training, header=None).apply(pd.to_numeric, errors="raise").to_numpy(float)
    X, y = tr[:, :-1], tr[:, -1]
    cand_df, C, numeric_cols = _read_candidate_matrix(args.candidates, expected_features=X.shape[1])
    if len(C) == 0:
        raise ValueError("No candidate rows found.")

    model_name = args.model.upper()
    supported = {"LG", "RFR", "ANN", "LSTM", "GRU", "TFT", "CNN_LSTM", "MOF_PREDICTIUM"}
    if model_name not in supported:
        raise ValueError("Supported screening models: " + ", ".join(sorted(supported)))

    # Reuse the same model definitions/evaluation semantics as the core benchmark.
    # This enables batch inference with any of the eight built-in architectures.
    if model_name == "MOF_PREDICTIUM":
        payload = fit_mof_predictium_batch(X, y)
        pred = predict_mof_batch(payload, C)
    else:
        from benchmark import fit_predict_model
        pred = fit_predict_model(model_name, X, y, C, epochs=160)

    # Ensemble/tree uncertainty and OOD for every candidate.
    rf = RandomForestRegressor(n_estimators=250, random_state=SEED, n_jobs=-1).fit(X, y)
    tree_pred = np.vstack([t.predict(C) for t in rf.estimators_])
    unc_sd = tree_pred.std(axis=0, ddof=1)

    scaler = StandardScaler().fit(X)
    Z = scaler.transform(X)
    Zc = scaler.transform(C)
    nn_train = NearestNeighbors(n_neighbors=min(2, len(Z))).fit(Z)
    dtrain, _ = nn_train.kneighbors(Z)
    train_nn = dtrain[:, 1] if dtrain.shape[1] > 1 else np.zeros(len(Z))
    threshold = float(np.quantile(train_nn, 0.95))
    nn = NearestNeighbors(n_neighbors=1).fit(Z)
    dc, _ = nn.kneighbors(Zc)
    dc = dc[:, 0]

    result = cand_df.copy().reset_index(drop=True)
    result["predicted_target"] = pred
    result["uncertainty_sd_rf"] = unc_sd
    result["ood_distance"] = dc
    result["is_ood"] = dc > threshold
    ascending = args.direction.lower() == "min"
    result = result.sort_values("predicted_target", ascending=ascending).reset_index(drop=True)
    result.insert(0, "rank", np.arange(1, len(result) + 1))
    result.to_csv(out / "screened_candidates.csv", index=False)
    summary = {
        "model": model_name,
        "candidates": len(result),
        "direction": args.direction,
        "ood_threshold": threshold,
        "ood_candidates": int(result["is_ood"].sum()),
        "uncertainty_source": "RandomForest tree dispersion",
    }
    json_dump(summary, out / "screening_summary.json")
    print(json.dumps({"ok": True, "output": str(out), **summary}, allow_nan=False))


def cmd_map(args):
    out = ensure_dir(args.outdir)
    df = read_table(args.input, header="infer")
    df.columns = [str(c) for c in df.columns]
    color_col = (getattr(args, "color", "") or "").strip()
    X, cols = numeric_matrix(df)
    if X.shape[1] < 2:
        raise ValueError("At least two numeric columns are needed for a property-space map.")
    Z = StandardScaler().fit_transform(X)
    pca = PCA(n_components=2, random_state=SEED)
    P = pca.fit_transform(Z)
    mapdf = df.copy().reset_index(drop=True)
    mapdf["PCA1"] = P[:, 0]
    mapdf["PCA2"] = P[:, 1]
    mapdf.to_csv(out / "pca_map.csv", index=False)
    fig = plt.figure(figsize=(7, 5))
    if color_col:
        if color_col not in df.columns:
            raise ValueError(f"Color column '{color_col}' not found.")
        cvals = pd.to_numeric(df[color_col], errors="raise").to_numpy(float)
        sc = plt.scatter(P[:, 0], P[:, 1], c=cvals, s=18, alpha=0.75)
        plt.colorbar(sc, label=color_col)
    else:
        plt.scatter(P[:, 0], P[:, 1], s=18, alpha=0.75)
    plt.xlabel("PCA1")
    plt.ylabel("PCA2")
    plt.title("MOF descriptor/property space: PCA")
    plt.tight_layout(); fig.savefig(out / "pca_map.png", dpi=160); plt.close(fig)

    umap_ok = False
    if HAVE_UMAP:
        reducer = umap.UMAP(n_components=2, random_state=SEED, n_neighbors=min(15, max(2, len(df)-1)))
        U = reducer.fit_transform(Z)
        udf = df.copy().reset_index(drop=True)
        udf["UMAP1"] = U[:, 0]
        udf["UMAP2"] = U[:, 1]
        udf.to_csv(out / "umap_map.csv", index=False)
        fig = plt.figure(figsize=(7, 5))
        if color_col:
            cvals = pd.to_numeric(df[color_col], errors="raise").to_numpy(float)
            sc = plt.scatter(U[:, 0], U[:, 1], c=cvals, s=18, alpha=0.75)
            plt.colorbar(sc, label=color_col)
        else:
            plt.scatter(U[:, 0], U[:, 1], s=18, alpha=0.75)
        plt.xlabel("UMAP1"); plt.ylabel("UMAP2"); plt.title("MOF descriptor/property space: UMAP")
        plt.tight_layout(); fig.savefig(out / "umap_map.png", dpi=160); plt.close(fig)
        umap_ok = True
    else:
        (out / "UMAP_NOT_INSTALLED.txt").write_text("Install 'umap-learn' (included in requirements.txt) and re-run this module.", encoding="utf-8")

    summary = {"rows": len(df), "numeric_columns": cols, "color_column": color_col or None, "pca_explained_variance_ratio": pca.explained_variance_ratio_.tolist(), "umap_created": umap_ok}
    json_dump(summary, out / "property_map_summary.json")
    print(json.dumps({"ok": True, "output": str(out), **summary}, allow_nan=False))


def pareto_mask(values: np.ndarray, maximize: np.ndarray):
    """Return non-dominated points using an incremental skyline frontier.

    All objectives are internally converted to minimization. The algorithm compares
    each candidate only with the current frontier, which is typically much smaller
    than the full candidate set and is practical for large screening tables.
    """
    V = values.copy().astype(float)
    V[:, maximize] *= -1.0
    frontier: list[int] = []
    for i in range(len(V)):
        vi = V[i]
        dominated = False
        remove: list[int] = []
        for j in frontier:
            vj = V[j]
            if np.all(vj <= vi) and np.any(vj < vi):
                dominated = True
                break
            if np.all(vi <= vj) and np.any(vi < vj):
                remove.append(j)
        if dominated:
            continue
        if remove:
            rem = set(remove)
            frontier = [j for j in frontier if j not in rem]
        frontier.append(i)
    mask = np.zeros(len(V), dtype=bool)
    mask[frontier] = True
    return mask


def cmd_pareto(args):
    out = ensure_dir(args.outdir)
    df = read_table(args.input, header="infer")
    df.columns = [str(c) for c in df.columns]
    objectives = [x.strip() for x in args.objectives.split(",") if x.strip()]
    directions = [x.strip().lower() for x in args.directions.split(",") if x.strip()]
    if len(objectives) < 2:
        raise ValueError("At least two objective columns are required.")
    if len(directions) != len(objectives) or any(d not in {"min", "max"} for d in directions):
        raise ValueError("Directions must match objectives and contain only min/max.")
    for c in objectives:
        if c not in df.columns:
            raise ValueError(f"Objective column '{c}' not found.")
    vals = df[objectives].apply(pd.to_numeric, errors="raise").to_numpy(float)
    maximize = np.array([d == "max" for d in directions], dtype=bool)
    mask = pareto_mask(vals, maximize)
    result = df.copy()
    result["is_pareto"] = mask
    result.to_csv(out / "pareto_all_candidates.csv", index=False)
    result[mask].to_csv(out / "pareto_front.csv", index=False)
    summary = {"objectives": objectives, "directions": directions, "rows": len(df), "pareto_rows": int(mask.sum())}
    json_dump(summary, out / "pareto_summary.json")
    print(json.dumps({"ok": True, "output": str(out), **summary}, allow_nan=False))


def cmd_inverse(args):
    """Gradient-based inverse design in descriptor space followed by candidate retrieval.

    A differentiable neural surrogate is trained on the current labelled training table.
    Descriptor values are optimized to match a user-specified target property while being
    constrained to the observed training descriptor range. Existing candidates are then
    ranked by distance to the optimized descriptor and by surrogate target agreement.
    """
    try:
        import torch
        import torch.nn as nn
    except Exception as exc:
        raise RuntimeError("PyTorch is required for gradient-based inverse design.") from exc

    out = ensure_dir(args.outdir)
    tr = read_table(args.training, header=None).apply(pd.to_numeric, errors="raise").to_numpy(float)
    if tr.shape[1] < 2:
        raise ValueError("Training data must contain descriptors followed by one target column.")
    X, y = tr[:, :-1], tr[:, -1]
    cand_df, C, numeric_cols = _read_candidate_matrix(args.candidates, expected_features=X.shape[1])
    target_value = float(args.target_value)
    top_n = int(args.top)

    x_scaler = StandardScaler().fit(X)
    y_scaler = StandardScaler().fit(y.reshape(-1, 1))
    Xs = x_scaler.transform(X).astype(np.float32)
    ys = y_scaler.transform(y.reshape(-1, 1)).astype(np.float32)
    Cs = x_scaler.transform(C).astype(np.float32)
    target_scaled = float(y_scaler.transform([[target_value]])[0, 0])

    torch.manual_seed(SEED)
    model = nn.Sequential(
        nn.Linear(X.shape[1], 64), nn.ReLU(),
        nn.Linear(64, 32), nn.ReLU(),
        nn.Linear(32, 1),
    )
    xt = torch.tensor(Xs, dtype=torch.float32)
    yt = torch.tensor(ys, dtype=torch.float32)
    opt = torch.optim.Adam(model.parameters(), lr=0.01)
    loss_fn = nn.MSELoss()
    model.train()
    for _ in range(350):
        pred = model(xt)
        loss = loss_fn(pred, yt)
        opt.zero_grad(); loss.backward(); opt.step()

    # Start from the candidate whose surrogate prediction is closest to the requested target.
    model.eval()
    with torch.no_grad():
        cand_pred_scaled = model(torch.tensor(Cs)).squeeze(1).cpu().numpy()
    start_idx = int(np.argmin(np.abs(cand_pred_scaled - target_scaled)))
    z0 = torch.tensor(Cs[start_idx], dtype=torch.float32)
    z = z0.clone().detach().requires_grad_(True)
    optimizer = torch.optim.Adam([z], lr=0.05)
    zmin = torch.tensor(np.min(Xs, axis=0), dtype=torch.float32)
    zmax = torch.tensor(np.max(Xs, axis=0), dtype=torch.float32)
    tgt = torch.tensor(target_scaled, dtype=torch.float32)
    trace = []
    regularization = 0.002
    for step in range(251):
        pred = model(z.unsqueeze(0)).squeeze()
        target_loss = (pred - tgt) ** 2
        proximity_loss = regularization * torch.mean((z - z0) ** 2)
        loss = target_loss + proximity_loss
        optimizer.zero_grad(); loss.backward(); optimizer.step()
        with torch.no_grad():
            z.clamp_(zmin, zmax)
        if step % 10 == 0 or step == 250:
            pred_raw = float(y_scaler.inverse_transform([[float(pred.detach())]])[0, 0])
            trace.append({"step": step, "loss": float(loss.detach()), "surrogate_property": pred_raw})

    zopt = z.detach().cpu().numpy()
    xopt = x_scaler.inverse_transform(zopt.reshape(1, -1))[0]
    with torch.no_grad():
        cand_pred_s = model(torch.tensor(Cs)).squeeze(1).cpu().numpy()
    cand_pred = y_scaler.inverse_transform(cand_pred_s.reshape(-1, 1)).ravel()
    distance = np.sqrt(np.sum((Cs - zopt) ** 2, axis=1))
    target_error = np.abs(cand_pred - target_value)

    result = cand_df.copy().reset_index(drop=True)
    result["inverse_surrogate_prediction"] = cand_pred
    result["target_absolute_error"] = target_error
    result["optimized_descriptor_distance"] = distance
    # Primary retrieval criterion follows the descriptor-space plan; target agreement breaks ties.
    order = np.lexsort((target_error, distance))
    result = result.iloc[order].reset_index(drop=True)
    result.insert(0, "inverse_design_rank", np.arange(1, len(result) + 1))
    result.head(top_n).to_csv(out / "inverse_design_top_candidates.csv", index=False)
    result.to_csv(out / "inverse_design_all_candidates.csv", index=False)

    pd.DataFrame([xopt], columns=[f"descriptor_{i+1}" for i in range(len(xopt))]).assign(
        requested_target=target_value
    ).to_csv(out / "optimized_descriptor.csv", index=False)
    pd.DataFrame(trace).to_csv(out / "optimization_trace.csv", index=False)

    with torch.no_grad():
        opt_pred_s = float(model(torch.tensor(zopt, dtype=torch.float32).unsqueeze(0)).squeeze())
    opt_pred = float(y_scaler.inverse_transform([[opt_pred_s]])[0, 0])
    summary = {
        "method": "gradient-based optimization of a differentiable neural surrogate in standardized descriptor space, constrained to training ranges, followed by nearest-candidate retrieval",
        "target_value": target_value,
        "optimized_surrogate_prediction": opt_pred,
        "feature_count": int(X.shape[1]),
        "candidate_count": int(len(C)),
        "top_n": top_n,
        "candidate_numeric_columns": numeric_cols,
    }
    json_dump(summary, out / "inverse_design_summary.json")
    print(json.dumps({"ok": True, "output": str(out), **summary}, allow_nan=False))


# ---------- Milestone 4 ----------
def cmd_tutorial(args):
    out = ensure_dir(args.outdir)
    n = int(args.rows)
    rng = np.random.default_rng(SEED)
    topologies = np.array(["fcu", "sod", "pcu", "dia", "rtl", "acs"])
    metals = np.array(["Zr", "Zn", "Fe", "Cu", "Al", "Co"])
    linkers = np.array(["carboxylate", "imidazolate", "triazolate", "phosphonate"])
    topology = rng.choice(topologies, n)
    metal = rng.choice(metals, n)
    linker = rng.choice(linkers, n)
    pore_diameter = rng.uniform(4.5, 22.0, n)
    surface = np.clip(rng.normal(1450, 500, n), 150, 3200)
    pore_vol = np.clip(surface / 2400 + rng.normal(0, 0.12, n), 0.1, 1.8)
    density = np.clip(rng.normal(0.95, 0.25, n), 0.3, 1.8)
    metal_en_map = {"Zr": 1.33, "Zn": 1.65, "Fe": 1.83, "Cu": 1.90, "Al": 1.61, "Co": 1.88}
    metal_en = np.array([metal_en_map[x] for x in metal])
    linker_hydro = rng.uniform(0.0, 1.0, n)
    # Demonstration targets are synthetic by design and are not experimental claims.
    water = np.clip(0.65 + 0.10*(metal == "Zr") + 0.06*(linker == "phosphonate") - 0.18*linker_hydro + rng.normal(0,0.08,n), 0, 1)
    gas = np.clip(0.0025*surface + 0.10*pore_diameter + rng.normal(0,0.5,n), 0, None)
    tox = np.clip(0.35 + 0.12*(metal == "Co") + 0.10*(metal == "Cu") - 0.10*(metal == "Zr") + 0.15*linker_hydro + rng.normal(0,0.08,n), 0, 1)
    df = pd.DataFrame({
        "mof_id": [f"TUTORIAL_MOF_{i+1:04d}" for i in range(n)],
        "topology": topology,
        "metal_node": metal,
        "linker_family": linker,
        "pore_diameter_A": pore_diameter,
        "surface_area_m2g": surface,
        "pore_volume_cm3g": pore_vol,
        "density_gcm3": density,
        "metal_electronegativity": metal_en,
        "linker_hydrophilicity": linker_hydro,
        "water_stability_score": water,
        "gas_uptake_mmol_g": gas,
        "toxicity_proxy_demo": tox,
    })
    df.to_csv(out / "mof_tutorial_dataset.csv", index=False)
    (out / "README_TUTORIAL_DATASET.md").write_text(
        "# MOF-Predictium tutorial dataset\n\n"
        "This deterministic dataset (seed 42) is **synthetic and educational only**. It is bundled to demonstrate "
        "multi-target prediction, stratified evaluation, screening, PCA/UMAP, Pareto analysis and inverse design. "
        "It must not be cited as experimental MOF property or toxicity evidence.\n",
        encoding="utf-8")
    print(json.dumps({"ok": True, "output": str(out), "rows": n, "file": str(out / 'mof_tutorial_dataset.csv')}, allow_nan=False))


def build_parser():
    p = argparse.ArgumentParser(description="MOF-Predictium VM completion toolbox")
    sub = p.add_subparsers(dest="cmd", required=True)

    q = sub.add_parser("robustness"); q.add_argument("--input", required=True); q.add_argument("--outdir", required=True); q.set_defaults(func=cmd_robustness)
    q = sub.add_parser("multitarget"); q.add_argument("--input", required=True); q.add_argument("--targets", required=True, type=int); q.add_argument("--outdir", required=True); q.set_defaults(func=cmd_multitarget)
    q = sub.add_parser("stratified"); q.add_argument("--input", required=True); q.add_argument("--target", required=True); q.add_argument("--groups", required=True); q.add_argument("--exclude", default=""); q.add_argument("--outdir", required=True); q.set_defaults(func=cmd_stratified)
    q = sub.add_parser("screen"); q.add_argument("--training", required=True); q.add_argument("--candidates", required=True); q.add_argument("--model", default="MOF_PREDICTIUM"); q.add_argument("--direction", choices=["min","max"], default="max"); q.add_argument("--outdir", required=True); q.set_defaults(func=cmd_screen)
    q = sub.add_parser("map"); q.add_argument("--input", required=True); q.add_argument("--color", default=""); q.add_argument("--outdir", required=True); q.set_defaults(func=cmd_map)
    q = sub.add_parser("pareto"); q.add_argument("--input", required=True); q.add_argument("--objectives", required=True); q.add_argument("--directions", required=True); q.add_argument("--outdir", required=True); q.set_defaults(func=cmd_pareto)
    q = sub.add_parser("inverse"); q.add_argument("--training", required=True); q.add_argument("--candidates", required=True); q.add_argument("--target-value", required=True, type=float); q.add_argument("--top", default=20, type=int); q.add_argument("--outdir", required=True); q.set_defaults(func=cmd_inverse)
    q = sub.add_parser("tutorial"); q.add_argument("--outdir", required=True); q.add_argument("--rows", default=240, type=int); q.set_defaults(func=cmd_tutorial)
    return p


def main():
    parser = build_parser()
    args = parser.parse_args()
    try:
        args.func(args)
    except Exception as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, allow_nan=False))
        raise


if __name__ == "__main__":
    main()
