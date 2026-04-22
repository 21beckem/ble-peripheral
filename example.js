import ble from './index.js';

// 1. Check hardware support before doing anything
const check = await ble.isSupported();
if (!check.success) {
  console.error('BLE not available:', check.reason);
  process.exit(1);
}
console.log('BLE supported:', check.reason);

// 2. Register event handlers
ble.on('ready',        ({   deviceName   }) => console.log(`Advertising as ${deviceName} - waiting for connection...`));
ble.on('connection',   ({    deviceId    }) => console.log('Connected:   ', deviceId));
ble.on('disconnection',({    deviceId    }) => console.log('Disconnected:', deviceId));
ble.on('data',         ({ deviceId, data }) => {
  console.log(`[${deviceId.slice(-5)}] ${data}`);
  // plug this into your game input handler here
});
ble.on('error', err => console.error('BLE error:', err.message));

// 3. Start
ble.begin({
  serviceUuid: 'a07498ca-ad5b-474e-940d-16f1fbe7e8cd',
  charUuid:    '51ff12bb-3ed8-46e5-b4f9-d64e2fec021b'
});

// 4. Stop cleanly on exit
process.on('SIGINT', () => {
  ble.stop();
  process.exit(0);
});
