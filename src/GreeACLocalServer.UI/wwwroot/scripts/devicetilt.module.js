export function attach(containerId) {
    const container = document.getElementById(containerId);
    if (!container || container.dataset.tiltAttached) return;
    container.dataset.tiltAttached = 'true';

    container.addEventListener('mousemove', (e) => {
        const card = e.target.closest('.gac-device-card');
        if (!card) return;
        const rect = card.getBoundingClientRect();
        const px = (e.clientX - rect.left) / rect.width;
        const py = (e.clientY - rect.top) / rect.height;
        const rotateY = (px - 0.5) * 8;
        const rotateX = (0.5 - py) * 6;
        card.style.transform = `perspective(900px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) translateY(-2px)`;
    });

    container.addEventListener('mouseout', (e) => {
        const card = e.target.closest ? e.target.closest('.gac-device-card') : null;
        if (card && (!e.relatedTarget || !card.contains(e.relatedTarget))) {
            card.style.transform = '';
        }
    });
}