import './MessageFormatter.css';
import { OrderStatusTimeline, parseOrderStatusMessage } from './OrderStatusTimeline';

interface MessageFormatterProps {
  content: string;
  hideSellerInfo?: boolean;
  customerName?: string;
}

export function MessageFormatter({ content, hideSellerInfo, customerName }: MessageFormatterProps) {
  // Check if message contains order status information
  const orderStatus = parseOrderStatusMessage(content);
  if (orderStatus) {
    return (
      <OrderStatusTimeline
        orderId={orderStatus.orderId}
        currentStatus={orderStatus.status}
        description={orderStatus.description}
        lastUpdated={orderStatus.lastUpdated}
      />
    );
  }

  // Check if message contains a proposed message to seller (must have the emoji 📝 or 📜)
  const hasProposedMessage = content.includes('📝') || content.includes('📜');
  
  if (hasProposedMessage) {
    return <FormattedProposedMessage content={content} hideSellerInfo={hideSellerInfo} customerName={customerName} />;
  }
  
  // Regular message formatting
  return <FormattedText text={content} />;
}

function replaceSignaturePlaceholders(text: string, customerName?: string): string {
  if (!customerName) return text;
  return text
    .replace(/\[Votre\s+nom\]/gi, customerName)
    .replace(/\[Votre\s+Nom\]/g, customerName)
    .replace(/\[VOTRE\s+NOM\]/g, customerName);
}

function FormattedProposedMessage({
  content,
  hideSellerInfo,
  customerName,
}: {
  content: string;
  hideSellerInfo?: boolean;
  customerName?: string;
}) {
  // Look for either 📝 or 📜 emoji (agent might use either)
  const proposedMessageStart = content.indexOf('📝') !== -1 ? content.indexOf('📝') : content.indexOf('📜');
  
  // Look for seller requirements section
  let sellerInfoStart = content.indexOf('💡');
  if (sellerInfoStart === -1) {
    sellerInfoStart = content.indexOf('Le vendeur pourrait');
  }
  if (sellerInfoStart === -1) {
    sellerInfoStart = content.indexOf('vendeur pourrait aussi demander');
  }
  
  const approvalPatterns = [
    'Cliquez sur le bouton',
    'Si ce message vous convient',
    'Approuvez-vous ce message',
    'Vous pouvez approuver',
    'Souhaitez-vous apporter',
  ];
  
  let approvalStart = -1;
  for (const pattern of approvalPatterns) {
    const index = content.indexOf(pattern);
    if (index !== -1 && (approvalStart === -1 || index < approvalStart)) {
      approvalStart = index;
    }
  }
  
  let introText = '';
  let proposedMessage = '';
  let sellerInfo = '';
  let approvalText = '';
  
  if (proposedMessageStart !== -1) {
    introText = content.substring(0, proposedMessageStart).trim();
    
    // Determine end of proposed message
    let messageEnd = content.length;
    if (sellerInfoStart !== -1) {
      messageEnd = sellerInfoStart;
    } else if (approvalStart !== -1) {
      messageEnd = approvalStart;
    }
    
    const rawProposed = content.substring(proposedMessageStart, messageEnd);
    const messageHeaderEnd = rawProposed.indexOf(':');
    if (messageHeaderEnd !== -1) {
      proposedMessage = rawProposed.substring(messageHeaderEnd + 1).trim();
      proposedMessage = proposedMessage.replace(/\*\*/g, '').trim();
      proposedMessage = replaceSignaturePlaceholders(proposedMessage, customerName);
    }
    
    // Extract seller info section (optional)
    if (!hideSellerInfo && sellerInfoStart !== -1) {
      let sellerInfoEnd = approvalStart !== -1 ? approvalStart : content.length;
      const rawSellerInfo = content.substring(sellerInfoStart, sellerInfoEnd);
      const infoHeaderEnd = rawSellerInfo.indexOf(':');
      if (infoHeaderEnd !== -1) {
        sellerInfo = rawSellerInfo.substring(infoHeaderEnd + 1).trim();
        sellerInfo = sellerInfo.replace(/\*\*/g, '').trim();
      }
    }
    
    // Extract approval text
    if (approvalStart !== -1) {
      approvalText = content.substring(approvalStart).trim();
    }
  }
  
  return (
    <div className="formatted-message">
      {introText && (
        <div className="intro-text">
          <FormattedText text={introText} />
        </div>
      )}
      
      {proposedMessage && (
        <div className="proposed-message-box">
          <div className="proposed-message-header">
            <span className="proposed-icon">📝</span>
            <span className="proposed-title">Message proposé au vendeur</span>
          </div>
          <div className="proposed-message-content">
            <FormattedText text={proposedMessage} />
          </div>
        </div>
      )}
      
      {sellerInfo && (
        <div className="seller-info-box">
          <div className="seller-info-header">
            <span className="seller-info-icon">💡</span>
            <span className="seller-info-title">Le vendeur pourrait aussi demander</span>
          </div>
          <div className="seller-info-content">
            <FormattedText text={sellerInfo} />
          </div>
        </div>
      )}
      
      {approvalText && (
        <div className="approval-hint">
          {approvalText}
        </div>
      )}
    </div>
  );
}

function FormattedText({ text }: { text: string }) {
  const lines = text.split(/\n/);
  const elements: React.ReactNode[] = [];
  let currentList: string[] = [];
  let listType: 'numbered' | 'bullet' | null = null;
  let keyIndex = 0;

  const flushList = () => {
    if (currentList.length > 0 && listType) {
      if (listType === 'numbered') {
        elements.push(
          <ol key={`list-${keyIndex++}`} className="question-list">
            {currentList.map((item, idx) => (
              <li key={idx} className="question-item">
                {formatLine(item)}
              </li>
            ))}
          </ol>
        );
      } else {
        elements.push(
          <ul key={`list-${keyIndex++}`} className="bullet-list">
            {currentList.map((item, idx) => (
              <li key={idx} className="bullet-item">
                {formatLine(item)}
              </li>
            ))}
          </ul>
        );
      }
      currentList = [];
      listType = null;
    }
  };

  for (const line of lines) {
    const trimmedLine = line.trim();
    if (!trimmedLine) continue;

    // Check for horizontal rule (---)
    if (trimmedLine === '---' || trimmedLine === '***' || trimmedLine === '___') {
      flushList();
      elements.push(<hr key={`hr-${keyIndex++}`} className="policy-divider" />);
      continue;
    }

    // Check for h3 header (### )
    const h3Match = trimmedLine.match(/^###\s+(.+)$/);
    if (h3Match) {
      flushList();
      elements.push(
        <h3 key={`h3-${keyIndex++}`} className="policy-section-title">
          {formatLine(h3Match[1])}
        </h3>
      );
      continue;
    }

    // Check for h2 header (## )
    const h2Match = trimmedLine.match(/^##\s+(.+)$/);
    if (h2Match) {
      flushList();
      elements.push(
        <h2 key={`h2-${keyIndex++}`} className="policy-title">
          {formatLine(h2Match[1])}
        </h2>
      );
      continue;
    }

    // Check if line is a numbered item (1. or 1) format)
    const numberedMatch = trimmedLine.match(/^(\d+)[\.)]\s*(.+)$/);
    // Check if line is a bullet item (- or • or ◦)
    const bulletMatch = trimmedLine.match(/^[-•◦]\s*(.+)$/);
    
    if (numberedMatch) {
      if (listType !== 'numbered') {
        flushList();
        listType = 'numbered';
      }
      currentList.push(numberedMatch[2]);
    } else if (bulletMatch) {
      if (listType !== 'bullet') {
        flushList();
        listType = 'bullet';
      }
      currentList.push(bulletMatch[1]);
    } else {
      flushList();
      elements.push(
        <p key={`p-${keyIndex++}`} className="formatted-line">
          {formatLine(trimmedLine)}
        </p>
      );
    }
  }

  flushList();

  return <>{elements}</>;
}

function formatLine(text: string): React.ReactNode {
  // Process bold (**text**) and italic (*text*) formatting
  const parts: React.ReactNode[] = [];
  let remaining = text;
  let keyIndex = 0;
  
  while (remaining.length > 0) {
    // Find bold (**) or italic (*) markers
    const boldStart = remaining.indexOf('**');
    const italicStart = remaining.search(/(?<!\*)\*(?!\*)/); // Single * not preceded or followed by another *
    
    // Determine which comes first
    let nextMarker: 'bold' | 'italic' | null = null;
    let markerPos = -1;
    
    if (boldStart !== -1 && (italicStart === -1 || boldStart < italicStart)) {
      nextMarker = 'bold';
      markerPos = boldStart;
    } else if (italicStart !== -1) {
      nextMarker = 'italic';
      markerPos = italicStart;
    }
    
    if (nextMarker === null) {
      parts.push(remaining);
      break;
    }
    
    // Add text before the marker
    if (markerPos > 0) {
      parts.push(remaining.substring(0, markerPos));
    }
    
    if (nextMarker === 'bold') {
      const boldEnd = remaining.indexOf('**', markerPos + 2);
      if (boldEnd === -1) {
        parts.push(remaining.substring(markerPos));
        break;
      }
      const boldText = remaining.substring(markerPos + 2, boldEnd);
      parts.push(<strong key={`b-${keyIndex++}`}>{boldText}</strong>);
      remaining = remaining.substring(boldEnd + 2);
    } else {
      // Find closing italic marker (single * not preceded or followed by another *)
      const afterFirst = remaining.substring(markerPos + 1);
      const italicEnd = afterFirst.search(/(?<!\*)\*(?!\*)/);
      if (italicEnd === -1) {
        parts.push(remaining.substring(markerPos));
        break;
      }
      const italicText = afterFirst.substring(0, italicEnd);
      parts.push(<em key={`i-${keyIndex++}`} className="italic-text">{italicText}</em>);
      remaining = afterFirst.substring(italicEnd + 1);
    }
  }
  
  return <>{parts}</>;
}
