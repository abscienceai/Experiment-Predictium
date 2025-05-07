import sys
import numpy as np
import pandas as pd
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import Conv1D, LSTM, Dense, MaxPooling1D, Flatten, Reshape

train_file = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))

df = pd.read_csv(train_file, header=None)
X = df.iloc[:, :-1].values
y = df.iloc[:, -1].values

# CNN+LSTM yapısı için reshape
X = np.reshape(X, (X.shape[0], X.shape[1], 1))
predict_input = np.reshape(predict_input, (1, len(predict_input), 1))

model = Sequential()
model.add(Conv1D(filters=64, kernel_size=1, activation='relu', input_shape=(X.shape[1], 1)))
model.add(MaxPooling1D(pool_size=1))
model.add(LSTM(50))
model.add(Dense(1))
model.compile(optimizer='adam', loss='mse')
model.fit(X, y, epochs=100, verbose=0)

prediction = model.predict(predict_input, verbose=0)
print(prediction[0][0])
