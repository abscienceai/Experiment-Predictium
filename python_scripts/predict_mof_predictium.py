"""Train/load the MOF-Predictium hybrid regressor and make one prediction.

Architecture:
- 1D convolutional local descriptor extractor
- multi-head self-attention
- gradient-boosted residual correction

The saved .pkl contains the neural state_dict, scaler, residual booster, and input size.
"""
from __future__ import annotations

import os
import sys
import joblib
import numpy as np
import pandas as pd
import torch
import torch.nn as nn
from sklearn.ensemble import GradientBoostingRegressor
from sklearn.preprocessing import StandardScaler

SEED = 42
np.random.seed(SEED)
torch.manual_seed(SEED)

train_file = sys.argv[1]
predict_input = np.asarray(list(map(float, sys.argv[2].split(","))), dtype=float)
model_path = sys.argv[3] if len(sys.argv) > 3 else None


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


def neural_predict(model, scaler, X):
    xt = torch.tensor(scaler.transform(X), dtype=torch.float32)
    model.eval()
    with torch.no_grad():
        return model(xt).squeeze(1).cpu().numpy()


def train_payload(X, y):
    scaler = StandardScaler().fit(X)
    xt = torch.tensor(scaler.transform(X), dtype=torch.float32)
    yt = torch.tensor(y, dtype=torch.float32).view(-1, 1)

    model = MOFPredictiumNet(X.shape[1])
    opt = torch.optim.Adam(model.parameters(), lr=0.005)
    loss_fn = nn.MSELoss()
    for _ in range(200):
        pred = model(xt)
        loss = loss_fn(pred, yt)
        opt.zero_grad(); loss.backward(); opt.step()

    base = neural_predict(model, scaler, X)
    residual = y - base
    booster = GradientBoostingRegressor(
        n_estimators=150, learning_rate=0.03, max_depth=2,
        random_state=SEED, loss="huber"
    ).fit(scaler.transform(X), residual)

    return {
        "version": "mof-predictium-v1",
        "n_features": int(X.shape[1]),
        "state_dict": model.state_dict(),
        "scaler": scaler,
        "booster": booster,
    }


def load_model(payload):
    model = MOFPredictiumNet(int(payload["n_features"]))
    model.load_state_dict(payload["state_dict"])
    return model


df = pd.read_csv(train_file, header=None)
X = df.iloc[:, :-1].to_numpy(dtype=float)
y = df.iloc[:, -1].to_numpy(dtype=float)

if len(predict_input) != X.shape[1]:
    raise ValueError(f"Expected {X.shape[1]} input values, received {len(predict_input)}.")

if model_path and os.path.exists(model_path):
    payload = joblib.load(model_path)
else:
    payload = train_payload(X, y)
    if model_path:
        parent = os.path.dirname(model_path)
        if parent:
            os.makedirs(parent, exist_ok=True)
        joblib.dump(payload, model_path)

model = load_model(payload)
scaler = payload["scaler"]
booster = payload["booster"]
P = predict_input.reshape(1, -1)
base = neural_predict(model, scaler, P)[0]
correction = booster.predict(scaler.transform(P))[0]
print(float(base + correction))
