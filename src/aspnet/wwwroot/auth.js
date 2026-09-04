window.authHelpers = {
    login: (senha, turnstileToken) => fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ senha, turnstileToken })
    }).then(async (res) => {
        const data = await res.json().catch(() => ({}));
        return {
            status: res.status,
            nome: res.status === 200 ? (data.nome ?? null) : null,
            requireTurnstile: !!data.requireTurnstile,
            siteKey: data.siteKey ?? null
        };
    }),
    logout: () => fetch('/api/auth/logout', { method: 'POST' })
};

window.turnstileHelpers = {
    _script: null,
    load: () => {
        if (window.turnstile) return Promise.resolve();
        if (!window.turnstileHelpers._script) {
            window.turnstileHelpers._script = new Promise((resolve, reject) => {
                const s = document.createElement('script');
                s.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
                s.async = true;
                s.defer = true;
                s.onload = resolve;
                s.onerror = () => reject(new Error('falha ao carregar turnstile'));
                document.head.appendChild(s);
            });
        }
        return window.turnstileHelpers._script;
    },
    render: async (elId, siteKey) => {
        await window.turnstileHelpers.load();
        window.turnstile.render('#' + elId, { sitekey: siteKey });
    },
    getToken: (elId) => {
        const el = document.getElementById(elId);
        return el && window.turnstile ? window.turnstile.getResponse(el) : null;
    },
    reset: (elId) => {
        const el = document.getElementById(elId);
        if (el && window.turnstile) window.turnstile.reset(el);
    }
};

window.backupHelpers = {
    download: (nome, bytesBase64) => {
        const bin = atob(bytesBase64);
        const bytes = new Uint8Array(bin.length);
        for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
        const blob = new Blob([bytes], { type: 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = nome;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    }
};
