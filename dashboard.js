const API_BASE = 'http://localhost:5153';

async function loadDashboardMetrics() {
    try {
        // Load summary metrics
        const summaryResponse = await fetch(`${API_BASE}/api/metrics/summary`);
        const summary = await summaryResponse.json();
        
        document.getElementById('total-opportunities').textContent = summary.totalOpportunities;
        document.getElementById('total-categories').textContent = `${summary.categories} categorías`;
        document.getElementById('average-score').textContent = `${Math.round(summary.averageScore)}%`;
        document.getElementById('priority-opportunities').textContent = summary.priorityOpportunities;
        document.getElementById('total-amount').textContent = `S/ ${formatNumber(summary.totalAmount)}`;
        
        // Load category chart data
        const categoryResponse = await fetch(`${API_BASE}/api/metrics/by-category`);
        const categoryData = await categoryResponse.json();
        renderCategoryChart(categoryData);
        
        // Load entity chart data
        const entityResponse = await fetch(`${API_BASE}/api/metrics/by-entity`);
        const entityData = await entityResponse.json();
        renderEntityChart(entityData);
        
    } catch (error) {
        console.error('Error loading dashboard metrics:', error);
    }
}

function formatNumber(num) {
    if (num >= 1000000) {
        return (num / 1000000).toFixed(1) + 'M';
    }
    if (num >= 1000) {
        return (num / 1000).toFixed(0) + 'K';
    }
    return num.toFixed(0);
}

function renderCategoryChart(data) {
    const ctx = document.getElementById('categoryChart').getContext('2d');
    
    const labels = data.map(d => d.category);
    const counts = data.map(d => d.count);
    
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Oportunidades',
                data: counts,
                backgroundColor: 'rgba(59, 130, 246, 0.6)',
                borderColor: 'rgba(59, 130, 246, 1)',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf'
                    },
                    grid: {
                        color: 'rgba(154, 181, 219, 0.12)'
                    }
                },
                x: {
                    ticks: {
                        color: '#92a4bf'
                    },
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
}

function renderEntityChart(data) {
    const ctx = document.getElementById('entityChart').getContext('2d');
    
    const labels = data.map(d => d.entity);
    const counts = data.map(d => d.count);
    
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Oportunidades',
                data: counts,
                backgroundColor: 'rgba(28, 200, 183, 0.6)',
                borderColor: 'rgba(28, 200, 183, 1)',
                borderWidth: 1
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf'
                    },
                    grid: {
                        display: false
                    }
                },
                x: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf'
                    },
                    grid: {
                        color: 'rgba(154, 181, 219, 0.12)'
                    }
                }
            }
        }
    });
}

// Load metrics when page loads
document.addEventListener('DOMContentLoaded', loadDashboardMetrics);
