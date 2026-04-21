"""
Round 26 stress harness for the sovereign MPMC submission.

This script reads the current Round 26 MpmcPipeline source, builds a temporary
.NET console program that exercises real multi-threaded scenarios, and writes:

- docs/battle_v26_stress.json
- docs/battle_v26_stress.md
"""

import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path


BASE_DIR = Path(r"c:\WSGTA\universal-or-strategy")
PIPELINE_FILE = Path(r"C:\tmp\arena_round_26\sub_01\MpmcPipeline.cs")
RESULTS_JSON = BASE_DIR / r"docs\battle_v26_stress.json"
RESULTS_MD = BASE_DIR / r"docs\battle_v26_stress.md"


def load_pipeline_source():
    return PIPELINE_FILE.read_text(encoding="utf-8")


def build_program_source(pipeline_source):
    template = r"""
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

__PIPELINE_SOURCE__

namespace Round26Stress
{
    internal sealed class ScenarioSummary
    {
        public string ScenarioName { get; set; }
        public int LaneCount { get; set; }
        public int LaneCapacity { get; set; }
        public int ProducerThreadCount { get; set; }
        public int ConsumerThreadCount { get; set; }
        public long ProducedCount { get; set; }
        public long ReceivedCount { get; set; }
        public long DuplicateCount { get; set; }
        public long LostCount { get; set; }
        public long PhantomCount { get; set; }
        public long ExceptionCount { get; set; }
        public long ResidualReceiveCount { get; set; }
        public bool TimedOut { get; set; }
        public double CompletionTimeMs { get; set; }
        public bool Pass { get; set; }
        public List<string> ErrorMessages { get; set; }
    }

    internal sealed class HarnessSummary
    {
        public int LogicalProcessorCount { get; set; }
        public bool ReducedHardwareValidation { get; set; }
        public List<ScenarioSummary> Scenarios { get; set; }
    }

    internal static class Program
    {
        private const long PayloadLaneStride = 1000000L;

        private static int Main()
        {
            int logicalProcessorCount = Environment.ProcessorCount;
            int laneCount = Math.Min(32, Math.Max(4, logicalProcessorCount));

            var scenarios = new List<ScenarioSummary>();
            scenarios.Add(RunBalancedScenario(laneCount));
            scenarios.Add(RunParkReacquireScenario(laneCount));
            scenarios.Add(RunCapacityPressureScenario(laneCount));
            scenarios.Add(RunEmptyLaneSkewScenario(laneCount));

            var payload = new HarnessSummary
            {
                LogicalProcessorCount = logicalProcessorCount,
                ReducedHardwareValidation = logicalProcessorCount < 32,
                Scenarios = scenarios
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            Console.WriteLine(JsonSerializer.Serialize(payload, options));

            bool allPassed = true;
            for (int i = 0; i < scenarios.Count; i++)
            {
                if (!scenarios[i].Pass)
                {
                    allPassed = false;
                    break;
                }
            }

            return allPassed ? 0 : 1;
        }

        private static ScenarioSummary RunBalancedScenario(int laneCount)
        {
            var itemsByLane = CreateItemsByLane(laneCount, 100000);
            return RunScenario(
                scenarioName: "Balanced 32-Lane Throughput",
                laneCount: laneCount,
                laneCapacity: 256,
                itemsByLane: itemsByLane,
                delayedStartLane: laneCount,
                delayedStartMs: 0,
                timeoutMs: 30000);
        }

        private static ScenarioSummary RunParkReacquireScenario(int laneCount)
        {
            var itemsByLane = CreateItemsByLane(laneCount, 50000);
            int firstWaveCount = Math.Min(2, laneCount);
            return RunScenario(
                scenarioName: "Steal / Park / Reacquire",
                laneCount: laneCount,
                laneCapacity: 256,
                itemsByLane: itemsByLane,
                delayedStartLane: firstWaveCount,
                delayedStartMs: 250,
                timeoutMs: 30000);
        }

        private static ScenarioSummary RunCapacityPressureScenario(int laneCount)
        {
            var itemsByLane = CreateItemsByLane(laneCount, 20000);
            return RunScenario(
                scenarioName: "Capacity Pressure",
                laneCount: laneCount,
                laneCapacity: 4,
                itemsByLane: itemsByLane,
                delayedStartLane: laneCount,
                delayedStartMs: 0,
                timeoutMs: 30000);
        }

        private static ScenarioSummary RunEmptyLaneSkewScenario(int laneCount)
        {
            int hotLaneCount = Math.Min(2, laneCount);
            var itemsByLane = new int[laneCount];
            for (int laneId = 0; laneId < hotLaneCount; laneId++)
                itemsByLane[laneId] = 150000;

            return RunScenario(
                scenarioName: "Empty-Lane Skew",
                laneCount: laneCount,
                laneCapacity: 256,
                itemsByLane: itemsByLane,
                delayedStartLane: laneCount,
                delayedStartMs: 0,
                timeoutMs: 30000);
        }

        private static int[] CreateItemsByLane(int laneCount, int itemsPerLane)
        {
            var itemsByLane = new int[laneCount];
            for (int laneId = 0; laneId < laneCount; laneId++)
                itemsByLane[laneId] = itemsPerLane;
            return itemsByLane;
        }

        private static ScenarioSummary RunScenario(
            string scenarioName,
            int laneCount,
            int laneCapacity,
            int[] itemsByLane,
            int delayedStartLane,
            int delayedStartMs,
            int timeoutMs)
        {
            var pipeline = new MpmcPipeline(laneCount, laneCapacity);
            var errors = new ConcurrentQueue<string>();
            var startGate = new ManualResetEventSlim(false);
            var delayedGate = new ManualResetEventSlim(delayedStartMs == 0);
            var threads = new List<Thread>(laneCount * 2);

            var seen = new int[laneCount][];
            long producedCount = 0;
            for (int laneId = 0; laneId < laneCount; laneId++)
            {
                int itemCount = itemsByLane[laneId];
                seen[laneId] = new int[itemCount];
                producedCount += itemCount;
            }

            int cancel = 0;
            int completedProducers = 0;
            long receivedCount = 0;
            long uniqueReceivedCount = 0;
            long duplicateCount = 0;
            long phantomCount = 0;
            long exceptionCount = 0;
            long residualReceiveCount = 0;
            long lastProgressTimestamp = Stopwatch.GetTimestamp();
            long stallTicks = Stopwatch.Frequency * 2L;

            void ObserveItem(double value)
            {
                Interlocked.Increment(ref receivedCount);
                Volatile.Write(ref lastProgressTimestamp, Stopwatch.GetTimestamp());

                long raw = (long)Math.Round(value);
                int laneId = (int)(raw / PayloadLaneStride);
                int sequence = (int)(raw % PayloadLaneStride);

                if (laneId < 0 || laneId >= laneCount)
                {
                    Interlocked.Increment(ref phantomCount);
                    return;
                }

                if (sequence < 0 || sequence >= seen[laneId].Length)
                {
                    Interlocked.Increment(ref phantomCount);
                    return;
                }

                int seenCount = Interlocked.Increment(ref seen[laneId][sequence]);
                if (seenCount == 1)
                    Interlocked.Increment(ref uniqueReceivedCount);
                else
                    Interlocked.Increment(ref duplicateCount);
            }

            for (int laneId = 0; laneId < laneCount; laneId++)
            {
                int producerLaneId = laneId;
                var producerThread = new Thread(() =>
                {
                    try
                    {
                        startGate.Wait();

                        if (producerLaneId >= delayedStartLane)
                            delayedGate.Wait();

                        int targetCount = itemsByLane[producerLaneId];
                        for (int sequence = 0; sequence < targetCount; sequence++)
                        {
                            if (Volatile.Read(ref cancel) != 0)
                                break;

                            double payload = (double)((producerLaneId * PayloadLaneStride) + sequence);
                            int spinCount = 0;
                            while (Volatile.Read(ref cancel) == 0)
                            {
                                if (pipeline.TrySend(producerLaneId, payload))
                                    break;

                                Thread.SpinWait(64);
                                spinCount++;
                                if ((spinCount & 255) == 0)
                                    Thread.Yield();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref exceptionCount);
                        errors.Enqueue("producer lane " + producerLaneId + ": " + ex.Message);
                    }
                    finally
                    {
                        Interlocked.Increment(ref completedProducers);
                    }
                });

                producerThread.IsBackground = true;
                producerThread.Start();
                threads.Add(producerThread);
            }

            for (int laneId = 0; laneId < laneCount; laneId++)
            {
                int consumerLaneId = laneId;
                var consumerThread = new Thread(() =>
                {
                    try
                    {
                        startGate.Wait();

                        int missCount = 0;
                        while (Volatile.Read(ref cancel) == 0)
                        {
                            double item;
                            if (pipeline.TryReceive(consumerLaneId, out item))
                            {
                                ObserveItem(item);
                                missCount = 0;
                                continue;
                            }

                            missCount++;

                            if (Volatile.Read(ref completedProducers) >= laneCount)
                            {
                                if (Volatile.Read(ref uniqueReceivedCount) >= producedCount)
                                    break;

                                long idleTicks = Stopwatch.GetTimestamp() - Volatile.Read(ref lastProgressTimestamp);
                                if (idleTicks > stallTicks)
                                    break;
                            }

                            if ((missCount & 255) == 0)
                                Thread.Yield();
                            else
                                Thread.SpinWait(64);
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref exceptionCount);
                        errors.Enqueue("consumer lane " + consumerLaneId + ": " + ex.Message);
                    }
                });

                consumerThread.IsBackground = true;
                consumerThread.Start();
                threads.Add(consumerThread);
            }

            Thread releaseThread = null;
            if (delayedStartMs > 0)
            {
                releaseThread = new Thread(() =>
                {
                    try
                    {
                        startGate.Wait();
                        Thread.Sleep(delayedStartMs);
                        delayedGate.Set();
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref exceptionCount);
                        errors.Enqueue("delayed gate: " + ex.Message);
                    }
                });
                releaseThread.IsBackground = true;
                releaseThread.Start();
            }

            var stopwatch = Stopwatch.StartNew();
            startGate.Set();

            bool timedOut = false;
            long deadline = Environment.TickCount64 + timeoutMs;
            for (int index = 0; index < threads.Count; index++)
            {
                int remainingMs = (int)Math.Max(0L, deadline - Environment.TickCount64);
                if (!threads[index].Join(remainingMs))
                {
                    timedOut = true;
                    break;
                }
            }

            if (timedOut)
            {
                Volatile.Write(ref cancel, 1);
                delayedGate.Set();
                for (int index = 0; index < threads.Count; index++)
                {
                    if (threads[index].IsAlive)
                        threads[index].Join(5000);
                }
            }

            if (releaseThread != null && releaseThread.IsAlive)
                releaseThread.Join(1000);

            for (int sweep = 0; sweep < laneCount * 2; sweep++)
            {
                bool madeProgress = false;
                for (int laneId = 0; laneId < laneCount; laneId++)
                {
                    int laneDrainCount = 0;
                    while (laneDrainCount < laneCapacity * 2)
                    {
                        double item;
                        if (!pipeline.TryReceive(laneId, out item))
                            break;

                        ObserveItem(item);
                        Interlocked.Increment(ref residualReceiveCount);
                        laneDrainCount++;
                        madeProgress = true;
                    }
                }

                if (!madeProgress)
                    break;
            }

            stopwatch.Stop();

            long lostCount = 0;
            for (int laneId = 0; laneId < laneCount; laneId++)
            {
                int[] laneSeen = seen[laneId];
                for (int sequence = 0; sequence < laneSeen.Length; sequence++)
                {
                    if (laneSeen[sequence] == 0)
                        lostCount++;
                }
            }

            var errorMessages = new List<string>();
            string error;
            while (errorMessages.Count < 8 && errors.TryDequeue(out error))
                errorMessages.Add(error);

            bool pass =
                !timedOut &&
                duplicateCount == 0 &&
                lostCount == 0 &&
                phantomCount == 0 &&
                exceptionCount == 0 &&
                residualReceiveCount == 0 &&
                receivedCount == producedCount;

            return new ScenarioSummary
            {
                ScenarioName = scenarioName,
                LaneCount = laneCount,
                LaneCapacity = laneCapacity,
                ProducerThreadCount = laneCount,
                ConsumerThreadCount = laneCount,
                ProducedCount = producedCount,
                ReceivedCount = receivedCount,
                DuplicateCount = duplicateCount,
                LostCount = lostCount,
                PhantomCount = phantomCount,
                ExceptionCount = exceptionCount,
                ResidualReceiveCount = residualReceiveCount,
                TimedOut = timedOut,
                CompletionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                Pass = pass,
                ErrorMessages = errorMessages
            };
        }
    }
}
"""
    return template.replace("__PIPELINE_SOURCE__", pipeline_source)


def write_temp_project(temp_dir, program_source):
    csproj = Path(temp_dir) / "Round26Stress.csproj"
    program = Path(temp_dir) / "Program.cs"

    csproj.write_text(
        """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Optimize>true</Optimize>
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
  </PropertyGroup>
</Project>
""",
        encoding="utf-8",
    )
    program.write_text(program_source, encoding="utf-8")
    return csproj


def run_harness(csproj_path):
    env = os.environ.copy()
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    result = subprocess.run(
        ["dotnet", "run", "--project", str(csproj_path), "-c", "Release", "-v", "q"],
        capture_output=True,
        text=True,
        timeout=600,
        env=env,
    )

    if result.returncode not in (0, 1):
        raise RuntimeError(
            "Stress harness build/run failed.\nSTDOUT:\n{0}\nSTDERR:\n{1}".format(
                result.stdout, result.stderr
            )
        )

    stdout = result.stdout.strip()
    if not stdout:
        raise RuntimeError("Stress harness produced no stdout.")

    json_start = stdout.find("{")
    if json_start >= 0:
        stdout = stdout[json_start:]

    try:
        return json.loads(stdout), result.returncode
    except json.JSONDecodeError as exc:
        raise RuntimeError(
            "Failed to parse stress harness JSON.\nSTDOUT:\n{0}\nSTDERR:\n{1}".format(
                result.stdout, result.stderr
            )
        ) from exc


def write_outputs(payload):
    RESULTS_JSON.write_text(json.dumps(payload, indent=2), encoding="utf-8")

    lines = []
    lines.append("# Round 26 Stress Harness Results")
    lines.append("")

    logical = payload.get("logicalProcessorCount", 0)
    reduced = payload.get("reducedHardwareValidation", False)
    if reduced:
        lines.append(
            "Reduced-hardware validation: {0} logical processors detected; mission target is 32.".format(
                logical
            )
        )
    else:
        lines.append(
            "Full hardware validation: {0} logical processors detected.".format(logical)
        )
    lines.append("")

    lines.append(
        "| Scenario | Pass | Lanes | Cap | Producers | Consumers | Produced | Received | Dup | Lost | Phantom | Exceptions | Residual | Timed Out | ms |"
    )
    lines.append(
        "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|"
    )

    failures = []
    for scenario in payload.get("scenarios", []):
        passed = "PASS" if scenario.get("pass") else "FAIL"
        if passed == "FAIL":
            failures.append(scenario.get("scenarioName", "unknown"))
        lines.append(
            "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} | {12} | {13} | {14:.3f} |".format(
                scenario.get("scenarioName", "unknown"),
                passed,
                scenario.get("laneCount", 0),
                scenario.get("laneCapacity", 0),
                scenario.get("producerThreadCount", 0),
                scenario.get("consumerThreadCount", 0),
                scenario.get("producedCount", 0),
                scenario.get("receivedCount", 0),
                scenario.get("duplicateCount", 0),
                scenario.get("lostCount", 0),
                scenario.get("phantomCount", 0),
                scenario.get("exceptionCount", 0),
                scenario.get("residualReceiveCount", 0),
                "yes" if scenario.get("timedOut") else "no",
                scenario.get("completionTimeMs", 0.0),
            )
        )

    lines.append("")
    if failures:
        lines.append("Conclusion: failures detected in {0}.".format(", ".join(failures)))
    else:
        lines.append("Conclusion: all configured stress scenarios passed.")

    RESULTS_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    pipeline_source = load_pipeline_source()
    program_source = build_program_source(pipeline_source)

    with tempfile.TemporaryDirectory(prefix="round26_stress_") as temp_dir:
        csproj_path = write_temp_project(temp_dir, program_source)
        payload, return_code = run_harness(csproj_path)

    write_outputs(payload)

    print(
        "Round 26 stress results written to {0} and {1}".format(
            RESULTS_JSON, RESULTS_MD
        )
    )

    if return_code != 0:
        sys.exit(return_code)


if __name__ == "__main__":
    main()
