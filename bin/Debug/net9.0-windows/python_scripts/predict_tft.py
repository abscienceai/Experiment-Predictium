import sys
import numpy as np
import pandas as pd
import torch
import torch.nn as nn

train_file = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))

df = pd.read_csv(train_file, header=None)
X = torch.tensor(df.iloc[:, :-1].values, dtype=torch.float32)
y = torch.tensor(df.iloc[:, -1].values, dtype=torch.float32).view(-1, 1)
predict_input = torch.tensor(predict_input, dtype=torch.float32).unsqueeze(0)

class SimpleTransformer(nn.Module):
    def __init__(self, input_dim):
        super().__init__()
        self.encoder = nn.Linear(input_dim, 64)
        self.transformer = nn.TransformerEncoder(
            nn.TransformerEncoderLayer(d_model=64, nhead=4),
            num_layers=1
        )
        self.decoder = nn.Linear(64, 1)

    def forward(self, x):
        x = self.encoder(x).unsqueeze(1)
        x = self.transformer(x)
        return self.decoder(x[:, 0, :])

model = SimpleTransformer(X.shape[1])
loss_fn = nn.MSELoss()
optimizer = torch.optim.Adam(model.parameters(), lr=0.01)

for epoch in range(200):
    model.train()
    pred = model(X)
    loss = loss_fn(pred, y)
    optimizer.zero_grad()
    loss.backward()
    optimizer.step()

model.eval()
with torch.no_grad():
    output = model(predict_input)
    print(output.item())
