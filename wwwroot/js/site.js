// JavaScript customizado para BelgaAuth

// Funções utilitárias
function showNotification(message, type = 'success') {
    const notification = document.createElement('div');
    notification.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
    notification.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
    notification.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.remove();
    }, 5000);
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('pt-BR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(() => {
        showNotification('Copiado para a área de transferência!', 'success');
    }).catch(() => {
        showNotification('Erro ao copiar', 'danger');
    });
}

// Validação de formulários
function validateEmail(email) {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(email);
}

function validatePassword(password) {
    return password.length >= 6;
}

// Animações ao carregar
document.addEventListener('DOMContentLoaded', () => {
    const elements = document.querySelectorAll('.fade-in');
    elements.forEach((el, index) => {
        setTimeout(() => {
            el.style.opacity = '1';
        }, index * 100);
    });
});






// Remove red colors from table headers - Force cyan color
function fixTableHeaderColors() {
    // Get all table headers
    const tableHeads = document.querySelectorAll('thead, thead tr, thead th, .table thead, .table thead tr, .table thead th');
    
    tableHeads.forEach(element => {
        // Remove any Bootstrap danger classes
        element.classList.remove('table-danger', 'bg-danger', 'text-danger');
        
        // Force cyan background
        element.style.backgroundColor = 'rgba(56, 184, 209, 0.2) !important';
        element.style.background = 'linear-gradient(135deg, rgba(56, 184, 209, 0.2), rgba(77, 212, 232, 0.1)) !important';
        element.style.color = '#f5f5f5 !important';
        element.style.borderColor = 'rgba(56, 184, 209, 0.3) !important';
    });
    
    // Also fix individual th elements
    const thElements = document.querySelectorAll('th');
    thElements.forEach(th => {
        th.style.backgroundColor = 'rgba(56, 184, 209, 0.2) !important';
        th.style.background = 'linear-gradient(135deg, rgba(56, 184, 209, 0.2), rgba(77, 212, 232, 0.1)) !important';
        th.style.color = '#f5f5f5 !important';
        th.style.borderColor = 'rgba(56, 184, 209, 0.3) !important';
    });
}

// Run on page load
document.addEventListener('DOMContentLoaded', fixTableHeaderColors);

// Run periodically to catch dynamically added tables
setInterval(fixTableHeaderColors, 1000);
