/**
 * BenchmarkRunner — V24 Sub-Nanosecond Performance Harness
 * 
 * Runs continuous benchmarks against the SovereignChannel V24 core,
 * collecting latency distributions, mode transitions, and safety
 * invariant validation results.
 */

import { SovereignChannel, createSovereignChannel } from './SovereignChannel';
import { AdaptiveStripingEngine } from './AdaptiveStriping';

export interface BenchmarkResult {
  iteration: number;
  writeLatency: number;
  readLatency: number;
  handshakeLatency: number;
  mode: 'L1_LOCAL' | 'L2_STRIPED';
  safetyOK: boolean;
  contention: number;
  sequenceNumber: number;
  timestamp: number;
}

export interface BenchmarkStats {
  totalRuns: number;
  avgWriteLatency: number;
  avgReadLatency: number;
  avgHandshakeLatency: number;
  p50WriteLatency: number;
  p99WriteLatency: number;
  p50HandshakeLatency: number;
  p99HandshakeLatency: number;
  minWriteLatency: number;
  maxWriteLatency: number;
  safetyInvariantPassRate: number;
  modeSwitches: number;
  currentMode: 'L1_LOCAL' | 'L2_STRIPED';
  contentionAvg: number;
}

export type BenchmarkCallback = (result: BenchmarkResult, stats: BenchmarkStats) => void;

export class BenchmarkRunner {
  private _channel: SovereignChannel;
  private _striping: AdaptiveStripingEngine;
  private _running: boolean = false;
  private _results: BenchmarkResult[] = [];
  private _iteration: number = 0;
  private _callback: BenchmarkCallback | null = null;
  private _intervalId: ReturnType<typeof setInterval> | null = null;

  constructor(cacheLineSize: number = 64, hardwareConcurrency: number = 4) {
    this._channel = createSovereignChannel(cacheLineSize, hardwareConcurrency);
    this._striping = new AdaptiveStripingEngine(cacheLineSize);
  }

  get channel(): SovereignChannel { return this._channel; }
  get striping(): AdaptiveStripingEngine { return this._striping; }
  get running(): boolean { return this._running; }
  get results(): BenchmarkResult[] { return this._results; }
  get iteration(): number { return this._iteration; }

  /**
   * Run a single benchmark iteration.
   */
  runIteration(): BenchmarkResult {
    // Generate random data payload (simulating telemetry)
    const payload = Array.from({ length: 16 }, () =>
      Math.floor(Math.random() * 0xFFFFFFFF)
    );

    // Write phase
    const t0 = performance.now();
    const writeMetrics = this._channel.write(payload);
    const t1 = performance.now();

    // Read phase
    const { valid } = this._channel.read();
    const t2 = performance.now();

    const writeLatency = (t1 - t0) * 1e6;
    const readLatency = (t2 - t1) * 1e6;
    const handshakeLatency = (t2 - t0) * 1e6;

    // Update striping engine
    this._striping.sample(handshakeLatency);

    this._iteration++;

    const result: BenchmarkResult = {
      iteration: this._iteration,
      writeLatency,
      readLatency,
      handshakeLatency,
      mode: writeMetrics.mode,
      safetyOK: writeMetrics.safetyInvariantOK && valid,
      contention: writeMetrics.contentionLevel,
      sequenceNumber: writeMetrics.sequenceNumber,
      timestamp: performance.now(),
    };

    this._results.push(result);
    if (this._results.length > 1000) {
      this._results = this._results.slice(-500);
    }

    if (this._callback) {
      this._callback(result, this.computeStats());
    }

    return result;
  }

  /**
   * Run a batch of iterations synchronously.
   */
  runBatch(count: number): BenchmarkResult[] {
    const batchResults: BenchmarkResult[] = [];
    for (let i = 0; i < count; i++) {
      batchResults.push(this.runIteration());
    }
    return batchResults;
  }

  /**
   * Start continuous benchmarking at the specified interval (ms).
   */
  start(callback: BenchmarkCallback, intervalMs: number = 10): void {
    if (this._running) return;
    this._running = true;
    this._callback = callback;
    this._intervalId = setInterval(() => {
      if (this._running) {
        this.runIteration();
      }
    }, intervalMs);
  }

  /**
   * Stop continuous benchmarking.
   */
  stop(): void {
    this._running = false;
    if (this._intervalId) {
      clearInterval(this._intervalId);
      this._intervalId = null;
    }
  }

  /**
   * Reset all state.
   */
  reset(): void {
    this.stop();
    this._results = [];
    this._iteration = 0;
    this._callback = null;
    this._channel = createSovereignChannel(
      this._channel.config.cacheLineSize,
      navigator.hardwareConcurrency || 4
    );
    this._striping = new AdaptiveStripingEngine(this._channel.config.cacheLineSize);
  }

  /**
   * Compute aggregate statistics from all results.
   */
  computeStats(): BenchmarkStats {
    const results = this._results;
    if (results.length === 0) {
      return this.emptyStats();
    }

    const writes = results.map(r => r.writeLatency);
    const handshakes = results.map(r => r.handshakeLatency);
    const sortedWrites = [...writes].sort((a, b) => a - b);
    const sortedHandshakes = [...handshakes].sort((a, b) => a - b);

    const safetyPasses = results.filter(r => r.safetyOK).length;

    return {
      totalRuns: results.length,
      avgWriteLatency: writes.reduce((a, b) => a + b, 0) / writes.length,
      avgReadLatency: results.reduce((a, b) => a + b.readLatency, 0) / results.length,
      avgHandshakeLatency: handshakes.reduce((a, b) => a + b, 0) / handshakes.length,
      p50WriteLatency: sortedWrites[Math.floor(sortedWrites.length * 0.5)],
      p99WriteLatency: sortedWrites[Math.floor(sortedWrites.length * 0.99)],
      p50HandshakeLatency: sortedHandshakes[Math.floor(sortedHandshakes.length * 0.5)],
      p99HandshakeLatency: sortedHandshakes[Math.floor(sortedHandshakes.length * 0.99)],
      minWriteLatency: sortedWrites[0],
      maxWriteLatency: sortedWrites[sortedWrites.length - 1],
      safetyInvariantPassRate: safetyPasses / results.length,
      modeSwitches: this._channel.modeSwitches,
      currentMode: this._channel.mode,
      contentionAvg: results.reduce((a, b) => a + b.contention, 0) / results.length,
    };
  }

  private emptyStats(): BenchmarkStats {
    return {
      totalRuns: 0,
      avgWriteLatency: 0,
      avgReadLatency: 0,
      avgHandshakeLatency: 0,
      p50WriteLatency: 0,
      p99WriteLatency: 0,
      p50HandshakeLatency: 0,
      p99HandshakeLatency: 0,
      minWriteLatency: 0,
      maxWriteLatency: 0,
      safetyInvariantPassRate: 0,
      modeSwitches: 0,
      currentMode: 'L1_LOCAL',
      contentionAvg: 0,
    };
  }
}
