(() => {
  const list = document.querySelector('#menu-reorder-list');
  const token = document.querySelector('#menu-order-token input[name="__RequestVerificationToken"]')?.value;
  const status = document.querySelector('#menu-order-status');
  if (!list || !token) return;

  let draggedItem = null;
  const items = () => [...list.querySelectorAll('.menu-reorder-item')];
  const setStatus = message => { status.textContent = message; };

  list.addEventListener('dragstart', event => {
    const item = event.target.closest('.menu-reorder-item');
    if (!item || event.target.closest('input, button, form')) { event.preventDefault(); return; }
    draggedItem = item;
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', item.dataset.menuId);
    requestAnimationFrame(() => item.classList.add('is-dragging'));
  });

  list.addEventListener('dragover', event => {
    event.preventDefault();
    if (!draggedItem) return;
    const candidates = items().filter(item => item !== draggedItem);
    const next = candidates.find(item => event.clientY < item.getBoundingClientRect().top + item.getBoundingClientRect().height / 2);
    list.insertBefore(draggedItem, next || null);
  });

  list.addEventListener('dragend', async () => {
    if (!draggedItem) return;
    draggedItem.classList.remove('is-dragging');
    draggedItem = null;
    const ordered = items().map((item, index) => ({ id: Number(item.dataset.menuId), sortOrder: (index + 1) * 10 }));
    items().forEach((item, index) => item.querySelector('input[name="sortOrder"]').value = ordered[index].sortOrder);
    setStatus('Saving menu order…');
    try {
      const response = await fetch(list.dataset.reorderUrl, { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(ordered) });
      if (!response.ok) throw new Error();
      setStatus('Menu order saved.');
    } catch {
      setStatus('Could not save the new order. Refresh and try again.');
    }
  });
})();
