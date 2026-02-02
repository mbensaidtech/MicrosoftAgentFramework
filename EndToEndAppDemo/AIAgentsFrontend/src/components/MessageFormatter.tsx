import './MessageFormatter.css';

interface MessageFormatterProps {
  content: string;
}

/**
 * Formats chat message content, handling:
 * - Sources section with styled badges
 * - Basic markdown (bold, lists)
 * - Line breaks
 */
export function MessageFormatter({ content }: MessageFormatterProps) {
  // Find sources section - look for the emoji or "Sources:" text
  // Handles: "📚 **Sources:**", "📚 Sources:", "**Sources:**", etc.
  let sourcesIndex = -1;
  
  // Try to find the sources marker
  const markers = ['📚', '**Sources:**', '**Sources**:', 'Sources:'];
  for (const marker of markers) {
    const idx = content.indexOf(marker);
    if (idx !== -1) {
      sourcesIndex = idx;
      break;
    }
  }
  
  if (sourcesIndex !== -1) {
    const mainContent = content.substring(0, sourcesIndex).trim();
    const sourcesSection = content.substring(sourcesIndex);
    
    // Extract source items
    const sources = extractSources(sourcesSection);
    
    if (sources.length > 0) {
      return (
        <div className="formatted-message">
          <div className="main-content">
            {formatText(mainContent)}
          </div>
          <SourcesBadges sources={sources} />
        </div>
      );
    }
  }

  // No sources found, just format the text
  return (
    <div className="formatted-message">
      {formatText(content)}
    </div>
  );
}

/**
 * Extract source items from the sources section
 */
function extractSources(content: string): string[] {
  const sources: string[] = [];
  const lines = content.split('\n');
  
  for (const line of lines) {
    const trimmed = line.trim();
    // Skip header lines
    if (trimmed.includes('Sources') || trimmed === '📚' || !trimmed) {
      continue;
    }
    // Extract source items (lines starting with - or •)
    if (trimmed.startsWith('-') || trimmed.startsWith('•')) {
      let source = trimmed.substring(1).trim();
      // Remove markdown formatting and brackets
      source = source.replace(/\*\*/g, '').replace(/\[|\]/g, '');
      if (source) {
        sources.push(source);
      }
    }
  }
  
  return sources;
}

/**
 * Renders sources as small badges (similar to "Streaming" badge style)
 */
function SourcesBadges({ sources }: { sources: string[] }) {
  return (
    <div className="sources-badges-container">
      {sources.map((source, index) => (
        <span key={index} className="source-badge">
          {source}
        </span>
      ))}
    </div>
  );
}

/**
 * Formats text with basic markdown support
 */
function formatText(text: string): React.ReactNode[] {
  const lines = text.split('\n');
  const elements: React.ReactNode[] = [];
  
  lines.forEach((line, lineIndex) => {
    if (lineIndex > 0) {
      elements.push(<br key={`br-${lineIndex}`} />);
    }
    
    // Process inline formatting
    const parts = processInlineFormatting(line);
    elements.push(<span key={`line-${lineIndex}`}>{parts}</span>);
  });
  
  return elements;
}

/**
 * Processes inline markdown formatting (bold)
 */
function processInlineFormatting(text: string): React.ReactNode[] {
  const parts: React.ReactNode[] = [];
  const remaining = text;
  let keyIndex = 0;
  
  // Match **bold** text
  const boldRegex = /\*\*([^*]+)\*\*/g;
  let lastIndex = 0;
  let match;
  
  while ((match = boldRegex.exec(text)) !== null) {
    // Add text before the match
    if (match.index > lastIndex) {
      parts.push(text.substring(lastIndex, match.index));
    }
    // Add bold text
    parts.push(<strong key={`bold-${keyIndex++}`}>{match[1]}</strong>);
    lastIndex = match.index + match[0].length;
  }
  
  // Add remaining text
  if (lastIndex < text.length) {
    parts.push(text.substring(lastIndex));
  }
  
  return parts.length > 0 ? parts : [remaining];
}

export default MessageFormatter;
