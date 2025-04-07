// Move these functions to the global scope for use in both initializeRangeSlider and updateSliderTooltips
function formatTime(seconds) {
    const hrs = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    return `${hrs.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

function getParsed(currentFrom, currentTo) {
    const from = parseInt(currentFrom.value, 10);
    const to = parseInt(currentTo.value, 10);
    return [from, to];
}

function fillSlider(from, to, sliderColor, rangeColor, controlSlider) {
    const rangeDistance = to.max - to.min;
    const fromPosition = from.value - to.min;
    const toPosition = to.value - to.min;
    controlSlider.style.background = `linear-gradient(
        to right,
        ${sliderColor} 0%,
        ${sliderColor} ${(fromPosition / rangeDistance) * 100}%,
        ${rangeColor} ${(fromPosition / rangeDistance) * 100}%,
        ${rangeColor} ${(toPosition / rangeDistance) * 100}%,
        ${sliderColor} ${(toPosition / rangeDistance) * 100}%,
        ${sliderColor} 100%)`;
}

function setTooltip(slider, tooltip) {
    const value = slider.value;
    tooltip.textContent = formatTime(value);
    const thumbPosition = (value - slider.min) / (slider.max - slider.min);
    const percent = thumbPosition * 100;
    const markerWidth = 20;
    const offset = (((percent - 50) / 50) * markerWidth) / 2;
    tooltip.style.left = `calc(${percent}% - ${offset}px)`;
}

function controlFromSlider(fromSlider, toSlider) {
    const [from, to] = getParsed(fromSlider, toSlider);
    fillSlider(fromSlider, toSlider, '#7e6fff0f', '#7e6fff', toSlider);
    if (from > to) {
        fromSlider.value = to;
    }
    setTooltip(fromSlider, document.querySelector('#fromSliderTooltip'));
}

function controlToSlider(fromSlider, toSlider) {
    const [from, to] = getParsed(fromSlider, toSlider);
    fillSlider(fromSlider, toSlider, '#7e6fff0f', '#7e6fff', toSlider);
    if (from <= to) {
        toSlider.value = to;
    } else {
        toSlider.value = from;
    }
    setTooltip(toSlider, document.querySelector('#toSliderTooltip'));
}

// Main initialization function
function initializeRangeSlider() {
    const fromSlider = document.querySelector('#fromSlider');
    const toSlider = document.querySelector('#toSlider');
    const scale = document.getElementById('scale');

    if (!fromSlider || !toSlider || !scale) {
        console.warn("Los elementos del rango no se encuentran en esta página.");
        return;
    }

    const MIN = parseInt(fromSlider.getAttribute('min'));
    const MAX = parseInt(fromSlider.getAttribute('max'));
    const STEPS = parseInt(scale.dataset.steps);

    // Events
    fromSlider.oninput = () => controlFromSlider(fromSlider, toSlider);
    toSlider.oninput = () => controlToSlider(fromSlider, toSlider);

    // Initial setup
    fillSlider(fromSlider, toSlider, '#7e6fff0f', '#7e6fff', toSlider);
    setTooltip(fromSlider, document.querySelector('#fromSliderTooltip'));
    setTooltip(toSlider, document.querySelector('#toSliderTooltip'));
}

// New function to update tooltips programmatically
function updateSliderTooltips(fromValue, toValue) {
    const fromSlider = document.querySelector('#fromSlider');
    const toSlider = document.querySelector('#toSlider');

    if (fromSlider && toSlider) {
        // Set slider values
        fromSlider.value = fromValue;
        toSlider.value = toValue;

        // Update tooltips
        setTooltip(fromSlider, document.querySelector('#fromSliderTooltip'));
        setTooltip(toSlider, document.querySelector('#toSliderTooltip'));

        // Update slider fill
        fillSlider(fromSlider, toSlider, '#7e6fff0f', '#7e6fff', toSlider);
    }
}

// Make functions available globally
window.initializeRangeSlider = initializeRangeSlider;
window.updateSliderTooltips = updateSliderTooltips;