let currentLang = 'en';
let currentDocId = null;
let manifest = null;

async function init() {
    try {
        const response = await fetch('manifest.json');
        if (!response.ok) throw new Error('No manifest');
        manifest = await response.json();
    } catch {
        document.getElementById('nav-list').innerHTML =
            '<li class="wiki-nav-item"><span class="wiki-loading">Wiki not generated. Run WikiGenerator to build static pages.</span></li>';
        return;
    }
    renderNav();
    loadFirstDoc();
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
    document.getElementById('content-frame').src = path;
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