import sys
import numpy as np
import pandas as pd
from sklearn.linear_model import LinearRegression

train_file = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))

df = pd.read_csv(train_file, header=None)
X = df.iloc[:, :-1].values
y = df.iloc[:, -1].values

model = LinearRegression()
model.fit(X, y)

pred = model.predict([predict_input])
print(pred[0])
