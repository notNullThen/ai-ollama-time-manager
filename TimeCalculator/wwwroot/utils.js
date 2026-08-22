/**
 * Copies the inner text of an element to the clipboard and provides visual feedback on a button.
 * @param {string} elementId - The ID of the element containing the text to copy.
 * @param {HTMLElement} btn - The button element that was clicked.
 */
window.copyToClipboard = function (elementId, btn) {
    const element = document.getElementById(elementId);
    if (!element) return;

    const text = element.textContent;
    const originalText = btn.innerText;

    const showFeedback = () => {
        btn.innerText = 'Copied!';
        btn.classList.add('btn-success');
        btn.classList.remove('btn-outline-primary');

        setTimeout(() => {
            btn.innerText = originalText;
            btn.classList.remove('btn-success');
            btn.classList.add('btn-outline-primary');
        }, 2000);
    };

    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text)
            .then(showFeedback)
            .catch(err => {
                console.error('Failed to copy text: ', err);
            });
    } else {
        // Fallback for older browsers or non-secure contexts
        const textarea = document.createElement('textarea');
        textarea.value = text;
        // Ensure textarea is not visible but part of the DOM
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        textarea.style.top = '0';
        document.body.appendChild(textarea);
        textarea.select();
        try {
            document.execCommand('copy');
            showFeedback();
        } catch (err) {
            console.error('Fallback copy failed: ', err);
        }
        document.body.removeChild(textarea);
    }
};

/**
 * Sets up auto-scrolling for a container, disabling it on manual scroll and enabling via button.
 */
window.setupAutoScroll = function(containerId, buttonId) {
    const container = document.getElementById(containerId);
    const button = document.getElementById(buttonId);
    if (!container || !button) return;

    // Prevent attaching multiple observers
    if (container.dataset.autoScrollInitialized) return;
    container.dataset.autoScrollInitialized = "true";

    let isAutoScrollEnabled = true;

    const updateButtonState = () => {
        if (isAutoScrollEnabled) {
            button.classList.add('btn-primary');
            button.classList.remove('btn-outline-primary');
            button.innerHTML = 'Auto-scroll: ON';
        } else {
            button.classList.remove('btn-primary');
            button.classList.add('btn-outline-primary');
            button.innerHTML = 'Auto-scroll: OFF';
        }
    };

    button.addEventListener('click', () => {
        isAutoScrollEnabled = true;
        container.scrollTop = container.scrollHeight;
        updateButtonState();
    });

    let lastScrollTop = container.scrollTop;

    container.addEventListener('scroll', () => {
        // Only disable if user scrolled UP
        if (container.scrollTop < lastScrollTop) {
            const isAtBottom = container.scrollHeight - container.scrollTop <= container.clientHeight + 10;
            if (!isAtBottom && isAutoScrollEnabled) {
                isAutoScrollEnabled = false;
                updateButtonState();
            }
        }
        lastScrollTop = container.scrollTop;
    });

    // Observer to scroll to bottom when content changes
    const observer = new MutationObserver(() => {
        if (isAutoScrollEnabled) {
            container.scrollTop = container.scrollHeight;
            lastScrollTop = container.scrollTop;
        }
    });
    observer.observe(container, { childList: true, subtree: true, characterData: true });

    // Initial state
    updateButtonState();
    if (isAutoScrollEnabled) {
        container.scrollTop = container.scrollHeight;
        lastScrollTop = container.scrollTop;
    }
};

/**
 * Packs variable-width buttons into as few flex rows as possible.
 * Returns the optimized order without modifying the DOM.
 */
window.getPackedButtonOrder = function(container) {
    if (!container || !container.parentElement) return [];

    const buttons = Array.from(container.querySelectorAll(':scope > button'));
    const availableWidth = container.parentElement.clientWidth;
    const gap = parseFloat(getComputedStyle(container).columnGap) || 0;

    if (buttons.length < 2 || availableWidth <= 0) {
        return buttons.map(button => button.dataset.modelName);
    }

    const items = buttons.map((button, originalIndex) => ({
        name: button.dataset.modelName,
        width: button.getBoundingClientRect().width,
        originalIndex
    }));

    // Largest-first best-fit packing fills gaps that normal flex wrapping leaves behind.
    items.sort((left, right) =>
        right.width - left.width || left.originalIndex - right.originalIndex);

    const rows = [];
    for (const item of items) {
        let bestRow = null;
        let smallestRemainder = Number.POSITIVE_INFINITY;

        for (const row of rows) {
            const usedWidth = row.width + gap + item.width;
            const remainder = availableWidth - usedWidth;
            if (remainder >= 0 && remainder < smallestRemainder) {
                bestRow = row;
                smallestRemainder = remainder;
            }
        }

        if (bestRow) {
            bestRow.items.push(item);
            bestRow.width += gap + item.width;
        } else {
            rows.push({ items: [item], width: item.width });
        }
    }

    return rows.flatMap(row => row.items.map(item => item.name));
};
