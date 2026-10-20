// Blazor decides preventDefault at render time, not per key, so the picker cannot stop only some keys itself.
// While a picker's list is open, Enter must pick an option instead of submitting the form,
// and the arrow keys move through the list instead of the text cursor.
document.addEventListener('keydown', function (e) {
    var input = e.target;
    if (!input.matches || !input.matches('.lookup-picker input[role="combobox"]')) return;
    if (input.getAttribute('aria-expanded') !== 'true') return;
    if (e.key === 'Enter' || e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Escape') {
        e.preventDefault();
    }
}, true);
