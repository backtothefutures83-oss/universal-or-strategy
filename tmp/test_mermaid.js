const puppeteer = require('puppeteer');
const fs = require('fs');

(async () => {
    const html = fs.readFileSync('docs/arena_dashboard.html', 'utf8');
    const browser = await puppeteer.launch({ headless: true });
    const page = await browser.newPage();
    
    page.on('console', msg => console.log('PAGE LOG:', msg.text()));
    
    await page.setContent(html);
    await new Promise(r => setTimeout(r, 2000));
    
    // Check if there are syntax error text on the page
    const errors = await page.evaluate(() => {
        let errs = [];
        document.querySelectorAll('.mermaid').forEach((el, index) => {
            if (el.innerHTML.includes('Syntax error in text')) {
                errs.push('Block ' + index + ' failed.');
            }
        });
        return errs;
    });
    console.log('Errors:', errors);
    
    await browser.close();
})();
