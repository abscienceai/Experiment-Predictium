import sys
import numpy as np
import pandas as pd
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import LSTM, Dense

train_file = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))

df = pd.read_csv(train_file, header=None)
X = df.iloc[:, :-1].values
y = df.iloc[:, -1].values

X = np.reshape(X, (X.shape[0], 1, X.shape[1]))
predict_input = np.reshape(predict_input, (1, 1, -1))

model = Sequential()
model.add(LSTM(32, input_shape=(1, X.shape[2])))
model.add(Dense(1))
model.compile(optimizer='adam', loss='mse')
model.fit(X, y, epochs=100, verbose=0)

prediction = model.predict(predict_input, verbose=0)
print(prediction[0][0])
