# Round 26 Stress Harness Results

Reduced-hardware validation: 12 logical processors detected; mission target is 32.

| Scenario | Pass | Lanes | Cap | Producers | Consumers | Produced | Received | Dup | Lost | Phantom | Exceptions | Residual | Timed Out | ms |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|
| Balanced 32-Lane Throughput | PASS | 12 | 256 | 12 | 12 | 1200000 | 1200000 | 0 | 0 | 0 | 0 | 0 | no | 90.995 |
| Steal / Park / Reacquire | PASS | 12 | 256 | 12 | 12 | 600000 | 600000 | 0 | 0 | 0 | 0 | 0 | no | 310.350 |
| Capacity Pressure | PASS | 12 | 4 | 12 | 12 | 240000 | 240000 | 0 | 0 | 0 | 0 | 0 | no | 40.212 |
| Empty-Lane Skew | PASS | 12 | 256 | 12 | 12 | 300000 | 300000 | 0 | 0 | 0 | 0 | 0 | no | 35.585 |

Conclusion: all configured stress scenarios passed.
