export function attach(containerId) {
  const container = document.getElementById(containerId);
  if (!container || container.dataset.tiltAttached) return;
  container.dataset.tiltAttached = 'true';
  const targets = new Map(), current = new Map(), rafs = new Map();

  function tick(card) {
    const t = targets.get(card), c = current.get(card);
    c.x += (t.x - c.x) * 0.22;
    c.y += (t.y - c.y) * 0.22;
    card.style.transform = `perspective(900px) rotateX(${c.x}deg) rotateY(${c.y}deg) translateY(${(Math.abs(t.x)+Math.abs(t.y))>0.1?-2:0}px)`;
    if (Math.abs(t.x-c.x) > 0.02 || Math.abs(t.y-c.y) > 0.02) {
      rafs.set(card, requestAnimationFrame(() => tick(card)));
    } else { rafs.delete(card); }
  }

  container.addEventListener('mousemove', (e) => {
    const card = e.target.closest('.gac-device-card');
    if (!card) return;
    if (!current.has(card)) current.set(card, {x:0,y:0});
    const rect = card.getBoundingClientRect();
    const px = (e.clientX - rect.left) / rect.width;
    const py = (e.clientY - rect.top) / rect.height;
    targets.set(card, { x:(0.5-py)*6, y:(px-0.5)*8 });
    if (!rafs.has(card)) rafs.set(card, requestAnimationFrame(() => tick(card)));
  });

  container.addEventListener('mouseout', (e) => {
    const card = e.target.closest ? e.target.closest('.gac-device-card') : null;
    if (card && (!e.relatedTarget || !card.contains(e.relatedTarget))) {
      targets.set(card, {x:0,y:0});
      if (!rafs.has(card)) rafs.set(card, requestAnimationFrame(() => tick(card)));
    }
  });
}
