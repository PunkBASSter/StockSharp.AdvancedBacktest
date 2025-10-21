import { readdir, readFile, writeFile } from 'fs/promises';
import { join } from 'path';

/**
 * Post-build script to:
 * 1. Convert absolute paths to relative paths
 * 2. Embed chartData.json into HTML for file:// protocol support
 * This enables the static export to work with file:// protocol without CORS issues
 */

async function fixHtmlPaths(dir) {
    const entries = await readdir(dir, { withFileTypes: true });

    for (const entry of entries) {
        const fullPath = join(dir, entry.name);

        if (entry.isDirectory()) {
            // Recursively process subdirectories
            await fixHtmlPaths(fullPath);
        } else if (entry.name.endsWith('.html') || entry.name.endsWith('.js')) {
            // Read HTML file
            let content = await readFile(fullPath, 'utf-8');

            // Replace absolute paths with relative paths in HTML attributes
            content = content.replace(/href="\/_next\//g, 'href="./_next/');
            content = content.replace(/src="\/_next\//g, 'src="./_next/');
            content = content.replace(/href='\/_next\//g, "href='./_next/");
            content = content.replace(/src='\/_next\//g, "src='./_next/");

            // Replace absolute paths in embedded JavaScript (RSC payload and inline scripts)
            // These are JSON-escaped strings in Next.js hydration data: \"\/_next\/
            content = content.replace(/\\"\/_next\//g, '\\"./_next/');
            content = content.replace(/\\'\/_next\//g, "\\'./_next/");

            // Fix webpack public path in JS files: r.p="/_next/" -> r.p="./_next/"
            if (entry.name.endsWith('.js')) {
                content = content.replace(/r\.p="\/_next\/"/g, 'r.p="./_next/"');
                content = content.replace(/r\.p='\/_next\/'/g, "r.p='./_next/'");
            }

            // Try to embed chartData.json if it exists in the same directory (HTML only)
            if (entry.name.endsWith('.html')) {
                const chartDataPath = join(dir, 'chartData.json');
                try {
                    const chartDataContent = await readFile(chartDataPath, 'utf-8');
                    const chartData = JSON.parse(chartDataContent);

                    // Load and embed indicator files if they exist
                    if (chartData.indicatorFiles && chartData.indicatorFiles.length > 0) {
                        chartData.indicators = [];
                        for (const indicatorFile of chartData.indicatorFiles) {
                            try {
                                const indicatorPath = join(dir, indicatorFile);
                                const indicatorContent = await readFile(indicatorPath, 'utf-8');
                                const indicatorData = JSON.parse(indicatorContent);
                                chartData.indicators.push(indicatorData);
                                console.log(`  ✓ Loaded indicator: ${indicatorFile}`);
                            } catch (err) {
                                console.warn(`  ⚠ Failed to load indicator file: ${indicatorFile}`, err.message);
                            }
                        }
                        // Remove indicatorFiles array since we've embedded the data
                        delete chartData.indicatorFiles;
                    }

                    // Inject chart data as inline script right after <head> tag
                    // This ensures it's available before React hydration
                    const scriptTag = `<script>window.__CHART_DATA__ = ${JSON.stringify(chartData)};</script>`;
                    content = content.replace('<head>', `<head>${scriptTag}`);
                    console.log(`✓ Embedded chartData.json into: ${entry.name}`);
                } catch {
                    // chartData.json doesn't exist in this directory, skip embedding
                }
            }

            // Write back
            await writeFile(fullPath, content, 'utf-8');
            console.log(`✓ Fixed paths in: ${entry.name}`);
        }
    }
}

// Run the script
// Use current working directory (when called from C#) or ./out (when called manually)
const outDir = process.argv[2] || process.cwd();

console.log(`Fixing paths for file:// protocol support in: ${outDir}`);
await fixHtmlPaths(outDir);
console.log('✓ All paths fixed successfully!');
