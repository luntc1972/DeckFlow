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
const renderSuggestions = (list, datalist) => {
    datalist.innerHTML = '';
    list.forEach(name => {
        const option = document.createElement('option');
        option.value = name;
        datalist.appendChild(option);
    });
};
const attachCommanderSearch = () => {
    const input = document.getElementById('commander-search-input');
    const datalist = document.getElementById('commander-suggestions');
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
            const response = await fetch(`/commander-categories/search?query=${encodeURIComponent(query)}`);
            if (!response.ok) {
                return;
            }
            const names = await response.json();
            renderSuggestions(names, datalist);
        }
        catch (error) {
            console.error('Failed to fetch commander suggestions', error);
        }
    };
    const debounced = debounce(fetchSuggestions, 350);
    input.addEventListener('input', debounced);
};
document.addEventListener('DOMContentLoaded', attachCommanderSearch);
