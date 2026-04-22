import { spawn }       from 'child_process';
import { EventEmitter } from 'events';
import fs   from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const EXE       = path.join(__dirname, 'BlePeripheral.exe');

class BleController extends EventEmitter {
  #proc = null;
  #buf  = '';

  /**
   * Check if this machine can run BLE peripheral mode.
   * @returns {{ success: boolean, reason: string }}
   */
  isSupported() {
    return new Promise((resolve) => {
      if (!fs.existsSync(EXE)) {
        return resolve({ success: false, reason: 'BlePeripheral.exe not found next to index.js' });
      }

      const proc = spawn(EXE, ['--check']);
      let out = '';

      proc.stdout.on('data', d => out += d.toString());
      proc.on('close', () => {
        try {
          resolve(JSON.parse(out.trim()));
        } catch {
          resolve({ success: false, reason: 'Could not parse --check response from BlePeripheral.exe' });
        }
      });
      proc.on('error', err => {
        resolve({ success: false, reason: `Failed to launch exe: ${err.message}` });
      });
    });
  }

  /**
   * Remove non-printable and JSON-parsable characters and trim whitespace from a string.
   * @param {string} str 
   * @returns {string}
   */
  #cleanString(str) {
    return str.replace(/[^\x20-\x7E]/g, '').trim();
  }

  /**
   * Start the BLE peripheral and begin advertising.
   * Emits: 'ready', 'connection', 'disconnection', 'data', 'error'
   */
  begin({serviceUuid, charUuid} = {}) {
    if (this.#proc) return;
    if (typeof serviceUuid !== 'string' || !serviceUuid.trim())
      throw new Error('serviceUuid is required');
    if (typeof charUuid !== 'string' || !charUuid.trim())
      throw new Error('charUuid is required');

    this.#proc = spawn(EXE, [
      '--service', serviceUuid,
      '--char',    charUuid
    ], { stdio: ['pipe', 'pipe', 'pipe'] });

    this.#proc.stdout.on('data', chunk => {
      this.#buf += chunk.toString();
      const lines = this.#buf.split('\n');
      this.#buf = lines.pop(); // hold incomplete trailing line

      for (const line of lines) {
        const cleanLine = this.#cleanString(line);
        if (!cleanLine) continue;
        try {
          const { event, ...rest } = JSON.parse(cleanLine);
          if (event) this.emit(event, rest);
        } catch {
          // ignore malformed JSON lines
        }
      }
    });

    this.#proc.stderr.on('data', d => {
      this.emit('error', new Error(d.toString().trim()));
    });

    this.#proc.on('close', code => {
      this.#proc = null;
      if (code !== 0 && code !== null) {
        this.emit('error', new Error(`BlePeripheral.exe exited with code ${code}`));
      }
    });

    this.#proc.on('error', err => {
      this.emit('error', err);
    });
  }

  /**
   * Gracefully stop the BLE peripheral.
   */
  stop() {
    if (!this.#proc) return;
    try {
      this.#proc.stdin.write(JSON.stringify({ cmd: 'stop' }) + '\n');
    } catch {
      // process may already be gone
    }
  }
}

export default new BleController();
