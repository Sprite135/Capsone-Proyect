const puppeteer = require('puppeteer-extra');
const StealthPlugin = require('puppeteer-extra-plugin-stealth');

// Activar stealth mode
puppeteer.use(StealthPlugin());

async function scrapeSeace() {
    console.log('[PuppeteerStealth] Iniciando scraping de SEACE...');
    
    const opportunities = [];
    
    try {
        // Lanzar navegador con stealth
        const browser = await puppeteer.launch({
            headless: false, // Mantener visible para ver la interacción
            args: [
                '--no-sandbox',
                '--disable-setuid-sandbox',
                '--disable-dev-shm-usage',
                '--disable-accelerated-2d-canvas',
                '--no-first-run',
                '--no-zygote',
                '--single-process',
                '--disable-gpu'
            ]
        });
        
        const page = await browser.newPage();
        
        // Configurar viewport y user agent realista
        await page.setViewport({ width: 1920, height: 1080 });
        await page.setUserAgent('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36');
        
        console.log('[PuppeteerStealth] Navegando a SEACE...');
        await page.goto('https://prod2.seace.gob.pe/seacebus-uiwd-pub/buscadorPublico/buscadorPublico.xhtml#', {
            waitUntil: 'load',
            timeout: 60000
        });
        
        // Esperar a que cargue el formulario
        console.log('[PuppeteerStealth] Esperando 10 segundos para carga del formulario...');
        await new Promise(resolve => setTimeout(resolve, 10000));
        
        // Llenar campo Año de Convocatoria
        console.log('[PuppeteerStealth] Buscando campo Año de Convocatoria...');
        
        // Intentar encontrar el campo por diferentes selectores
        const yearSelectors = [
            'input[name*="convocatoria"]',
            'input[name*="anio"]',
            'input[name*="year"]',
            'input[id*="convocatoria"]',
            'input[id*="anio"]',
            'input[id*="year"]',
            'select[name*="convocatoria"]',
            'select[name*="anio"]',
            'select[name*="year"]'
        ];
        
        let yearField = null;
        for (const selector of yearSelectors) {
            try {
                yearField = await page.$(selector);
                if (yearField) {
                    console.log(`[PuppeteerStealth] Campo encontrado con selector: ${selector}`);
                    break;
                }
            } catch (e) {
                // Continuar con siguiente selector
            }
        }
        
        if (yearField) {
            // Verificar si es select o input
            const tagName = await yearField.evaluate(el => el.tagName);
            console.log(`[PuppeteerStealth] Tipo de campo: ${tagName}`);
            
            if (tagName === 'SELECT') {
                await yearField.select('2026');
                console.log('[PuppeteerStealth] Año seleccionado: 2026');
            } else {
                await yearField.click({ clickCount: 3 });
                await yearField.type('2026');
                console.log('[PuppeteerStealth] Año ingresado: 2026');
            }
        } else {
            console.log('[PuppeteerStealth] No se encontró campo de año, intentando por XPath...');
            // Intentar con XPath
            const yearInput = await page.$x('//input[contains(@placeholder, "Año") or contains(@label, "Año") or contains(@title, "Año")]');
            if (yearInput.length > 0) {
                await yearInput[0].click({ clickCount: 3 });
                await yearInput[0].type('2026');
                console.log('[PuppeteerStealth] Año ingresado por XPath: 2026');
            }
        }
        
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        // Hacer clic en Buscar
        console.log('[PuppeteerStealth] Buscando botón Buscar...');
        const searchSelectors = [
            'button[type="submit"]',
            'button:contains("Buscar")',
            'input[type="submit"]',
            'button[id*="buscar"]',
            'button[id*="search"]',
            'a:contains("Buscar")'
        ];
        
        let searchButton = null;
        for (const selector of searchSelectors) {
            try {
                searchButton = await page.$(selector);
                if (searchButton) {
                    console.log(`[PuppeteerStealth] Botón encontrado con selector: ${selector}`);
                    break;
                }
            } catch (e) {
                // Continuar
            }
        }
        
        if (searchButton) {
            await searchButton.click();
            console.log('[PuppeteerStealth] Botón Buscar clickeado');
        } else {
            console.log('[PuppeteerStealth] No se encontró botón Buscar, intentando por texto...');
            await page.evaluate(() => {
                const buttons = Array.from(document.querySelectorAll('button, input[type="submit"]'));
                const searchBtn = buttons.find(btn => btn.textContent.includes('Buscar') || btn.value.includes('Buscar'));
                if (searchBtn) searchBtn.click();
            });
        }
        
        // Esperar resultados
        console.log('[PuppeteerStealth] Esperando 15 segundos para resultados...');
        await new Promise(resolve => setTimeout(resolve, 15000));
        
        // Buscar tablas de resultados
        const tables = await page.$$('table');
        console.log(`[PuppeteerStealth] Encontradas ${tables.length} tablas`);
        
        for (let i = 0; i < tables.length; i++) {
            try {
                const rows = await tables[i].$$('tbody tr');
                console.log(`[PuppeteerStealth] Tabla ${i}: ${rows.length} filas`);
                
                if (rows.length > 0) {
                    for (const row of rows.slice(0, 20)) {
                        try {
                            const cells = await row.$$('td');
                            if (cells.length >= 6) {
                                const cellData = [];
                                for (let j = 0; j < Math.min(cells.length, 12); j++) {
                                    const text = await cells[j].evaluate(el => el.textContent);
                                    cellData.push(text?.trim() || '');
                                }
                                
                                opportunities.push({
                                    nro: cellData[0],
                                    entidad: cellData[1],
                                    fechaPublicacion: cellData[2],
                                    nomenclatura: cellData[3],
                                    reiniciadoDesde: cellData[4],
                                    objetoContratacion: cellData[5],
                                    descripcionObjeto: cellData[6],
                                    codigoSnip: cellData[7],
                                    codigoUnicoInversion: cellData[8],
                                    cuantia: cellData[9],
                                    moneda: cellData[10],
                                    versionSeace: cellData[11]
                                });
                                
                                console.log(`[PuppeteerStealth] ${cellData[1]} - ${cellData[2]}`);
                            }
                        } catch (e) {
                            console.log(`[PuppeteerStealth] Error en fila: ${e.message}`);
                        }
                    }
                }
            } catch (e) {
                console.log(`[PuppeteerStealth] Error en tabla ${i}: ${e.message}`);
            }
        }
        
        // Guardar screenshot para debug
        await page.screenshot({ path: 'seace-screenshot.png', fullPage: true });
        console.log('[PuppeteerStealth] Screenshot guardado: seace-screenshot.png');
        
        // Guardar HTML para debug
        const html = await page.content();
        fs.writeFileSync('seace-page.html', html);
        console.log('[PuppeteerStealth] HTML guardado: seace-page.html');
        
        // Mantener navegador abierto para inspección
        console.log('[PuppeteerStealth] Navegador abierto para inspección. Presiona Ctrl+C para cerrar.');
        await new Promise(resolve => setTimeout(resolve, 30000));
        
        await browser.close();
        
    } catch (error) {
        console.error('[PuppeteerStealth] Error:', error.message);
        console.error(error.stack);
    }
    
    // Guardar resultados
    fs.writeFileSync('seace-results.json', JSON.stringify(opportunities, null, 2));
    console.log(`[PuppeteerStealth] Resultados guardados: ${opportunities.length} oportunidades`);
    
    return opportunities;
}

const fs = require('fs');
scrapeSeace();
