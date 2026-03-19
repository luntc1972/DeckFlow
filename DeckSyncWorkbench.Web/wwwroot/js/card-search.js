"use strict";
const debounce = (fn, delay) => {
    let timer;
    return () => {
        if (timer !== undefined) {
            window.clearTimeout(timer);
        }
        timer = window.setTimeout(fn, delay);
    };
};
const renderCardSuggestions = (list, datalist) => {
    datalist.innerHTML = '';
    list.forEach(name => {
        const option = document.createElement('option');
        option.value = name;
        datalist.appendChild(option);
    });
};
const attachCardSearch = () => {
    const input = document.querySelector('input[name="CardName"]');
    const datalist = document.getElementById('card-suggestions');
    if (!input || !datalist) {
        return;
    }
    const fetchSuggestions = async () => {
        const query = input.value.trim();
        if (query.length < 2) {
            datalist.innerHTML = '';
            return;
        }
        try {
            const response = await fetch(`/suggest-categories/card-search?query=${encodeURIComponent(query)}`);
            if (!response.ok) {
                return;
            }
            const names = await response.json();
            renderCardSuggestions(names, datalist);
        }
        catch (error) {
            console.error('Failed to fetch card suggestions', error);
        }
    };
    const debounced = debounce(fetchSuggestions, 250);
    input.addEventListener('input', debounced);
};
document.addEventListener('DOMContentLoaded', attachCardSearch);
