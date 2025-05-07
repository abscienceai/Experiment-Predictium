import sys
import numpy as np
from sklearn.neural_network import MLPRegressor
import pandas as pd

train_file = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))

# Veriyi oku
df = pd.read_csv(train_file, header=None)
X = df.iloc[:, :-1].values
y = df.iloc[:, -1].values

# Modeli eğit
model = MLPRegressor(hidden_layer_sizes=(50,), max_iter=1000)
model.fit(X, y)

# Tahmin yap
pred = model.predict([predict_input])
print(pred[0])
