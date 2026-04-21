const fs=require('fs');
const c=fs.readFileSync('docs/arena_dashboard.html','utf8');
const m=c.match(/<div class="mermaid\s*">([\s\S]*?)<\/div>/g) || c.match(/<div[^>]*class=[^>]*mermaid[^>]*>([\s\S]*?)<\/div>/g);
if(m){
    m.forEach((b,i)=>console.log(`\n\n--- BLOCK ${i} ---\n`+b));
}else{
    console.log('No blocks');
}
