"""
Benchmark script for Experiment-Predictium
Trains all 7 models on 80% of data, evaluates on 20%, outputs JSON metrics.
"""
import sys
import json
import time
import warnings
warnings.filterwarnings("ignore")

import numpy as np
import pandas as pd
from sklearn.model_selection import train_test_split
from sklearn.metrics import mean_squared_error, mean_absolute_error, r2_score
from sklearn.linear_model import LinearRegression
from sklearn.ensemble import RandomForestRegressor
from sklearn.neural_network import MLPRegressor
import torch
import torch.nn as nn

train_file = sys.argv[1]
df = pd.read_csv(train_file, header=None)
X = df.iloc[:, :-1].values
y = df.iloc[:, -1].values

# 80/20 train-test split with fixed seed for reproducibility
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42)

results = []

# ── Utility: compute metrics ──────────────────────────────────────
def metrics(y_true, y_pred):
    rmse = float(np.sqrt(mean_squared_error(y_true, y_pred)))
    mae  = float(mean_absolute_error(y_true, y_pred))
    r2   = float(r2_score(y_true, y_pred))
    return rmse, mae, r2

# ── 1. Linear Regression ──────────────────────────────────────────
try:
    t0 = time.time()
    m = LinearRegression()
    m.fit(X_train, y_train)
    preds = m.predict(X_test)
    rmse, mae, r2 = metrics(y_test, preds)
    results.append({"model":"LG","rmse":rmse,"mae":mae,"r2":r2,
                    "time":round(time.time()-t0,2),"error":""})
except Exception as e:
    results.append({"model":"LG","rmse":0,"mae":0,"r2":0,"time":0,"error":str(e)})

# ── 2. Random Forest ──────────────────────────────────────────────
try:
    t0 = time.time()
    m = RandomForestRegressor(n_estimators=100, random_state=42)
    m.fit(X_train, y_train)
    preds = m.predict(X_test)
    rmse, mae, r2 = metrics(y_test, preds)
    results.append({"model":"RFR","rmse":rmse,"mae":mae,"r2":r2,
                    "time":round(time.time()-t0,2),"error":""})
except Exception as e:
    results.append({"model":"RFR","rmse":0,"mae":0,"r2":0,"time":0,"error":str(e)})

# ── 3. ANN (MLP) ──────────────────────────────────────────────────
try:
    t0 = time.time()
    m = MLPRegressor(hidden_layer_sizes=(100, 50), max_iter=1000, random_state=42)
    m.fit(X_train, y_train)
    preds = m.predict(X_test)
    rmse, mae, r2 = metrics(y_test, preds)
    results.append({"model":"ANN","rmse":rmse,"mae":mae,"r2":r2,
                    "time":round(time.time()-t0,2),"error":""})
except Exception as e:
    results.append({"model":"ANN","rmse":0,"mae":0,"r2":0,"time":0,"error":str(e)})

# ── PyTorch helper ────────────────────────────────────────────────
def torch_metrics(model, X_t, y_t):
    model.eval()
    with torch.no_grad():
        preds = model(X_t).squeeze().numpy()
    return metrics(y_t.numpy(), preds)

def train_torch(model, X_t, y_t, epochs=200, lr=0.01):
    opt  = torch.optim.Adam(model.parameters(), lr=lr)
    loss_fn = nn.MSELoss()
    for _ in range(epochs):
        model.train()
        loss = loss_fn(model(X_t), y_t.view(-1,1))
        opt.zero_grad(); loss.backward(); opt.step()

Xtrain_t = torch.tensor(X_train, dtype=torch.float32)
Xtest_t  = torch.tensor(X_test,  dtype=torch.float32)
ytrain_t = torch.tensor(y_train, dtype=torch.float32)
ytest_t  = torch.tensor(y_test,  dtype=torch.float32)
n_feat   = X_train.shape[1]

# ── 4. LSTM ───────────────────────────────────────────────────────
try:
    t0 = time.time()
    class LSTMModel(nn.Module):
        def __init__(self, n):
            super().__init__()
            self.lstm = nn.LSTM(n, 32, batch_first=True)
            self.fc   = nn.Linear(32, 1)
        def forward(self, x):
            out, _ = self.lstm(x.unsqueeze(1))
            return self.fc(out[:, -1, :])
    m = LSTMModel(n_feat)
    train_torch(m, Xtrain_t, ytrain_t)
    rmse, mae, r2 = torch_metrics(m, Xtest_t, ytest_t)
    results.append({"model":"LSTM","rmse":rmse,"mae":mae,"r2":r2,
                    "time":round(time.time()-t0,2),"error":""})
except Exception as e:
    results.append({"model":"LSTM","rmse":0,"mae":0,"r2":0,"time":0,"error":str(e)})

# ── 5. GRU ────────────────────────────────────────────────────────
try:
    t0 = time.time()
    class GRUModel(nn.Module):
        def __init__(self, n):
            super().__init__()
            self.gru = nn.GRU(n, 32, batch_first=True)
            self.fc  = nn.Linear(32, 1)
        def forward(self, x):
            out, _ = self.gru(x.unsqueeze(1))
            return self.fc(out[:, -1, :])
    m = GRUModel(n_feat)
    train_torch(m, Xtrain_t, ytrain_t)
    rmse, mae, r2 = torch_metrics(m, Xtest_t, ytest_t)
    results.append({"model":"GRU","rmse":rmse,"mae":mae,"r2":r2,
                    "time":round(time.time()-t0,2),"error":""})
except Exception as e:
    results.append({"model":"GRU","rmse":0,"mae":0,"r2":0,"time":0,"error":str(e)})

# ── 6. TFT (Transformer) ──────────────────────────────────────────
try:
    t0 = time.time()
    class TFTModel(nn.Module):
        def __init__(self, n):
            super().__init__()
            self.enc = nn.Linear(n, 64)
            self.tf  = nn.TransformerEncoder(
                nn.TransformerEncoderLayer(d_model=64, nhead=4, batch_first=True), 1)
            self.dec = nn.Linear(64, 1)
        def forward(self, x):
            x = self.enc(x).unsqueeze(1)
            return self.dec(self.tf(x)[:, 0, :])
    m = TFTModel(n_feat)
    train_torch(m, Xtrain_t, ytrain_t)
    rmse, mae, r2 = torch_metrics(m, Xtest_t, ytest_t)
    results.append({"model":"TFT","rmse":rmse,"mae":mae,"r2":r2,
                    "time":round(time.time()-t0,2),"error":""})
except Exception as e:
    results.append({"model":"TFT","rmse":0,"mae":0,"r2":0,"time":0,"error":str(e)})

# ── 7. CNN-LSTM ───────────────────────────────────────────────────
try:
    t0 = time.time()
    class CNNLSTMModel(nn.Module):
        def __init__(self, n):
            super().__init__()
            self.conv = nn.Conv1d(1, 32, 1)
            self.lstm = nn.LSTM(32, 50, batch_first=True)
            self.fc   = nn.Linear(50, 1)
        def forward(self, x):
            out = torch.relu(self.conv(x.unsqueeze(1)))
            out, _ = self.lstm(out.permute(0, 2, 1))
            return self.fc(out[:, -1, :])
    m = CNNLSTMModel(n_feat)
    train_torch(m, Xtrain_t, ytrain_t)
    rmse, mae, r2 = torch_metrics(m, Xtest_t, ytest_t)
    results.append({"model":"CNN_LSTM","rmse":rmse,"mae":mae,"r2":r2,
                    "time":round(time.time()-t0,2),"error":""})
except Exception as e:
    results.append({"model":"CNN_LSTM","rmse":0,"mae":0,"r2":0,"time":0,"error":str(e)})

# Output JSON to stdout
print(json.dumps(results))
