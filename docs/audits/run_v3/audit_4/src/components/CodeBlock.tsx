interface Token {
  text: string;
  type: "keyword" | "comment" | "string" | "number" | "fn" | "type" | "plain" | "dim";
}

interface Line {
  tokens: Token[];
  indent: number;
}

function parseLines(raw: string): Line[] {
  return raw.split("\n").map((line) => {
    const trimmed = line.trimStart();
    const indent = line.length - trimmed.length;
    // Tokenise by regex
    const tokens: Token[] = [];
    let rest = trimmed;

    while (rest.length > 0) {
      // Comment
      const commentMatch = rest.match(/^(\/\/.*)/);
      if (commentMatch) {
        tokens.push({ text: commentMatch[1], type: "comment" });
        break;
      }
      // String
      const strMatch = rest.match(/^("[^"]*"|'[^']*')/);
      if (strMatch) {
        tokens.push({ text: strMatch[1], type: "string" });
        rest = rest.slice(strMatch[1].length);
        continue;
      }
      // Keywords
      const kwMatch = rest.match(/^(const|let|fn|function|return|new|if|else|while|for|export|import|from|async|await|struct|impl|pub|unsafe|class|static|void|type|interface|extends)\b/);
      if (kwMatch) {
        tokens.push({ text: kwMatch[1], type: "keyword" });
        rest = rest.slice(kwMatch[1].length);
        continue;
      }
      // Type names (PascalCase)
      const typeMatch = rest.match(/^([A-Z][A-Za-z0-9_]*)/);
      if (typeMatch) {
        tokens.push({ text: typeMatch[1], type: "type" });
        rest = rest.slice(typeMatch[1].length);
        continue;
      }
      // Function calls
      const fnMatch = rest.match(/^([a-z_][a-zA-Z0-9_]*)(?=\()/);
      if (fnMatch) {
        tokens.push({ text: fnMatch[1], type: "fn" });
        rest = rest.slice(fnMatch[1].length);
        continue;
      }
      // Numbers
      const numMatch = rest.match(/^(\d+[A-Za-z_]*)/);
      if (numMatch) {
        tokens.push({ text: numMatch[1], type: "number" });
        rest = rest.slice(numMatch[1].length);
        continue;
      }
      // Plain word
      const wordMatch = rest.match(/^([a-z_][a-zA-Z0-9_]*)/);
      if (wordMatch) {
        tokens.push({ text: wordMatch[1], type: "plain" });
        rest = rest.slice(wordMatch[1].length);
        continue;
      }
      // Symbols / single char
      tokens.push({ text: rest[0], type: "dim" });
      rest = rest.slice(1);
    }

    return { tokens, indent };
  });
}

const COLOR_MAP: Record<Token["type"], string> = {
  keyword: "#a78bfa",
  comment: "#475569",
  string:  "#86efac",
  number:  "#fb923c",
  fn:      "#38bdf8",
  type:    "#f0abfc",
  plain:   "#e2e8f0",
  dim:     "#64748b",
};

interface Props {
  label: string;
  lang?: string;
  code: string;
}

export default function CodeBlock({ label, lang = "rs", code }: Props) {
  const lines = parseLines(code);

  return (
    <div className="rounded-2xl border border-slate-700 bg-[#080f1a] overflow-hidden">
      {/* Header bar */}
      <div className="flex items-center justify-between px-4 py-2.5 border-b border-slate-800 bg-slate-900/50">
        <div className="flex items-center gap-2">
          <div className="flex gap-1.5">
            <div className="w-3 h-3 rounded-full bg-red-500/70" />
            <div className="w-3 h-3 rounded-full bg-yellow-500/70" />
            <div className="w-3 h-3 rounded-full bg-green-500/70" />
          </div>
          <span className="text-slate-400 text-xs font-mono ml-2">{label}</span>
        </div>
        <span className="text-slate-600 text-xs font-mono uppercase">{lang}</span>
      </div>

      {/* Code */}
      <pre className="overflow-x-auto px-4 py-4 text-xs leading-6 font-mono">
        {lines.map((line, i) => (
          <div key={i} className="flex">
            <span className="select-none text-slate-700 w-7 shrink-0 text-right mr-4">
              {i + 1}
            </span>
            <span>
              {" ".repeat(line.indent)}
              {line.tokens.map((tok, j) => (
                <span key={j} style={{ color: COLOR_MAP[tok.type] }}>
                  {tok.text}
                </span>
              ))}
            </span>
          </div>
        ))}
      </pre>
    </div>
  );
}
