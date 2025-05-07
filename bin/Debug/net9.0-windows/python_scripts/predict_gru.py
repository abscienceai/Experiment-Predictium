import sys
import numpy as np
import pandas as pd
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import GRU, Dense

# Argümanlar
train_file = sys.argv[1]
predict_input = list(map(float, sys.argv[2].split(",")))

# Eğitim verisini oku
df = pd.read_csv(train_file, header=None)
X = df.iloc[:, :-1].values
y = df.iloc[:, -1].values

# GRU girişi için 3D shape: (samples, timesteps=1, features)
X = np.reshape(X, (X.shape[0], 1, X.shape[1]))
predict_input = np.reshape(predict_input, (1, 1, -1))

# Modeli oluştur ve eğit
model = Sequential()
model.add(GRU(32, input_shape=(1, X.shape[2])))
model.add(Dense(1))
model.compile(optimizer='adam', loss='mse')
model.fit(X, y, epochs=100, verbose=0)

# Tahmin et
prediction = model.predict(predict_input, verbose=0)
print(prediction[0][0])
