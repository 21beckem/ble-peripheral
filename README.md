# BLE Peripheral Bridge (Windows)

Small Node.js wrapper around a native C# BLE peripheral executable.

- `index.js` starts and controls `BlePeripheral.exe`
- `BlePeripheral.exe` hosts a BLE GATT service on Windows and streams events as JSON lines
- `example.js` is a quick smoke test

## Requirements

- Windows with BLE hardware
- BLE adapter that supports **Peripheral role**
- Node.js 18+
- .NET 8 SDK (only needed if rebuilding the C# executable)

## Project Layout

- `index.js`: library entry point (`BleController`)
- `example.js`: simple usage example
- `ble-server/Program.cs`: native BLE host implementation
- `ble-server/publish.bat`: publishes `BlePeripheral.exe`

## Install

```bash
npm install
```

## Build Native Executable (if needed)

From repo root:

```bash
ble-server\publish.bat
```

This will automatically replace the current exe in the root folder

## Run Example

```bash
npm test
```

Or run your own app:

```bash
npm start
```

## API (Node)

Import:

```js
import ble from './index.js';
```

Methods:

- `await ble.isSupported()` -> `{ success, reason }`
- `ble.begin({ serviceUuid, charUuid })`
- `ble.stop()`

Events:

- `ready`
- `connection` (`deviceId`)
- `disconnection` (`deviceId`)
- `data` (`deviceId`, `data`)
- `error` (`Error`)