const fs=require('fs');
const c=fs.readFileSync('docs/arena_dashboard.html','utf8');
const m=c.match(/<div class="mermaid\s*">([\s\S]*?)<\/div>/g) || c.match(/<div[^>]*class=[^>]*mermaid[^>]*>([\s\S]*?)<\/div>/g);
let out = '';
if(m){
    m.forEach((b,i)=> { out += `\n\n--- BLOCK ${i} ---\n`+b; });
}else{
    out += 'No blocks\n';
}
fs.writeFileSync('tmp/mermaids.txt', out, 'utf8');
