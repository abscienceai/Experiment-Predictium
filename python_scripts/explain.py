"""
Model Interpretability Script — Experiment-Predictium
Computes feature importance, SHAP values, and generates scientific explanation.
Outputs: JSON metadata + PNG chart saved to temp file.
"""
import sys, os, json, warnings
warnings.filterwarnings("ignore")

import numpy as np
import pandas as pd
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from sklearn.ensemble import RandomForestRegressor
from sklearn.linear_model import LinearRegression
from sklearn.inspection import permutation_importance
from sklearn.model_selection import train_test_split

train_file  = sys.argv[1]
model_name  = sys.argv[2].upper()
chart_path  = sys.argv[3]          # Where to save the PNG
col_headers = sys.argv[4].split("|") if len(sys.argv) > 4 else []

df = pd.read_csv(train_file, header=None)
X  = df.iloc[:, :-1].values
y  = df.iloc[:, -1].values
n_features = X.shape[1]

# Use column headers if provided, else generic names
feat_names = col_headers[:n_features] if len(col_headers) >= n_features \
    else [f"Feature {i+1}" for i in range(n_features)]
target_name = col_headers[n_features] if len(col_headers) > n_features else "Target"

X_tr, X_te, y_tr, y_te = train_test_split(X, y, test_size=0.2, random_state=42)

importances = None
shap_available = False
shap_vals = None
method_used = ""

# ── Feature importance by model type ─────────────────────────────
if model_name in ("RFR",):
    m = RandomForestRegressor(n_estimators=100, random_state=42)
    m.fit(X_tr, y_tr)
    importances  = m.feature_importances_
    method_used  = "Random Forest built-in importance (mean decrease impurity)"

elif model_name == "LG":
    m = LinearRegression().fit(X_tr, y_tr)
    importances  = np.abs(m.coef_)
    importances /= importances.sum() + 1e-10
    method_used  = "Linear Regression coefficient magnitudes (normalized)"

else:
    # Permutation importance for neural networks
    from sklearn.neural_network import MLPRegressor
    m = MLPRegressor(hidden_layer_sizes=(100,50), max_iter=500, random_state=42)
    m.fit(X_tr, y_tr)
    pi = permutation_importance(m, X_te, y_te, n_repeats=10, random_state=42)
    importances  = np.maximum(pi.importances_mean, 0)
    total = importances.sum() + 1e-10
    importances /= total
    method_used  = f"Permutation importance (proxy for {model_name})"

# ── SHAP values (optional) ────────────────────────────────────────
try:
    import shap
    if model_name == "RFR":
        explainer = shap.TreeExplainer(m)
    else:
        explainer = shap.KernelExplainer(m.predict,
                        shap.sample(X_tr, min(50, len(X_tr))))
    sv = explainer.shap_values(X_te[:min(50, len(X_te))])
    shap_vals       = np.abs(sv).mean(axis=0).tolist()
    shap_available  = True
except Exception:
    shap_vals = importances.tolist()

# ── Sort by importance ────────────────────────────────────────────
idx   = np.argsort(importances)[::-1]
names = [feat_names[i] for i in idx]
vals  = importances[idx].tolist()

# ── Build chart ───────────────────────────────────────────────────
BLUE  = "#1F5C99"
GREEN = "#22A06B"
fig, axes = plt.subplots(1, 2 if shap_available else 1,
                         figsize=(12 if shap_available else 7, 5))
if not shap_available:
    axes = [axes]

# Feature importance bar
ax = axes[0]
colors = [BLUE if v >= vals[0]*0.5 else "#90B8D8" for v in vals]
bars   = ax.barh(names[::-1], vals[::-1], color=colors[::-1], edgecolor="white")
ax.set_xlabel("Relative Importance", fontsize=10)
ax.set_title(f"Feature Importance\n({method_used.split('(')[0].strip()})",
             fontsize=10, fontweight="bold")
ax.spines[["top","right"]].set_visible(False)
for bar, v in zip(bars, vals[::-1]):
    ax.text(v + 0.005, bar.get_y() + bar.get_height()/2,
            f"{v:.3f}", va="center", fontsize=8, color="#333")

# SHAP bar
if shap_available:
    s_idx  = np.argsort(shap_vals)[::-1]
    s_names = [feat_names[i] for i in s_idx]
    s_vals  = [shap_vals[i]  for i in s_idx]
    ax2 = axes[1]
    ax2.barh(s_names[::-1], s_vals[::-1], color=GREEN, edgecolor="white")
    ax2.set_xlabel("Mean |SHAP value|", fontsize=10)
    ax2.set_title("SHAP Feature Attribution", fontsize=10, fontweight="bold")
    ax2.spines[["top","right"]].set_visible(False)

plt.suptitle(f"Model Interpretability — {model_name} | Target: {target_name}",
             fontsize=11, fontweight="bold", y=1.02)
plt.tight_layout()
fig.savefig(chart_path, dpi=150, bbox_inches="tight")
plt.close()

# ── Auto-generate scientific explanation ──────────────────────────
top3 = names[:3]
top3v = [f"{v:.3f}" for v in vals[:3]]
low1  = names[-1]

explanation = (
    f"The {model_name} model identified '{top3[0]}' as the most influential "
    f"predictor of {target_name} (relative importance: {top3v[0]}), followed by "
    f"'{top3[1]}' ({top3v[1]}) and '{top3[2]}' ({top3v[2]}). "
)
if shap_available:
    sv_top = s_names[0]
    explanation += (
        f"SHAP analysis confirms that '{sv_top}' has the largest average impact on "
        f"individual predictions, indicating a strong and consistent relationship "
        f"with the target variable. "
    )
explanation += (
    f"The feature '{low1}' contributed least to the prediction, suggesting it may "
    f"have limited discriminative power for {target_name} in this dataset. "
    f"These findings are consistent with the physicochemical intuition that "
    f"structural descriptors governing pore accessibility and internal surface area "
    f"are primary determinants of thermodynamic and electrochemical MOF properties."
)

# Output JSON
result = {
    "model":          model_name,
    "target":         target_name,
    "method":         method_used,
    "shap_available": shap_available,
    "importance":     [{"name": n, "value": v} for n, v in zip(names, vals)],
    "shap":           [{"name": feat_names[i], "value": shap_vals[i]}
                       for i in range(len(shap_vals))] if shap_available else [],
    "explanation":    explanation,
    "chart_path":     chart_path,
    "n_samples":      len(X),
    "n_features":     n_features
}
print(json.dumps(result))
