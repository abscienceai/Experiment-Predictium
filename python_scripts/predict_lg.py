import sys, os
import numpy as np
import pandas as pd
import joblib
from sklearn.linear_model import LinearRegression

train_file    = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))
model_path    = sys.argv[3] if len(sys.argv) > 3 else None

df = pd.read_csv(train_file, header=None)
X  = df.iloc[:, :-1].values
y  = df.iloc[:, -1].values

if model_path and os.path.exists(model_path):
    model = joblib.load(model_path)
else:
    model = LinearRegression()
    model.fit(X, y)
    if model_path:
        os.makedirs(os.path.dirname(model_path), exist_ok=True)
        joblib.dump(model, model_path)

print(model.predict([predict_input])[0])