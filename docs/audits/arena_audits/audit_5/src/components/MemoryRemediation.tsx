import { useState } from 'react';

export default function MemoryRemediation() {
  const [activeSection, setActiveSection] = useState<'mlockall' | 'slab' | 'redis'>('mlockall');

  const sections = {
    mlockall: {
      title: 'mlockall(MCL_CURRENT | MCL_FUTURE)',
      description: 'Locks entire process in RAM to prevent page faults',
      code: `// Apply at process startup
#include <sys/mman.h>

void lock_process_memory() {
    // Lock all current and future memory
    if (mlockall(MCL_CURRENT | MCL_FUTURE) != 0) {
        perror("mlockall failed");
        exit(1);
    }
    
    // Disable malloc trimming
    mallopt(M_TRIM_THRESHOLD, -1);
    
    // Disable mmap threshold
    mallopt(M_MMAP_MAX, 0);
}`,
      effects: [
        { label: 'Page Fault Jitter', before: '1-5ms (worst case)', after: '0 (eliminated)' },
        { label: 'Swap Risk', before: 'Unbounded', after: '0 (impossible)' },
        { label: 'Memory Lock', before: 'None', after: 'RAM-resident only' },
      ],
      requirements: [
        'CAP_IPC_LOCK capability or root privileges',
        'RLIMIT_MEMLOCK must accommodate total memory footprint',
        'Pre-fault all pages at startup to avoid minor faults',
      ],
    },
    slab: {
      title: 'Custom Slab Pool',
      description: 'Pre-allocated memory blocks bypassing heap allocation',
      code: `// Custom slab allocator for hot path
struct SlabPool {
    void* base;
    size_t block_size;
    size_t capacity;
    atomic_size_t cursor;
    uint8_t* bitmap;
};

SlabPool* slab_create(size_t block_size, size_t capacity) {
    SlabPool* pool = aligned_alloc(64, sizeof(SlabPool));
    
    // Pre-allocate with page alignment
    pool->base = mmap(NULL, 
        block_size * capacity,
        PROT_READ | PROT_WRITE,
        MAP_PRIVATE | MAP_ANONYMOUS | MAP_LOCKED,
        -1, 0);
    
    // Pre-fault all pages
    memset(pool->base, 0, block_size * capacity);
    
    pool->block_size = block_size;
    pool->capacity = capacity;
    pool->cursor = 0;
    
    return pool;
}

void* slab_alloc(SlabPool* pool) {
    // Lock-free bump allocation
    size_t idx = atomic_fetch_add(&pool->cursor, 1);
    if (idx >= pool->capacity) return NULL;
    return (char*)pool->base + (idx * pool->block_size);
}`,
      effects: [
        { label: 'Allocation Time', before: '50-500ns (kmalloc)', after: '<10ns (bump)' },
        { label: 'SLUB Contention', before: '5-15µs (lock wait)', after: '0 (lock-free)' },
        { label: 'Fragmentation', before: 'Variable', after: 'Zero (pre-sized)' },
      ],
      requirements: [
        'Accurate sizing of peak memory requirements',
        'Worst-case capacity must be pre-allocated',
        'No dynamic growth during hot path operation',
      ],
    },
    redis: {
      title: 'Atomic Redis Lua Scripts',
      description: 'Multi-account coordination as single atomic unit',
      code: `-- Pre-load at startup
local script = [[
    -- Atomic multi-account transfer
    local from_account = KEYS[1]
    local to_account = KEYS[2]
    local amount = tonumber(ARGV[1])
    
    -- Check balance atomically
    local balance = tonumber(redis.call('GET', from_account))
    if balance < amount then
        return {err = "INSUFFICIENT_FUNDS"}
    end
    
    -- Execute both updates atomically
    redis.call('DECRBY', from_account, amount)
    redis.call('INCRBY', to_account, amount)
    
    return {ok = "TRANSFER_COMPLETE"}
]]

-- Load script once, store SHA
local sha = redis.script_load(script)

-- Execute by SHA (no re-transmission)
redis.evalsha(sha, 2, "account:1", "account:2", "100")`,
      effects: [
        { label: 'Round Trips', before: '3-5 calls', after: '1 call' },
        { label: 'Lock Duration', before: '50-200µs', after: '10-50µs' },
        { label: 'Consistency', before: 'Race conditions possible', after: 'Atomic guarantee' },
      ],
      requirements: [
        'Redis must run in single-threaded mode (default)',
        'Scripts must be pre-loaded (SCRIPT LOAD) at startup',
        'Avoid O(N) operations within Lua scripts',
      ],
    },
  };

  const current = sections[activeSection];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="text-center">
        <h2 className="text-2xl font-bold text-cyan-400 mb-2">Zero-Heap Implementation</h2>
        <p className="text-slate-400 text-sm max-w-2xl mx-auto">
          Three-pronged memory remediation strategy: lock all memory, pre-allocate all blocks, 
          atomize all persistence operations.
        </p>
      </div>

      {/* Tab Navigation */}
      <div className="flex gap-2 bg-slate-800/50 p-1 rounded-lg">
        {(Object.keys(sections) as Array<keyof typeof sections>).map((key) => (
          <button
            key={key}
            onClick={() => setActiveSection(key)}
            className={`flex-1 py-3 px-4 rounded-md text-sm font-medium transition-all ${
              activeSection === key
                ? 'bg-cyan-500 text-white'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-700/50'
            }`}
          >
            {key === 'mlockall' && '🔒 mlockall'}
            {key === 'slab' && '🧱 Slab Pool'}
            {key === 'redis' && '⚡ Redis Lua'}
          </button>
        ))}
      </div>

      {/* Content */}
      <div className="grid grid-cols-2 gap-6">
        {/* Code Section */}
        <div className="bg-slate-900 border border-slate-700 rounded-xl overflow-hidden">
          <div className="bg-slate-800 px-4 py-2 border-b border-slate-700 flex items-center gap-2">
            <div className="flex gap-1.5">
              <div className="w-3 h-3 rounded-full bg-red-500" />
              <div className="w-3 h-3 rounded-full bg-yellow-500" />
              <div className="w-3 h-3 rounded-full bg-green-500" />
            </div>
            <span className="text-xs text-slate-400 font-mono ml-2">
              {activeSection === 'mlockall' && 'memory_lock.c'}
              {activeSection === 'slab' && 'slab_allocator.c'}
              {activeSection === 'redis' && 'atomic_transfer.lua'}
            </span>
          </div>
          <pre className="p-4 text-sm font-mono text-slate-300 overflow-x-auto">
            <code>{current.code}</code>
          </pre>
        </div>

        {/* Effects Section */}
        <div className="space-y-4">
          <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
            <h4 className="font-bold text-slate-200 mb-3">{current.title}</h4>
            <p className="text-sm text-slate-400">{current.description}</p>
          </div>

          <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
            <h4 className="font-bold text-emerald-400 mb-3">Performance Effects</h4>
            <div className="space-y-2">
              {current.effects.map((effect, idx) => (
                <div key={idx} className="flex items-center justify-between text-sm">
                  <span className="text-slate-400">{effect.label}</span>
                  <div className="flex items-center gap-3">
                    <span className="text-red-400 line-through">{effect.before}</span>
                    <span className="text-slate-500">→</span>
                    <span className="text-emerald-400">{effect.after}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="bg-amber-900/20 border border-amber-500/30 rounded-xl p-4">
            <h4 className="font-bold text-amber-400 mb-3">⚠️ Requirements</h4>
            <ul className="space-y-1">
              {current.requirements.map((req, idx) => (
                <li key={idx} className="text-sm text-slate-300 flex items-start gap-2">
                  <span className="text-amber-400">•</span>
                  {req}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>

      {/* Memory Layout Visualization */}
      <div className="bg-slate-900/50 border border-slate-700 rounded-xl p-6">
        <h3 className="font-bold text-lg mb-4 text-center">Memory Layout — Hot Path</h3>
        <div className="grid grid-cols-4 gap-4">
          <div className="text-center">
            <div className="h-32 bg-gradient-to-b from-emerald-500/30 to-emerald-500/10 rounded-lg border border-emerald-500/50 flex flex-col justify-center">
              <div className="text-emerald-400 font-bold text-sm">LOCKED</div>
              <div className="text-xs text-slate-400">Code Segment</div>
            </div>
          </div>
          <div className="text-center">
            <div className="h-32 bg-gradient-to-b from-cyan-500/30 to-cyan-500/10 rounded-lg border border-cyan-500/50 flex flex-col justify-center">
              <div className="text-cyan-400 font-bold text-sm">LOCKED</div>
              <div className="text-xs text-slate-400">Stack</div>
            </div>
          </div>
          <div className="text-center">
            <div className="h-32 bg-gradient-to-b from-blue-500/30 to-blue-500/10 rounded-lg border border-blue-500/50 flex flex-col justify-center">
              <div className="text-blue-400 font-bold text-sm">PRE-ALLOC</div>
              <div className="text-xs text-slate-400">Slab Pool</div>
            </div>
          </div>
          <div className="text-center">
            <div className="h-32 bg-gradient-to-b from-slate-500/30 to-slate-500/10 rounded-lg border border-slate-500/50 flex flex-col justify-center border-dashed">
              <div className="text-slate-500 font-bold text-sm">NO ACCESS</div>
              <div className="text-xs text-slate-500">Heap (bypassed)</div>
            </div>
          </div>
        </div>
        <div className="text-center mt-4 text-sm text-slate-400">
          All hot-path memory is RAM-resident. <span className="text-red-400">Heap is never touched</span> during execution.
        </div>
      </div>
    </div>
  );
}
