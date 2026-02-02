import './OrderStatusTimeline.css';

// All possible statuses in order
const ALL_STATUSES = [
  {
    code: 'PENDING',
    displayName: 'En attente',
    icon: '⏳'
  },
  {
    code: 'CONFIRMED',
    displayName: 'Confirmée',
    icon: '✓'
  },
  {
    code: 'IN_PREPARATION',
    displayName: 'Préparation',
    icon: '📦'
  },
  {
    code: 'SHIPPING',
    displayName: 'Livraison',
    icon: '🚚'
  },
  {
    code: 'DELIVERED',
    displayName: 'Livrée',
    icon: '✅'
  }
];

const CANCELED_STATUS = {
  code: 'CANCELED',
  displayName: 'Annulée',
  icon: '✕',
  description: 'Commande annulée'
};

// Map French display names to status codes (handle various spellings/accents)
const STATUS_NAME_MAP: Record<string, string> = {
  'en attente': 'PENDING',
  'attente': 'PENDING',
  'pending': 'PENDING',
  'confirmée': 'CONFIRMED',
  'confirmee': 'CONFIRMED',
  'confirmed': 'CONFIRMED',
  'en préparation': 'IN_PREPARATION',
  'en preparation': 'IN_PREPARATION',
  'préparation': 'IN_PREPARATION',
  'preparation': 'IN_PREPARATION',
  'in_preparation': 'IN_PREPARATION',
  'en livraison': 'SHIPPING',
  'livraison': 'SHIPPING',
  'shipping': 'SHIPPING',
  'expédiée': 'SHIPPING',
  'expediee': 'SHIPPING',
  'livrée': 'DELIVERED',
  'livree': 'DELIVERED',
  'delivered': 'DELIVERED',
  'annulée': 'CANCELED',
  'annulee': 'CANCELED',
  'canceled': 'CANCELED',
  'cancelled': 'CANCELED'
};

interface OrderStatusTimelineProps {
  orderId: string;
  currentStatus: string;
  description?: string;
  lastUpdated?: string;
}

export function OrderStatusTimeline({ orderId, currentStatus, description, lastUpdated }: OrderStatusTimelineProps) {
  // Normalize the status name to code
  const statusLower = currentStatus.toLowerCase().trim();
  const normalizedStatus = STATUS_NAME_MAP[statusLower] || currentStatus.toUpperCase().replace(/\s+/g, '_');
  const isCanceled = normalizedStatus === 'CANCELED';
  
  // Find current status index
  const currentIndex = ALL_STATUSES.findIndex(s => s.code === normalizedStatus);
  
  console.log('[OrderStatusTimeline] Status mapping:', { 
    input: currentStatus, 
    lower: statusLower,
    normalized: normalizedStatus, 
    currentIndex 
  });
  
  return (
    <div className="order-status-timeline">
      <div className="timeline-header">
        <div className="timeline-icon">📦</div>
        <div className="timeline-title">
          <h3>Suivi de commande</h3>
          <span className="order-id">{orderId}</span>
        </div>
      </div>

      {isCanceled ? (
        <div className="timeline-canceled">
          <div className="canceled-badge">
            <span className="canceled-icon">{CANCELED_STATUS.icon}</span>
            <span className="canceled-text">{CANCELED_STATUS.displayName}</span>
          </div>
          <p className="canceled-description">{description || CANCELED_STATUS.description}</p>
        </div>
      ) : (
        <div className="timeline-track">
          {ALL_STATUSES.map((status, index) => {
            const isCompleted = currentIndex >= 0 && index < currentIndex;
            const isCurrent = index === currentIndex;
            const isPending = currentIndex >= 0 && index > currentIndex;
            
            return (
              <div 
                key={status.code}
                className={`timeline-step ${isCompleted ? 'completed' : ''} ${isCurrent ? 'current' : ''} ${isPending ? 'pending' : ''}`}
              >
                <div className="step-connector">
                  <div className="connector-line" />
                </div>
                <div className="step-marker">
                  {isCompleted ? (
                    <svg className="check-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                      <polyline points="20,6 9,17 4,12" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                  ) : isCurrent ? (
                    <span className="current-icon">{status.icon}</span>
                  ) : (
                    <span className="pending-dot" />
                  )}
                </div>
                <div className="step-content">
                  <span className="step-label">
                    {status.displayName}
                  </span>
                  {isCurrent && (
                    <span className="current-badge">Actuel</span>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {!isCanceled && description && (
        <div className="timeline-description">
          <div className="description-icon">💬</div>
          <p>{description}</p>
        </div>
      )}

      {lastUpdated && (
        <div className="timeline-footer">
          <span className="update-label">Mise à jour :</span>
          <span className="update-time">{lastUpdated}</span>
        </div>
      )}
    </div>
  );
}

// Helper to clean up text by removing markdown bold markers
function cleanText(text: string): string {
  return text
    .replace(/\*\*/g, '')  // Remove **
    .replace(/\*/g, '')     // Remove single *
    .trim();
}

// Helper function to parse order status from AI message
export function parseOrderStatusMessage(content: string): {
  orderId: string;
  status: string;
  description?: string;
  lastUpdated?: string;
} | null {
  // Check if message contains order status pattern
  if (!content.includes('📦') && !content.toLowerCase().includes('statut de la commande')) {
    return null;
  }
  
  // Extract order ID (ORD-XXXX-XXX pattern)
  const orderIdMatch = content.match(/ORD-\d{4}-\d{3}/i);
  if (!orderIdMatch) return null;
  
  const orderId = orderIdMatch[0];
  
  // Extract status - find line containing "Statut" and ":"
  const lines = content.split('\n');
  let status = '';
  let description: string | undefined;
  let lastUpdated: string | undefined;
  
  for (const line of lines) {
    const cleanLine = cleanText(line);
    
    // Match "Statut : xxx" pattern
    if (cleanLine.toLowerCase().startsWith('statut') && cleanLine.includes(':')) {
      const colonIndex = cleanLine.indexOf(':');
      status = cleanLine.substring(colonIndex + 1).trim();
    }
    
    // Match "Description : xxx" pattern
    if (cleanLine.toLowerCase().startsWith('description') && cleanLine.includes(':')) {
      const colonIndex = cleanLine.indexOf(':');
      description = cleanLine.substring(colonIndex + 1).trim();
    }
    
    // Match "Dernière mise à jour : xxx" pattern
    if (cleanLine.toLowerCase().includes('mise à jour') && cleanLine.includes(':')) {
      const colonIndex = cleanLine.indexOf(':');
      lastUpdated = cleanLine.substring(colonIndex + 1).trim();
    }
  }
  
  if (!status) return null;
  
  console.log('[OrderStatusTimeline] Parsed:', { orderId, status, description, lastUpdated });
  
  return { orderId, status, description, lastUpdated };
}
