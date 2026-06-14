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
X = torch.tensor(df.iloc[:, :-1].values, dtype=torch.float32).unsqueeze(1)
y = torch.tensor(df.iloc[:, -1].values,  dtype=torch.float32).view(-1, 1)
P = torch.tensor(predict_input, dtype=torch.float32).unsqueeze(0).unsqueeze(0)

class GRUModel(nn.Module):
    def __init__(self, n):
        super().__init__()
        self.gru = nn.GRU(n, 32, batch_first=True)
        self.fc  = nn.Linear(32, 1)
    def forward(self, x):
        out, _ = self.gru(x)
        return self.fc(out[:, -1, :])

model = GRUModel(input_size)
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