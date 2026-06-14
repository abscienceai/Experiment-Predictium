import sys, os
import numpy as np
import pandas as pd
import torch
import torch.nn as nn

train_file    = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))
model_path    = sys.argv[3] if len(sys.argv) > 3 else None

df         = pd.read_csv(train_file, header=None)
input_size = df.shape[1] - 1
X = torch.tensor(df.iloc[:, :-1].values, dtype=torch.float32)
y = torch.tensor(df.iloc[:, -1].values,  dtype=torch.float32).view(-1, 1)
P = torch.tensor(predict_input, dtype=torch.float32).unsqueeze(0)

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

model = TFTModel(input_size)
if model_path and os.path.exists(model_path):
    model.load_state_dict(torch.load(model_path, weights_only=True))
else:
    opt = torch.optim.Adam(model.parameters(), lr=0.01)
    loss_fn = nn.MSELoss()
    for _ in range(200):
        model.train(); loss = loss_fn(model(X), y)
        opt.zero_grad(); loss.backward(); opt.step()
    if model_path:
        os.makedirs(os.path.dirname(model_path), exist_ok=True)
        torch.save(model.state_dict(), model_path)

model.eval()
with torch.no_grad():
    print(model(P).item())