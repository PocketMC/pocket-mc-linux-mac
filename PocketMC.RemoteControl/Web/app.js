document.getElementById('login-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const password = document.getElementById('password').value;
    
    try {
        const response = await fetch('/api/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `password=${encodeURIComponent(password)}`
        });

        if (response.ok) {
            document.getElementById('login-view').style.display = 'none';
            document.getElementById('dashboard-view').style.display = 'flex';
            loadInstances();
        } else {
            document.getElementById('login-error').style.display = 'block';
        }
    } catch (err) {
        console.error('Login failed', err);
    }
});

let currentWebSocket = null;
let currentInstanceId = null;

async function loadInstances() {
    try {
        const response = await fetch('/api/instances');
        if (response.status === 401) {
            document.getElementById('login-view').style.display = 'block';
            document.getElementById('dashboard-view').style.display = 'none';
            return;
        }
        const instances = await response.json();
        const list = document.getElementById('instance-list');
        list.innerHTML = '';
        instances.forEach(inst => {
            const li = document.createElement('li');
            li.textContent = inst.name;
            li.onclick = () => selectInstance(inst.slug, inst.name);
            list.appendChild(li);
        });
    } catch (err) {
        console.error('Failed to load instances', err);
    }
}

function selectInstance(slug, name) {
    currentInstanceId = slug;
    document.getElementById('current-instance-name').textContent = name;
    document.getElementById('instance-actions').style.display = 'flex';
    document.getElementById('console-form').style.display = 'flex';
    
    // reset console
    document.getElementById('console-output').textContent = '';
    
    // close old ws
    if (currentWebSocket) {
        currentWebSocket.close();
    }
    
    // connect ws
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    currentWebSocket = new WebSocket(`${protocol}//${window.location.host}/ws/instances/${slug}/console`);
    currentWebSocket.onmessage = (event) => {
        const pre = document.getElementById('console-output');
        pre.textContent += event.data;
        pre.scrollTop = pre.scrollHeight;
    };
}

['start', 'stop', 'restart'].forEach(action => {
    document.getElementById(`btn-${action}`).addEventListener('click', async () => {
        if (!currentInstanceId) return;
        await fetch(`/api/instances/${currentInstanceId}/${action}`, { method: 'POST' });
    });
});

document.getElementById('console-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    if (!currentInstanceId) return;
    const input = document.getElementById('console-input');
    const command = input.value;
    input.value = '';
    
    await fetch(`/api/instances/${currentInstanceId}/console/command`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: `command=${encodeURIComponent(command)}`
    });
});

// Check status on load to see if we're already authenticated
fetch('/api/status').then(r => {
    if (r.ok) {
        document.getElementById('login-view').style.display = 'none';
        document.getElementById('dashboard-view').style.display = 'flex';
        loadInstances();
    }
}).catch(() => {});
