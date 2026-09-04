// Máscara de valores estilo app bancário: formata no cliente, a cada tecla,
// preenchendo os centavos da direita para a esquerda (1 -> 0,01 -> 12 -> 0,12).
// casas > 2 (ex.: 8 para cripto) mantém a mesma mecânica com mais decimais.
window.moneyInterop = {
    attach: function (dotnetRef, wrapper, casas) {
        if (!wrapper) return;
        if (casas === undefined || casas === null) casas = 2;
        const capDigitos = casas > 2 ? 24 : 15;
        const el = wrapper.tagName === 'INPUT' ? wrapper : wrapper.querySelector('input');
        if (!el || el.dataset.moneyAttached === 'true') return;
        el.dataset.moneyAttached = 'true';
        el.setAttribute('inputmode', 'numeric');
        el.setAttribute('autocomplete', 'off');

        const formatar = (digitos) => {
            if (!digitos) return '';
            if (casas === 0) {
                const numero = Number(digitos);
                return numero.toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
            }
            const pad = digitos.padStart(casas + 1, '0');
            const inteiro = pad.slice(0, -casas);
            const dec = pad.slice(-casas);
            const numero = Number(inteiro + '.' + dec);
            return numero.toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas });
        };

        el.addEventListener('input', () => {
            const cru = el.value || '';
            let digitos = cru.replace(/[^,\d]/g, '').replace(/,/g, '');
            // Remove zeros à esquerda mantendo ao menos um dígito
            digitos = digitos.replace(/^0+(?=\d)/, '').slice(0, capDigitos);
            const formatado = formatar(digitos);
            if (el.value !== formatado) {
                el.value = formatado;
            }
            dotnetRef.invokeMethodAsync('OnDigitsJs', digitos).catch(() => {});
        });
    }
};
