"""
PDF Report Generator — Experiment-Predictium
Generates a scientific analysis report from collected results.
"""
import sys, json, os, datetime
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import matplotlib.gridspec as gridspec
from matplotlib.backends.backend_pdf import PdfPages

data_file   = sys.argv[1]   # JSON file with all report data
output_pdf  = sys.argv[2]   # Output PDF path

with open(data_file, "r", encoding="utf-8") as f:
    d = json.load(f)

# ── Colors ────────────────────────────────────────────────────────
BLUE  = "#1F5C99"
GREEN = "#22A06B"
GRAY  = "#888780"
LIGHT = "#F5F7FA"

def header_style():
    return dict(fontsize=13, fontweight="bold", color=BLUE)

with PdfPages(output_pdf) as pdf:

    # ── PAGE 1: Title page ────────────────────────────────────────
    fig = plt.figure(figsize=(8.27, 11.69))
    fig.patch.set_facecolor(LIGHT)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_axis_off()

    # Blue header bar
    ax.add_patch(plt.Rectangle((0, 0.82), 1, 0.18, color=BLUE, transform=ax.transAxes))
    ax.text(0.5, 0.92, "Experiment-Predictium", transform=ax.transAxes,
            ha="center", va="center", fontsize=22, fontweight="bold",
            color="white")
    ax.text(0.5, 0.86, "Machine Learning Analysis Report", transform=ax.transAxes,
            ha="center", va="center", fontsize=14, color="#BDD7EE")

    # Report metadata
    meta = [
        ("Target Variable",   d.get("target_name", "—")),
        ("Dataset Samples",   str(d.get("n_samples", "—"))),
        ("Features",          str(d.get("n_features", "—"))),
        ("Algorithm",         d.get("model_name", "—")),
        ("Train/Test Split",  "80% / 20%"),
        ("Generated",         datetime.datetime.now().strftime("%Y-%m-%d %H:%M")),
    ]
    y_pos = 0.72
    for label, value in meta:
        ax.text(0.15, y_pos, label + ":", transform=ax.transAxes,
                fontsize=11, color=GRAY, va="center")
        ax.text(0.5, y_pos, value, transform=ax.transAxes,
                fontsize=11, fontweight="bold", color="#1A1A1A", va="center")
        ax.plot([0.1, 0.9], [y_pos - 0.022, y_pos - 0.022],
                color="#D0D5DD", linewidth=0.5, transform=ax.transAxes)
        y_pos -= 0.055

    # EU4MOFs note
    ax.text(0.5, 0.18, "Generated with Experiment-Predictium",
            transform=ax.transAxes, ha="center", fontsize=9, color=GRAY)
    ax.text(0.5, 0.14, "EU4MOFs COST Action CA22147 — WG3 & WG4",
            transform=ax.transAxes, ha="center", fontsize=9, color=BLUE)

    pdf.savefig(fig, bbox_inches="tight")
    plt.close()

    # ── PAGE 2: Data Quality Summary ─────────────────────────────
    if "quality_stats" in d and d["quality_stats"]:
        stats = d["quality_stats"]
        fig, ax = plt.subplots(figsize=(8.27, 11.69))
        ax.set_axis_off()
        ax.text(0.5, 0.97, "Data Quality Summary",
                transform=ax.transAxes, ha="center", **header_style())

        cols   = ["Column", "Count", "Missing", "Missing%", "Min", "Max", "Mean", "Std Dev"]
        rows   = [[s["name"], s["count"], s["missing"],
                   f"{s['missing_pct']:.1f}%",
                   f"{s['min']:.4f}", f"{s['max']:.4f}",
                   f"{s['mean']:.4f}", f"{s['std']:.4f}"] for s in stats]

        tbl = ax.table(cellText=rows, colLabels=cols,
                       loc="upper center", cellLoc="center")
        tbl.auto_set_font_size(False)
        tbl.set_fontsize(8)
        tbl.scale(1, 1.4)

        # Style header
        for j in range(len(cols)):
            tbl[(0, j)].set_facecolor(BLUE)
            tbl[(0, j)].set_text_props(color="white", fontweight="bold")
        # Style missing columns
        for i, s in enumerate(stats):
            pct = s["missing_pct"]
            clr = "#D4EDDA" if pct == 0 else "#FFF3CD" if pct < 5 else "#F8D7DA"
            tbl[(i+1, 2)].set_facecolor(clr)
            tbl[(i+1, 3)].set_facecolor(clr)

        pdf.savefig(fig, bbox_inches="tight")
        plt.close()

    # ── PAGE 3: Benchmark Results ─────────────────────────────────
    if "benchmark" in d and d["benchmark"]:
        bm = d["benchmark"]
        models = [r["model"] for r in bm]
        rmse   = [r["rmse"]  for r in bm]
        mae    = [r["mae"]   for r in bm]
        r2     = [r["r2"]    for r in bm]

        best_idx = int(np.argmax(r2))

        fig = plt.figure(figsize=(8.27, 11.69))
        gs  = gridspec.GridSpec(3, 1, hspace=0.5)

        for ax_idx, (vals, title, clr) in enumerate([
            (r2,   "R² Score (higher is better)",   BLUE),
            (rmse, "RMSE (lower is better)",         "#C0392B"),
            (mae,  "MAE (lower is better)",          "#E67E22"),
        ]):
            ax = fig.add_subplot(gs[ax_idx])
            bar_colors = [GREEN if i == best_idx else clr for i in range(len(models))]
            ax.bar(models, vals, color=bar_colors, edgecolor="white", width=0.6)
            ax.set_title(title, fontsize=10, fontweight="bold", color="#1A1A1A")
            ax.spines[["top","right"]].set_visible(False)
            ax.tick_params(axis="x", labelsize=8)
            for i, v in enumerate(vals):
                ax.text(i, v + max(vals)*0.01, f"{v:.4f}", ha="center",
                        fontsize=7, color="#333")
            if ax_idx == 0:
                patch = mpatches.Patch(color=GREEN, label=f"Best: {models[best_idx]}")
                ax.legend(handles=[patch], fontsize=8)

        fig.suptitle("Benchmark Results — All Models", fontsize=13,
                     fontweight="bold", color=BLUE, y=0.98)
        pdf.savefig(fig, bbox_inches="tight")
        plt.close()

    # ── PAGE 4: Feature Importance Chart ─────────────────────────
    if "chart_path" in d and os.path.exists(d["chart_path"]):
        fig = plt.figure(figsize=(8.27, 11.69))
        ax  = fig.add_axes([0.05, 0.35, 0.9, 0.55])
        img = plt.imread(d["chart_path"])
        ax.imshow(img)
        ax.set_axis_off()

        ax2 = fig.add_axes([0.05, 0.05, 0.9, 0.28])
        ax2.set_axis_off()
        explanation = d.get("explanation", "")
        ax2.text(0, 1, "Scientific Interpretation:", transform=ax2.transAxes,
                 fontsize=10, fontweight="bold", color=BLUE, va="top")
        # Wrap text manually for older matplotlib compatibility
        import textwrap
        wrapped = "\n".join(textwrap.wrap(explanation, width=90))
        ax2.text(0, 0.82, wrapped, transform=ax2.transAxes,
                 fontsize=9, color="#1A1A1A", va="top",
                 bbox=dict(boxstyle="round", facecolor=LIGHT, alpha=0.8))

        fig.suptitle("Feature Importance & Interpretability",
                     fontsize=13, fontweight="bold", color=BLUE, y=0.97)
        pdf.savefig(fig, bbox_inches="tight")
        plt.close()

    # ── PAGE 5: Summary & Prediction ─────────────────────────────
    fig, ax = plt.subplots(figsize=(8.27, 11.69))
    ax.set_axis_off()
    ax.text(0.5, 0.97, "Summary & Prediction Result",
            transform=ax.transAxes, ha="center", **header_style())

    summary_lines = []
    if "prediction" in d:
        summary_lines.append(f"Prediction: {d['prediction']}")
    if "model_name" in d:
        summary_lines.append(f"Algorithm: {d['model_name']}")
    if "best_r2" in d:
        summary_lines.append(f"Best R² (benchmark): {d['best_r2']:.4f}")
    if "explanation" in d:
        summary_lines.append("")
        summary_lines.append("Key finding:")
        summary_lines.append(d["explanation"])

    y = 0.88
    for line in summary_lines:
        ax.text(0.1, y, line, transform=ax.transAxes,
                fontsize=9 if len(line) > 80 else 10,
                color="#1A1A1A", va="top", wrap=True)
        y -= 0.06 if len(line) > 80 else 0.04

    ax.text(0.5, 0.03,
            "Experiment-Predictium | EU4MOFs CA22147 | " +
            datetime.datetime.now().strftime("%Y-%m-%d"),
            transform=ax.transAxes, ha="center", fontsize=8, color=GRAY)

    pdf.savefig(fig, bbox_inches="tight")
    plt.close()

print(output_pdf)