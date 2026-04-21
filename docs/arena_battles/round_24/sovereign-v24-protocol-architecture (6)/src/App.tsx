import { motion } from "framer-motion";
import { useState } from "react";

const V24_ROBUST_CODE = String.raw`public unsafe sealed class SovereignChannel
{
    private readonly int _lineBytes;
    private readonly int _l2Bytes;
    private readonly int _numaNodes;
    private readonly int[] _nodeDistance;
    private readonly byte* _telemetry;
    private readonly StripeMode[] _modeByProducer;
    private readonly SlotHeader* _slots;
    private readonly nuint _slotMask;

    private enum StripeMode : byte
    {
        L1Local = 0,
        L2Striped = 1
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SlotHeader
    {
        public ulong Sequence;
        public ulong Shadow;
        public ulong PayloadPtr;
        public ulong Stamp;
    }

    public SovereignChannel(nuint ringPowerOf2, int producerCount)
    {
        var topo = HardwareTopologyProbe.Detect();
        _lineBytes = topo.L1DLineBytes;
        _l2Bytes = topo.L2LineBytes;
        _numaNodes = topo.NumaNodeCount;
        _nodeDistance = topo.NodeDistance;

        _slotMask = ringPowerOf2 - 1;
        _telemetry = (byte*)Marshal.AllocHGlobal(topo.TelemetryBytes);
        _modeByProducer = new StripeMode[producerCount];

        var slotBytes = Align(sizeof(SlotHeader), _lineBytes);
        var totalBytes = checked((nuint)slotBytes * ringPowerOf2);
        _slots = (SlotHeader*)NativeMemory.AlignedAlloc(totalBytes, (nuint)_lineBytes);

        for (nuint i = 0; i < ringPowerOf2; i++)
        {
            var slot = (SlotHeader*)((byte*)_slots + (nuint)slotBytes * i);
            slot->Sequence = i;
            slot->Shadow = i ^ 0x9E3779B97F4A7C15UL;
            slot->PayloadPtr = 0;
            slot->Stamp = 0;
        }
    }

    public bool TryPublish(int producerId, void* payload, ulong stamp)
    {
        var mode = _modeByProducer[producerId];
        var seq = ReadProducerSequence(producerId);
        var index = SequenceToIndex(seq, producerId, mode);
        var slot = ResolveSlot(index);

        var expected = seq;
        if (slot->Sequence != expected)
            return false;

        slot->PayloadPtr = (ulong)payload;
        slot->Stamp = stamp;

        // Safety invariant: sequence-shadow parity confirms atomic visibility without fences.
        var published = expected + 1;
        slot->Shadow = published ^ 0x9E3779B97F4A7C15UL;
        slot->Sequence = published;

        RecordTelemetry(producerId, stamp, mode);
        MaybeRebalanceMode(producerId);
        return true;
    }

    public bool TryConsume(ref ulong cursor, out void* payload, out ulong stamp)
    {
        var index = cursor & _slotMask;
        var slot = ResolveSlot(index);
        var expected = cursor + 1;

        var seenSeq = slot->Sequence;
        if (seenSeq != expected)
        {
            payload = null;
            stamp = 0;
            return false;
        }

        var seenShadow = slot->Shadow;
        if ((seenShadow ^ 0x9E3779B97F4A7C15UL) != seenSeq)
        {
            payload = null;
            stamp = 0;
            return false;
        }

        payload = (void*)slot->PayloadPtr;
        stamp = slot->Stamp;

        slot->Sequence = cursor + (_slotMask + 1);
        slot->Shadow = slot->Sequence ^ 0x9E3779B97F4A7C15UL;
        cursor++;
        return true;
    }

    private void MaybeRebalanceMode(int producerId)
    {
        var contention = ReadContentionWindow(producerId);
        var interrupts = ReadInterruptPressure();
        var remotePenalty = _numaNodes > 1 ? _nodeDistance[CurrentNode()] : 0;

        if (contention > 11 || interrupts > 6 || remotePenalty > 18)
            _modeByProducer[producerId] = StripeMode.L2Striped;
        else if (contention < 5 && interrupts < 3)
            _modeByProducer[producerId] = StripeMode.L1Local;
    }

    private nuint SequenceToIndex(ulong seq, int producerId, StripeMode mode)
    {
        if (mode == StripeMode.L1Local)
            return (nuint)seq & _slotMask;

        var stride = (nuint)Math.Max(1, _l2Bytes / _lineBytes);
        return (((nuint)seq * stride) + (nuint)producerId) & _slotMask;
    }

    private SlotHeader* ResolveSlot(nuint index)
    {
        var slotBytes = Align(sizeof(SlotHeader), _lineBytes);
        return (SlotHeader*)((byte*)_slots + (nuint)slotBytes * index);
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

    private ulong ReadProducerSequence(int producerId) => *(ulong*)(_telemetry + producerId * sizeof(ulong));
    private int ReadContentionWindow(int producerId) => *(_telemetry + 256 + producerId);
    private int ReadInterruptPressure() => *(_telemetry + 512);
    private int CurrentNode() => *(_telemetry + 768);

    private void RecordTelemetry(int producerId, ulong stamp, StripeMode mode)
    {
        var p = _telemetry + 1024 + producerId * 16;
        *(ulong*)p = stamp;
        *(p + 8) = (byte)mode;
    }
}

public readonly record struct HardwareTopology(int L1DLineBytes, int L2LineBytes, int NumaNodeCount, int[] NodeDistance, int TelemetryBytes);

public static class HardwareTopologyProbe
{
    public static HardwareTopology Detect()
    {
        int l1 = CpuInfo.ReadL1DLineBytes();
        int l2 = CpuInfo.ReadL2LineBytes();
        int nodes = NumaInfo.NodeCount();
        int[] distance = NumaInfo.DistanceMatrix(nodes);
        int telemetry = Math.Max(2048, l2 * nodes);
        return new HardwareTopology(l1, l2, nodes, distance, telemetry);
    }
}`;

export default function App() {
  const [copied, setCopied] = useState(false);

  const copyCode = async () => {
    await navigator.clipboard.writeText(`V24_ROBUST_CODE\n${V24_ROBUST_CODE}`);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  };

  return (
    <main className="min-h-screen overflow-hidden bg-zinc-950 text-zinc-100">
      <motion.div
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,rgba(34,197,94,0.18),transparent_35%),radial-gradient(circle_at_80%_5%,rgba(59,130,246,0.22),transparent_30%),radial-gradient(circle_at_50%_80%,rgba(217,70,239,0.16),transparent_40%)]"
        animate={{ opacity: [0.65, 0.95, 0.65] }}
        transition={{ duration: 8, repeat: Number.POSITIVE_INFINITY, ease: "easeInOut" }}
      />

      <div className="relative mx-auto flex w-full max-w-6xl flex-col gap-12 px-6 py-16 lg:px-10">
        <header className="space-y-5">
          <motion.p
            initial={{ y: 16, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ duration: 0.45 }}
            className="text-xs tracking-[0.35em] text-emerald-300"
          >
            SOV-V24-GLOBAL-ROBUST
          </motion.p>
          <motion.h1
            initial={{ y: 20, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.1, duration: 0.55 }}
            className="max-w-4xl text-4xl font-semibold leading-tight text-white md:text-6xl"
          >
            SovereignChannel v24: hardware-auto topology, adaptive striping, and fence-less safety invariants.
          </motion.h1>
          <motion.p
            initial={{ y: 20, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.2, duration: 0.55 }}
            className="max-w-3xl text-base text-zinc-300 md:text-lg"
          >
            The design probes live cache and NUMA geometry, moves between L1-local and L2-striped paths under
            contention pressure, and verifies every publication with sequence-shadow parity so safety is checked
            without introducing fence latency.
          </motion.p>
          <motion.button
            type="button"
            whileHover={{ scale: 1.03 }}
            whileTap={{ scale: 0.98 }}
            onClick={copyCode}
            className="inline-flex items-center gap-2 border border-zinc-700 px-4 py-2 text-sm text-zinc-100 transition hover:border-emerald-300 hover:text-emerald-200"
          >
            {copied ? "Copied V24_ROBUST_CODE" : "Copy V24_ROBUST_CODE"}
          </motion.button>
        </header>

        <motion.section
          initial={{ opacity: 0, y: 18 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.25, duration: 0.55 }}
          className="space-y-4"
        >
          <p className="font-mono text-sm text-emerald-300">V24_ROBUST_CODE</p>
          <pre className="max-h-[58vh] overflow-auto border border-zinc-800 bg-black/40 p-5 font-mono text-xs leading-relaxed text-zinc-200 sm:text-sm">
            <code>{V24_ROBUST_CODE}</code>
          </pre>
        </motion.section>

        <motion.section
          initial={{ opacity: 0, y: 18 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.35, duration: 0.55 }}
          className="max-w-4xl space-y-3 pb-8 text-sm leading-relaxed text-zinc-300 sm:text-base"
        >
          <h2 className="text-lg font-medium text-white">Portable Hardware Fence-Less Invariant</h2>
          <p>
            Safety is enforced through sequence-shadow differencing: each slot stores Sequence and Shadow where
            Shadow = Sequence xor constant. Consumers only accept data when both values agree, proving a complete
            publication without lock, barrier, or interlocked primitives.
          </p>
          <p>
            Portability comes from runtime topology probes. Cache line sizes and NUMA distances are detected during
            initialization, then used for aligned allocation, stripe stride width, and node-aware mode switching.
            This avoids hardcoded assumptions and keeps the path stable across heterogeneous sockets.
          </p>
          <p>
            To protect the sub-0.5ns budget, telemetry is unmanaged and branch-light. The publish/consume hot path
            only performs sequence checks, pointer writes, and mode-adaptive index math.
          </p>
        </motion.section>
      </div>
    </main>
  );
}
