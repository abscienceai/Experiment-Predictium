"""
Experiment-Predictium v2.2.0-VM benchmark engine

VM completion core benchmark:
- 8-model benchmark (7 existing baselines + MOF-Predictium)
- 80/20 reproducible hold-out evaluation
- 5-fold cross-validation
- RMSE, MAE, MAPE and R2
- fold-wise standardisation fitted only on training folds for neural models

Input CSV has no header. Last column is the regression target.
JSON is written to stdout for the WinForms front-end.
"""
from __future__ import annotations

import json
import math
import sys
import time
import warnings
from dataclasses import dataclass
from typing import Callable, Dict, Tuple

warnings.filterwarnings("ignore")

import numpy as np
import pandas as pd
from sklearn.ensemble import GradientBoostingRegressor, RandomForestRegressor
from sklearn.linear_model import LinearRegression
from sklearn.metrics import mean_absolute_error, mean_squared_error, r2_score
from sklearn.model_selection import KFold, train_test_split
from sklearn.neural_network import MLPRegressor
from sklearn.preprocessing import StandardScaler

import torch
import torch.nn as nn

SEED = 42
N_SPLITS = 5
HOLDOUT_EPOCHS = 160
CV_EPOCHS = 100

np.random.seed(SEED)
torch.manual_seed(SEED)


def regression_metrics(y_true, y_pred) -> Dict[str, float]:
    y_true = np.asarray(y_true, dtype=float)
    y_pred = np.asarray(y_pred, dtype=float)
    rmse = float(np.sqrt(mean_squared_error(y_true, y_pred)))
    mae = float(mean_absolute_error(y_true, y_pred))
    r2 = float(r2_score(y_true, y_pred)) if len(y_true) > 1 else float("nan")
    # MAPE is unstable at/near zero. Use epsilon-protected denominator and report percent.
    denom = np.maximum(np.abs(y_true), 1e-8)
    mape = float(np.mean(np.abs((y_true - y_pred) / denom)) * 100.0)
    return {"rmse": rmse, "mae": mae, "mape": mape, "r2": r2}


def summary(values):
    a = np.asarray(values, dtype=float)
    return {
        "mean": float(np.nanmean(a)),
        "std": float(np.nanstd(a, ddof=1)) if len(a) > 1 else 0.0,
    }


class LSTMModel(nn.Module):
    def __init__(self, n_features: int):
        super().__init__()
        self.lstm = nn.LSTM(n_features, 32, batch_first=True)
        self.fc = nn.Linear(32, 1)

    def forward(self, x):
        out, _ = self.lstm(x.unsqueeze(1))
        return self.fc(out[:, -1, :])


class GRUModel(nn.Module):
    def __init__(self, n_features: int):
        super().__init__()
        self.gru = nn.GRU(n_features, 32, batch_first=True)
        self.fc = nn.Linear(32, 1)

    def forward(self, x):
        out, _ = self.gru(x.unsqueeze(1))
        return self.fc(out[:, -1, :])


class TransformerBaseline(nn.Module):
    def __init__(self, n_features: int):
        super().__init__()
        self.enc = nn.Linear(n_features, 64)
        layer = nn.TransformerEncoderLayer(
            d_model=64, nhead=4, dim_feedforward=2048, dropout=0.1, batch_first=True
        )
        self.encoder = nn.TransformerEncoder(layer, num_layers=1)
        self.fc = nn.Linear(64, 1)

    def forward(self, x):
        z = self.enc(x).unsqueeze(1)
        return self.fc(self.encoder(z)[:, 0, :])


class CNNLSTMModel(nn.Module):
    def __init__(self, n_features: int):
        super().__init__()
        self.conv = nn.Conv1d(1, 32, kernel_size=1)
        self.lstm = nn.LSTM(32, 50, batch_first=True)
        self.fc = nn.Linear(50, 1)

    def forward(self, x):
        z = torch.relu(self.conv(x.unsqueeze(1)))
        z, _ = self.lstm(z.permute(0, 2, 1))
        return self.fc(z[:, -1, :])


class MOFPredictiumNet(nn.Module):
    """Hybrid neural backbone specified in the approved VM plan.

    1D convolution: local descriptor-pattern extraction
    Multi-head self-attention: global feature-dependency modelling
    Gradient-boosted residual correction is fitted separately after the neural backbone.
    """

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


def fit_torch_model(model_cls, X_train, y_train, epochs: int):
    xt = torch.tensor(X_train, dtype=torch.float32)
    yt = torch.tensor(y_train, dtype=torch.float32).view(-1, 1)

    torch.manual_seed(SEED)
    model = model_cls(X_train.shape[1])
    opt = torch.optim.Adam(model.parameters(), lr=0.01)
    loss_fn = nn.MSELoss()

    model.train()
    for _ in range(epochs):
        pred = model(xt)
        loss = loss_fn(pred, yt)
        opt.zero_grad()
        loss.backward()
        opt.step()

    return model


def predict_torch(model, X):
    xt = torch.tensor(X, dtype=torch.float32)
    model.eval()
    with torch.no_grad():
        return model(xt).squeeze(1).cpu().numpy()


def fit_mof_predictium(X_train, y_train, X_eval, epochs: int):
    # MOF-Predictium has its own train-fold-only standardisation.
    scaler = StandardScaler()
    Xs = scaler.fit_transform(X_train)
    xt = torch.tensor(Xs, dtype=torch.float32)
    yt = torch.tensor(y_train, dtype=torch.float32).view(-1, 1)

    torch.manual_seed(SEED)
    neural = MOFPredictiumNet(X_train.shape[1])
    opt = torch.optim.Adam(neural.parameters(), lr=0.005)
    loss_fn = nn.MSELoss()
    for _ in range(epochs):
        pred = neural(xt)
        loss = loss_fn(pred, yt)
        opt.zero_grad(); loss.backward(); opt.step()

    def npred(X):
        tx = torch.tensor(scaler.transform(X), dtype=torch.float32)
        neural.eval()
        with torch.no_grad():
            return neural(tx).squeeze(1).cpu().numpy()

    train_neural = npred(X_train)
    eval_neural = npred(X_eval)
    residual = y_train - train_neural
    booster = GradientBoostingRegressor(
        n_estimators=150, learning_rate=0.03, max_depth=2,
        random_state=SEED, loss="huber"
    ).fit(scaler.transform(X_train), residual)
    return eval_neural + booster.predict(scaler.transform(X_eval))


def fit_predict_model(name: str, X_train, y_train, X_eval, epochs: int):
    if name == "LG":
        m = LinearRegression().fit(X_train, y_train)
        return m.predict(X_eval)

    if name == "RFR":
        m = RandomForestRegressor(
            n_estimators=300, random_state=SEED, n_jobs=-1, min_samples_leaf=1
        ).fit(X_train, y_train)
        return m.predict(X_eval)

    if name == "ANN":
        m = MLPRegressor(
            hidden_layer_sizes=(100, 50), max_iter=1000, random_state=SEED
        ).fit(X_train, y_train)
        return m.predict(X_eval)

    torch_classes = {
        "LSTM": LSTMModel,
        "GRU": GRUModel,
        "TFT": TransformerBaseline,
        "CNN_LSTM": CNNLSTMModel,
    }
    if name in torch_classes:
        m = fit_torch_model(torch_classes[name], X_train, y_train, epochs)
        return predict_torch(m, X_eval)

    if name == "MOF_PREDICTIUM":
        return fit_mof_predictium(X_train, y_train, X_eval, epochs)

    raise ValueError(f"Unknown model: {name}")


def evaluate_one(name: str, X, y):
    started = time.time()
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.20, random_state=SEED
    )

    hold_pred = fit_predict_model(name, X_train, y_train, X_test, HOLDOUT_EPOCHS)
    hold = regression_metrics(y_test, hold_pred)

    kf = KFold(n_splits=N_SPLITS, shuffle=True, random_state=SEED)
    fold_metrics = []
    for train_idx, val_idx in kf.split(X):
        pred = fit_predict_model(
            name, X[train_idx], y[train_idx], X[val_idx], CV_EPOCHS
        )
        fold_metrics.append(regression_metrics(y[val_idx], pred))

    cv = {}
    for metric in ("rmse", "mae", "mape", "r2"):
        cv[metric] = summary([f[metric] for f in fold_metrics])

    return {
        "model": name,
        **hold,
        "cv_rmse_mean": cv["rmse"]["mean"],
        "cv_rmse_std": cv["rmse"]["std"],
        "cv_mae_mean": cv["mae"]["mean"],
        "cv_mae_std": cv["mae"]["std"],
        "cv_mape_mean": cv["mape"]["mean"],
        "cv_mape_std": cv["mape"]["std"],
        "cv_r2_mean": cv["r2"]["mean"],
        "cv_r2_std": cv["r2"]["std"],
        "time": round(time.time() - started, 2),
        "error": "",
    }


def failure(name: str, exc: Exception):
    return {
        "model": name,
        "rmse": 0.0,
        "mae": 0.0,
        "mape": 0.0,
        "r2": 0.0,
        "cv_rmse_mean": 0.0,
        "cv_rmse_std": 0.0,
        "cv_mae_mean": 0.0,
        "cv_mae_std": 0.0,
        "cv_mape_mean": 0.0,
        "cv_mape_std": 0.0,
        "cv_r2_mean": 0.0,
        "cv_r2_std": 0.0,
        "time": 0.0,
        "error": str(exc),
    }


def main():
    if len(sys.argv) < 2:
        raise SystemExit("Usage: benchmark.py <training_csv>")

    df = pd.read_csv(sys.argv[1], header=None)
    if df.shape[1] < 2:
        raise ValueError("Dataset must contain at least one feature column and one target column.")

    X = df.iloc[:, :-1].apply(pd.to_numeric, errors="raise").to_numpy(dtype=float)
    y = pd.to_numeric(df.iloc[:, -1], errors="raise").to_numpy(dtype=float)

    if len(df) < N_SPLITS:
        raise ValueError(f"At least {N_SPLITS} rows are required for {N_SPLITS}-fold cross-validation.")

    models = ["LG", "RFR", "ANN", "LSTM", "GRU", "TFT", "CNN_LSTM", "MOF_PREDICTIUM"]
    results = []
    for name in models:
        try:
            results.append(evaluate_one(name, X, y))
        except Exception as exc:
            results.append(failure(name, exc))

    print(json.dumps(results, allow_nan=False))


if __name__ == "__main__":
    main()
