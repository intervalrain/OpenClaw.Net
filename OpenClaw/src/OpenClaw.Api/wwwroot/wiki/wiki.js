let currentLang = 'en';
let currentDocId = null;
let manifest = null;
let useStaticMode = false;

async function init() {
    try {
        const response = await fetch('manifest.json');
        if (response.ok) {
            manifest = await response.json();
            useStaticMode = true;
            setModeIndicator('Static', 'mode-static');
        } else {
            throw new Error('No manifest');
        }
    } catch {
        manifest = {
            en: [
                { id: 'GUIDE', title: 'Learning Guide', path: 'en/GUIDE.md' },
                { id: '00-weda-core-overview', title: 'WEDA Core Overview', path: 'en/00-weda-core-overview.md' },
                { id: '01-domain-layer', title: 'Domain Layer', path: 'en/01-domain-layer.md' },
                { id: '02-application-layer', title: 'Application Layer', path: 'en/02-application-layer.md' },
                { id: '03-infrastructure-layer', title: 'Infrastructure Layer', path: 'en/03-infrastructure-layer.md' },
                { id: '04-api-layer', title: 'API Layer', path: 'en/04-api-layer.md' }
            ],
            zh: [
                { id: 'GUIDE', title: '學習指南', path: 'zh/GUIDE.md' },
                { id: '00-weda-core-overview', title: 'WEDA Core 概觀', path: 'zh/00-weda-core-overview.md' },
                { id: '01-domain-layer', title: 'Domain 層', path: 'zh/01-domain-layer.md' },
                { id: '02-application-layer', title: 'Application 層', path: 'zh/02-application-layer.md' },
                { id: '03-infrastructure-layer', title: 'Infrastructure 層', path: 'zh/03-infrastructure-layer.md' },
                { id: '04-api-layer', title: 'API 層', path: 'zh/04-api-layer.md' }
            ]
        };
        useStaticMode = false;
        setModeIndicator('Dynamic', 'mode-dynamic');
    }
    renderNav();
    loadFirstDoc();
}

function setModeIndicator(text, className) {
    const indicator = document.getElementById('mode-indicator');
    indicator.textContent = text;
    indicator.className = 'mode-indicator ' + className;
}

function renderNav() {
    const navList = document.getElementById('nav-list');
    const docs = manifest[currentLang] || [];
    navList.innerHTML = docs.map(doc => `
        <li class="wiki-nav-item">
            <a href="#" class="wiki-nav-link" data-id="${doc.id}" data-path="${doc.path}">
                ${doc.title}
            </a>
        </li>
    `).join('');
    navList.querySelectorAll('.wiki-nav-link').forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            loadDocument(link.dataset.id, link.dataset.path);
        });
    });
}

function loadFirstDoc() {
    const docs = manifest[currentLang] || [];
    if (docs.length > 0) {
        loadDocument(docs[0].id, docs[0].path);
    }
}

function loadDocument(docId, path) {
    currentDocId = docId;
    updateActiveNav();
    const frame = document.getElementById('content-frame');
    if (useStaticMode) {
        frame.src = path;
    } else {
        frame.srcdoc = `
            <!DOCTYPE html>
            <html>
            <head>
                <link rel="stylesheet" href="wiki.css">
                <script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"><\/script>
            </head>
            <body>
                <article class="markdown-body" id="content">
                    <div style="text-align:center;padding:40px;color:#666;">Loading...</div>
                </article>
                <script>
                    (async () => {
                        try {
                            const res = await fetch('${path}');
                            if (!res.ok) throw new Error('Not found');
                            let text = await res.text();
                            text = text.replace(/^---[\\s\\S]*?---\\n*/m, '');
                            document.getElementById('content').innerHTML = marked.parse(text);
                        } catch (e) {
                            document.getElementById('content').innerHTML =
                                '<div style="text-align:center;padding:40px;color:#666;">' +
                                '<h2>Document not found</h2>' +
                                '<p>Run WikiGenerator to generate static files.</p>' +
                                '</div>';
                        }
                    })();
                <\/script>
            </body>
            </html>
        `;
    }
}

function updateActiveNav() {
    document.querySelectorAll('.wiki-nav-link').forEach(link => {
        link.classList.toggle('active', link.dataset.id === currentDocId);
    });
}

document.querySelectorAll('.lang-btn').forEach(btn => {
    btn.addEventListener('click', () => {
        currentLang = btn.dataset.lang;
        document.querySelectorAll('.lang-btn').forEach(b => {
            b.classList.toggle('active', b === btn);
        });
        renderNav();
        const docs = manifest[currentLang] || [];
        const sameDoc = docs.find(d => d.id === currentDocId);
        if (sameDoc) {
            loadDocument(sameDoc.id, sameDoc.path);
        } else {
            loadFirstDoc();
        }
    });
});

init();
