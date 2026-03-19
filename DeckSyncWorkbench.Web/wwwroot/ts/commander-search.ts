const debounce = (fn: () => void, delay: number) => {
  let timer: number | undefined;
  return () => {
    if (timer !== undefined) {
      window.clearTimeout(timer);
    }
    timer = window.setTimeout(fn, delay);
  };
};

const renderSuggestions = (list: string[], datalist: HTMLDataListElement): void => {
  datalist.innerHTML = '';
  list.forEach(name => {
    const option = document.createElement('option');
    option.value = name;
    datalist.appendChild(option);
  });
};

const attachCommanderSearch = (): void => {
  const input = document.getElementById('commander-search-input') as HTMLInputElement | null;
  const datalist = document.getElementById('commander-suggestions') as HTMLDataListElement | null;
  if (!input || !datalist) {
    return;
  }

  const fetchSuggestions = async (): Promise<void> => {
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
      const names: string[] = await response.json();
      renderSuggestions(names, datalist);
    } catch (error) {
      console.error('Failed to fetch commander suggestions', error);
    }
  };

  const debounced = debounce(fetchSuggestions, 350);
  input.addEventListener('input', debounced);
};

document.addEventListener('DOMContentLoaded', attachCommanderSearch);
